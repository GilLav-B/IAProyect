namespace InteriorDesign.Domain.Entities;

/// <summary>
/// Requisito del usuario por habitacion (requisito/3 en diseno.pl).
/// Segun Type se llenan unos campos u otros:
/// presupuesto_max -> Amount; tiene -> Category, Attribute, ValueJson;
/// conservar -> Element; estilo -> Style; color -> Category, Color; categoria -> Category.
/// </summary>
public class Requirement
{
    public required string RoomId { get; set; }
    public required string Id { get; set; }
    public required string Priority { get; set; }
    public required string Type { get; set; }
    public string? Description { get; set; }

    public decimal? Amount { get; set; }
    public string? Category { get; set; }
    public string? Attribute { get; set; }
    /// <summary>Valor JSON (numero o texto), ej. 75 o "queen".</summary>
    public string? ValueJson { get; set; }
    public string? Element { get; set; }
    public string? Style { get; set; }
    public string? Color { get; set; }
}
