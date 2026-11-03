namespace InteriorDesign.Domain.Entities;

/// <summary>
/// Habitacion en planta. Origen (0,0) = esquina suroeste; x a lo largo, y a lo ancho. Medidas en cm.
/// </summary>
public class Room
{
    public required string Id { get; set; }
    public required string ProjectId { get; set; }
    public string? Name { get; set; }
    public required string SpaceType { get; set; }
    public int LengthCm { get; set; }
    public int WidthCm { get; set; }
    public int HeightCm { get; set; }
    public string MeasurementSource { get; set; } = "medidas_usuario";
    /// <summary>JSON con acabados de piso, muros y techo.</summary>
    public string? SurfacesJson { get; set; }
    /// <summary>JSON con datos de inmueble comercial (peopleCount, workAreas).</summary>
    public string? CommercialJson { get; set; }

    public Project? Project { get; set; }
    public List<RoomDoor> Doors { get; set; } = [];
    public List<RoomWindow> Windows { get; set; } = [];
    public List<RoomColumn> Columns { get; set; } = [];
    public List<Requirement> Requirements { get; set; } = [];
}

public class RoomDoor
{
    public required string RoomId { get; set; }
    public required string Id { get; set; }
    public required string Wall { get; set; }
    public int PositionCm { get; set; }
    public int WidthCm { get; set; }
}

public class RoomWindow
{
    public required string RoomId { get; set; }
    public required string Id { get; set; }
    public required string Wall { get; set; }
    public int PositionCm { get; set; }
    public int WidthCm { get; set; }
    public int SillHeightCm { get; set; }
    public int? HeightCm { get; set; }
}

public class RoomColumn
{
    public required string RoomId { get; set; }
    public required string Id { get; set; }
    public int XCm { get; set; }
    public int YCm { get; set; }
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
}
