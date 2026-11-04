using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteriorDesign.Domain.State;

/// <summary>Contrato JSON del catalogo (schemas/catalog.schema.json).</summary>
public class CatalogState
{
    public List<ProductState> Products { get; set; } = [];
    public List<MaterialState> Materials { get; set; } = [];
    public List<ProviderState> Providers { get; set; } = [];
}

public class ProductState
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Kind { get; set; }
    public required Dimensions Dimensions { get; set; }
    public List<string> Materials { get; set; } = [];
    public List<string> Colors { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public required PriceRange Price { get; set; }
    public Dictionary<string, JsonElement> Attributes { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ProviderId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Sku { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Availability { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Asset3D { get; set; }
    public List<string> Images { get; set; } = [];
    public List<string> Variants { get; set; } = [];

    [JsonIgnore] public bool IsConceptual => Kind == "conceptual";
}

public class Dimensions
{
    public int Width { get; set; }
    public int Depth { get; set; }
    public int Height { get; set; }
}

public class MaterialState
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public required PriceRange PricePerM2 { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Color { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Finish { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Texture { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ProviderId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Sku { get; set; }
}

public class ProviderState
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string BudgetCategory { get; set; }
    public required string Adapter { get; set; }
}
