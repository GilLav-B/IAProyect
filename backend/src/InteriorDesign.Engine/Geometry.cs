using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>Rectangulo en planta (cm). (X1,Y1) = esquina suroeste.</summary>
public readonly record struct Rect(int X1, int Y1, int X2, int Y2)
{
    public int Area => (X2 - X1) * (Y2 - Y1);

    /// <summary>Solape estricto (tocarse en el borde no cuenta), igual que rect_solapa/2.</summary>
    public bool Overlaps(Rect o) => X1 < o.X2 && X2 > o.X1 && Y1 < o.Y2 && Y2 > o.Y1;
}

public static class Geometry
{
    /// <summary>Dimensiones en planta segun la rotacion (dims_rot/5).</summary>
    public static (int A, int B) Rotated(int rotation, int width, int depth) =>
        rotation is 90 or 270 ? (depth, width) : (width, depth);

    /// <summary>Huella del producto colocado en (x,y) con la rotacion dada (rect_producto/5).</summary>
    public static Rect Footprint(Dimensions d, int x, int y, int rotation)
    {
        var (a, b) = Rotated(rotation, d.Width, d.Depth);
        return new Rect(x, y, x + a, y + b);
    }

    public static bool Inside(RoomState room, Rect r) =>
        r.X1 >= 0 && r.Y1 >= 0 && r.X2 <= room.Length && r.Y2 <= room.Width;

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
