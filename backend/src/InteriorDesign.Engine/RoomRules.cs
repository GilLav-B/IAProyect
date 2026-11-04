using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>
/// Reglas de la habitacion que el esquema JSON no puede expresar: rangos de medidas y
/// que puertas, ventanas y columnas queden dentro del cuarto sin encimarse.
/// </summary>
public static class RoomRules
{
    /// <summary>[SUPUESTO] rangos de medidas permitidas (cm).</summary>
    public const int MinSide = 150, MaxSide = 2000, MinHeight = 200, MaxHeight = 600;

    private static int WallLength(RoomState r, string wall) => wall is "norte" or "sur" ? r.Length : r.Width;

    /// <summary>Errores en espanol (vacio = la habitacion es valida).</summary>
    public static List<string> Validate(RoomState r)
    {
        var errors = new List<string>();
        if (r.Length is < MinSide or > MaxSide) errors.Add($"El largo debe estar entre {MinSide / 100.0:0.##} y {MaxSide / 100.0:0.##} m.");
        if (r.Width is < MinSide or > MaxSide) errors.Add($"El ancho debe estar entre {MinSide / 100.0:0.##} y {MaxSide / 100.0:0.##} m.");
        if (r.Height is < MinHeight or > MaxHeight) errors.Add($"El alto debe estar entre {MinHeight / 100.0:0.##} y {MaxHeight / 100.0:0.##} m.");

        var openings = r.Doors.Select(d => (Id: d.Id, d.Wall, d.Position, d.Width, Kind: "puerta"))
            .Concat(r.Windows.Select(w => (Id: w.Id, w.Wall, w.Position, w.Width, Kind: "ventana")))
            .ToList();

        foreach (var dup in openings.GroupBy(o => o.Id).Where(g => g.Count() > 1))
            errors.Add($"Hay dos puertas/ventanas con el id {dup.Key}.");

        foreach (var o in openings)
        {
            var wallLength = WallLength(r, o.Wall);
            if (o.Position < 0 || o.Position + o.Width > wallLength)
                errors.Add($"La {o.Kind} {o.Id} se sale de la pared {o.Wall} (mide {wallLength / 100.0:0.##} m).");
        }

        foreach (var w in r.Windows)
            if (w.SillHeight + (w.Height ?? 0) > r.Height)
                errors.Add($"La ventana {w.Id} es mas alta que el techo.");

        foreach (var wallGroup in openings.GroupBy(o => o.Wall))
        {
            var list = wallGroup.OrderBy(o => o.Position).ToList();
            for (var i = 1; i < list.Count; i++)
                if (list[i].Position < list[i - 1].Position + list[i - 1].Width)
                    errors.Add($"La {list[i].Kind} {list[i].Id} se encima con la {list[i - 1].Kind} {list[i - 1].Id} en la pared {wallGroup.Key}.");
        }

        foreach (var c in r.Columns)
            if (c.X < 0 || c.Y < 0 || c.X + c.Width > r.Length || c.Y + c.Depth > r.Width)
                errors.Add($"La columna {c.Id} queda fuera de la habitacion.");

        return errors;
    }
}
