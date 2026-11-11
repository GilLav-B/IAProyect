namespace InteriorDesign.Engine;

/// <summary>
/// Reglas fijas tomadas de diseno.pl (secciones 2, 4, 6 y 7).
/// Los parametros marcados [SUPUESTO] en diseno.pl se pueden ajustar aqui.
/// </summary>
public class Rules
{
    public static Rules Default { get; } = new();

    /// <summary>[SUPUESTO] cm libres frente a ventana.</summary>
    public int WindowAccessCm { get; init; } = 40;
    /// <summary>[SUPUESTO] fraccion maxima del piso ocupada por muebles.</summary>
    public decimal MaxOccupancy { get; init; } = 0.6m;
    /// <summary>[SUPUESTO] margen extra en modo de presupuesto flexible.</summary>
    public decimal FlexibleMargin { get; init; } = 0.10m;
    /// <summary>parametro(desperdicio_material, 0.10). Confirmado por el usuario (2026-11-11): 10%.</summary>
    public decimal MaterialWaste { get; init; } = 0.10m;

    public IReadOnlyDictionary<string, string> CategoryBudget { get; init; } = new Dictionary<string, string>
    {
        ["sofa"] = "muebles", ["sillon"] = "muebles", ["mesa_centro"] = "muebles", ["mueble_tv"] = "muebles",
        ["cama"] = "muebles", ["escritorio"] = "muebles", ["silla_oficina"] = "muebles", ["librero"] = "muebles",
        ["mesa_comedor"] = "muebles", ["silla_comedor"] = "muebles", ["isla"] = "muebles",
        ["televisor"] = "electronica", ["lampara"] = "iluminacion",
        ["alfombra"] = "decoracion", ["cuadro"] = "decoracion", ["planta"] = "decoracion",
    };

    /// <summary>Rubros en el orden de diseno.pl.</summary>
    public IReadOnlyList<string> BudgetCategories { get; init; } =
        ["muebles", "electronica", "iluminacion", "decoracion", "materiales", "instalacion"];

    /// <summary>Objetos que no ocupan piso por si solos.</summary>
    public IReadOnlySet<string> NoFloorArea { get; init; } = new HashSet<string> { "alfombra", "televisor", "cuadro" };

    public IReadOnlyDictionary<string, string[]> SpaceCategories { get; init; } = new Dictionary<string, string[]>
    {
        ["sala"] = ["sofa", "sillon", "mesa_centro", "mueble_tv", "televisor", "alfombra", "lampara", "cuadro", "planta", "librero"],
        ["recamara"] = ["cama", "mueble_tv", "televisor", "alfombra", "lampara", "cuadro", "planta"],
        ["cocina"] = ["isla", "lampara", "cuadro", "planta"],
        ["oficina"] = ["escritorio", "silla_oficina", "librero", "lampara", "planta", "cuadro", "alfombra"],
        ["comedor"] = ["mesa_comedor", "silla_comedor", "lampara", "cuadro", "planta", "alfombra"],
    };

    /// <summary>Muebles basicos que deberia tener cada espacio.</summary>
    public IReadOnlyDictionary<string, string[]> Basics { get; init; } = new Dictionary<string, string[]>
    {
        ["sala"] = ["sofa", "mesa_centro", "mueble_tv", "televisor"],
        ["recamara"] = ["cama"],
        ["cocina"] = ["isla"],
        ["oficina"] = ["escritorio", "silla_oficina"],
        ["comedor"] = ["mesa_comedor", "silla_comedor"],
    };

    /// <summary>Pares de estilos afines (simetricos).</summary>
    public IReadOnlyList<(string, string)> RelatedStyles { get; init; } =
        [("japandi", "minimalista"), ("moderno", "minimalista"), ("moderno", "industrial")];

    public IReadOnlySet<string> StructuralElements { get; init; } = new HashSet<string> { "muro", "columna", "puerta", "ventana" };

    /// <summary>Tipos de requisito que el motor evalua (presupuesto y conservar van aparte).</summary>
    public IReadOnlySet<string> EvaluableRequirements { get; init; } = new HashSet<string> { "tiene", "estilo", "color", "categoria" };

    public bool StylesCompatible(string a, string b) =>
        a == b || RelatedStyles.Any(p => (p.Item1 == a && p.Item2 == b) || (p.Item1 == b && p.Item2 == a));

    public bool CategoryAllowed(string space, string category) =>
        SpaceCategories.TryGetValue(space, out var list) && list.Contains(category);
}
