using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>Rectangulo en planta (cm). (X1,Y1) = esquina suroeste.</summary>
public readonly record struct Rect(int X1, int Y1, int X2, int Y2)
{
    public int Area => (X2 - X1) * (Y2 - Y1);

    /// <summary>Solape estricto (tocarse en el borde no cuenta), igual que rect_solapa/2.</summary>
    public bool Overlaps(Rect o) => X1 < o.X2 && X2 > o.X1 && Y1 < o.Y2 && Y2 > o.Y1;
}

/// <summary>
/// Huella de un mueble en planta. Con rotacion multiplo de 90 es un Rect exacto (mismas reglas que diseno.pl).
/// Con cualquier otro angulo es un rectangulo girado: Bounds es su caja envolvente (la posicion x,y del objeto
/// es la esquina suroeste de esa caja) y los choques se calculan con el teorema del eje separador.
/// </summary>
public readonly struct Footprint
{
    private const double Eps = 1e-6;

    public Rect Bounds { get; }
    public bool AxisAligned { get; }
    /// <summary>Area real (ancho x fondo), igual con o sin rotacion.</summary>
    public double Area { get; }
    private readonly (double X, double Y)[] _corners;

    private Footprint(Rect bounds, bool axisAligned, double area, (double, double)[] corners)
    {
        Bounds = bounds;
        AxisAligned = axisAligned;
        Area = area;
        _corners = corners;
    }

    public static Footprint FromRect(Rect r) =>
        new(r, true, r.Area, [(r.X1, r.Y1), (r.X2, r.Y1), (r.X2, r.Y2), (r.X1, r.Y2)]);

    public static Footprint Rotated(Rect bounds, double area, (double, double)[] corners) => new(bounds, false, area, corners);

    public IReadOnlyList<(double X, double Y)> Corners => _corners;

    public bool Overlaps(Rect r) => Overlaps(FromRect(r));

    /// <summary>Solape estricto: tocarse en el borde no cuenta.</summary>
    public bool Overlaps(Footprint o)
    {
        if (AxisAligned && o.AxisAligned) return Bounds.Overlaps(o.Bounds);
        if (!Bounds.Overlaps(o.Bounds)) return false;
        foreach (var axis in Axes().Concat(o.Axes()))
        {
            var (minA, maxA) = Project(axis);
            var (minB, maxB) = o.Project(axis);
            if (Math.Min(maxA, maxB) - Math.Max(minA, minB) <= Eps) return false;
        }
        return true;
    }

    /// <summary>Todas las esquinas dentro del cuarto.</summary>
    public bool Inside(RoomState room) => AxisAligned
        ? Bounds.X1 >= 0 && Bounds.Y1 >= 0 && Bounds.X2 <= room.Length && Bounds.Y2 <= room.Width
        : _corners.All(c => c.X >= -Eps && c.Y >= -Eps && c.X <= room.Length + Eps && c.Y <= room.Width + Eps);

    private IEnumerable<(double X, double Y)> Axes()
    {
        for (var i = 0; i < 2; i++)
        {
            var (x1, y1) = _corners[i];
            var (x2, y2) = _corners[i + 1];
            yield return (-(y2 - y1), x2 - x1);
        }
    }

    private (double Min, double Max) Project((double X, double Y) axis)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var (x, y) in _corners)
        {
            var p = x * axis.X + y * axis.Y;
            min = Math.Min(min, p);
            max = Math.Max(max, p);
        }
        return (min, max);
    }
}

public static class Geometry
{
    /// <summary>Rotacion normalizada a 0..359.</summary>
    public static int Normalize(int rotation) => ((rotation % 360) + 360) % 360;

    /// <summary>Dimensiones en planta segun la rotacion (dims_rot/5). Solo para multiplos de 90.</summary>
    public static (int A, int B) Rotated(int rotation, int width, int depth) =>
        Normalize(rotation) is 90 or 270 ? (depth, width) : (width, depth);

    /// <summary>
    /// Huella del producto colocado en (x,y) con la rotacion dada (rect_producto/5 para multiplos de 90).
    /// La rotacion es antihoraria vista desde arriba (de este hacia norte).
    /// </summary>
    public static Footprint Footprint(Dimensions d, int x, int y, int rotation)
    {
        var rot = Normalize(rotation);
        if (rot % 90 == 0)
        {
            var (a, b) = Rotated(rot, d.Width, d.Depth);
            return Engine.Footprint.FromRect(new Rect(x, y, x + a, y + b));
        }

        var t = rot * Math.PI / 180;
        double cos = Math.Cos(t), sin = Math.Sin(t);
        double bw = d.Width * Math.Abs(cos) + d.Depth * Math.Abs(sin);
        double bh = d.Width * Math.Abs(sin) + d.Depth * Math.Abs(cos);
        double cx = x + bw / 2, cy = y + bh / 2;
        var corners = new (double, double)[4];
        var i = 0;
        foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            double lx = dx * d.Width / 2.0, ly = dy * d.Depth / 2.0;
            corners[i++] = (cx + lx * cos - ly * sin, cy + lx * sin + ly * cos);
        }
        var bounds = new Rect(x, y, (int)Math.Ceiling(x + bw - 1e-9), (int)Math.Ceiling(y + bh - 1e-9));
        return Engine.Footprint.Rotated(bounds, (double)d.Width * d.Depth, corners);
    }

    public static bool Inside(RoomState room, Rect r) =>
        r.X1 >= 0 && r.Y1 >= 0 && r.X2 <= room.Length && r.Y2 <= room.Width;

    public static bool Inside(RoomState room, Footprint f) => f.Inside(room);

    /// <summary>Zona libre pegada a una pared (zona_pared/6).</summary>
    public static Rect WallZone(RoomState room, string wall, int position, int width, int depth) => wall switch
    {
        "sur" => new Rect(position, 0, position + width, depth),
        "norte" => new Rect(position, room.Width - depth, position + width, room.Width),
        "oeste" => new Rect(0, position, depth, position + width),
        "este" => new Rect(room.Length - depth, position, room.Length, position + width),
        _ => throw new ArgumentException($"Pared desconocida: {wall}")
    };

    /// <summary>Zona de abatimiento de la puerta: tan profunda como la puerta es ancha.</summary>
    public static Rect DoorZone(RoomState room, DoorState door) =>
        WallZone(room, door.Wall, door.Position, door.Width, door.Width);

    public static Rect WindowZone(RoomState room, WindowState window, int accessCm) =>
        WallZone(room, window.Wall, window.Position, window.Width, accessCm);
}
