using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteriorDesign.Domain.State;

/// <summary>
/// Contrato JSON del DesignState (schemas/design-state.schema.json).
/// Es la fuente de verdad que viaja entre API, frontend, IA, motor y Blender.
/// </summary>
public class DesignState
{
    public string SchemaVersion { get; set; } = "1.0";
    public required ProjectInfo Project { get; set; }
    public required DesignInfo Design { get; set; }
    public required RoomState Room { get; set; }
    public List<RequirementState> Requirements { get; set; } = [];
    public List<PlacedObject> Objects { get; set; } = [];
    public List<AppliedMaterial> Materials { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Lighting { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PriceRange? Installation { get; set; }
    public List<string> StructuralConfirmations { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BudgetSnapshot? Budget { get; set; }
    public List<HistoryEntryState> History { get; set; } = [];
}

public class ProjectInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
}

public class DesignInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ParentId { get; set; }
    public required string Style { get; set; }
    public required string BudgetMode { get; set; }
    public int Revision { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Author { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CreatedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? UpdatedAt { get; set; }
    public bool Favorite { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Comments { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ThumbnailUrl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RenderUrl { get; set; }
}

public class RoomState
{
    public required string Id { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    public required string SpaceType { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string MeasurementSource { get; set; } = "medidas_usuario";
    public List<DoorState> Doors { get; set; } = [];
    public List<WindowState> Windows { get; set; } = [];
    public List<ColumnState> Columns { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Surfaces { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Commercial { get; set; }
}

public class DoorState
{
    public required string Id { get; set; }
    public required string Wall { get; set; }
    public int Position { get; set; }
    public int Width { get; set; }
}

public class WindowState
{
    public required string Id { get; set; }
    public required string Wall { get; set; }
    public int Position { get; set; }
    public int Width { get; set; }
    public int SillHeight { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Height { get; set; }
}

public class ColumnState
{
    public required string Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Depth { get; set; }
}

/// <summary>Requisito con prioridad. Segun Type se usan unos campos u otros (ver Requirement en la BD).</summary>
public class RequirementState
{
    public required string Id { get; set; }
    public required string Priority { get; set; }
    public required string Type { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? Amount { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Category { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Attribute { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public JsonElement? Value { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Element { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Style { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Color { get; set; }
}

public class PlacedObject
{
    public required string InstanceId { get; set; }
    public required string CatalogId { get; set; }
    public required string Type { get; set; }
    public required Position Position { get; set; }
    public int Rotation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Scale? Scale { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ObjectConfig? Config { get; set; }
    public bool Locked { get; set; }
}

public class Position
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

public class Scale
{
    public double X { get; set; } = 1;
    public double Y { get; set; } = 1;
    public double Z { get; set; } = 1;
}

public class ObjectConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Color { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Material { get; set; }
}

public class AppliedMaterial
{
    public required string MaterialId { get; set; }
    public decimal AreaM2 { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Surface { get; set; }
}

public class PriceRange
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }

    public static PriceRange Zero => new() { Min = 0, Max = 0 };
    public static PriceRange operator +(PriceRange a, PriceRange b) => new() { Min = a.Min + b.Min, Max = a.Max + b.Max };
}

public class BudgetSnapshot
{
    public Dictionary<string, PriceRange> ByCategory { get; set; } = [];
    public required PriceRange Total { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? EffectiveCap { get; set; }
}

public class HistoryEntryState
{
    public DateTime Date { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Author { get; set; }
    public required string Description { get; set; }
    public List<string> ChangedInstanceIds { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Revision { get; set; }
}

public static class StateJson
{
    /// <summary>Opciones JSON del contrato: camelCase, sin distinguir mayusculas al leer.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
