namespace InteriorDesign.Domain.Entities;

public class Provider
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string BudgetCategory { get; set; }
    /// <summary>Formato del adaptador: json | csv</summary>
    public required string Adapter { get; set; }
}

/// <summary>Producto del catalogo (producto/8 en diseno.pl). Medidas en cm, precios en MXN.</summary>
public class Product
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    /// <summary>real | conceptual</summary>
    public required string Kind { get; set; }
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public int HeightCm { get; set; }
    public string[] Materials { get; set; } = [];
    public string[] Colors { get; set; } = [];
    public string[] Styles { get; set; } = [];
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    /// <summary>JSON con atributos para requisitos, ej. {"pulgadas": 75}.</summary>
    public string AttributesJson { get; set; } = "{}";
    public string? ProviderId { get; set; }
    public string? Sku { get; set; }
    public string? Availability { get; set; }
    public string? Asset3D { get; set; }
    public string[] Images { get; set; } = [];
    public string[] Variants { get; set; } = [];

    public Provider? Provider { get; set; }
}

/// <summary>Material del catalogo con precio por m2 (material/4 en diseno.pl).</summary>
public class CatalogMaterial
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public decimal PricePerM2Min { get; set; }
    public decimal PricePerM2Max { get; set; }
    public string? Color { get; set; }
    public string? Finish { get; set; }
    public string? Texture { get; set; }
    public string? ProviderId { get; set; }
    public string? Sku { get; set; }

    public Provider? Provider { get; set; }
}
