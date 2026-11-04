namespace InteriorDesign.Domain;

/// <summary>
/// Valores permitidos (en espanol, sin acentos). Deben coincidir con
/// schemas/common.schema.json y con diseno.pl; un test lo verifica.
/// </summary>
public static class DomainValues
{
    public static readonly string[] ProjectTypes = ["comercial", "domestico"];

    public static readonly string[] SpaceTypes = ["sala", "recamara", "cocina", "oficina", "comedor"];

    public static readonly string[] Styles = ["moderno", "minimalista", "industrial", "japandi", "clasico"];

    public static readonly string[] Categories =
    [
        "sofa", "sillon", "mesa_centro", "mueble_tv", "cama", "escritorio",
        "silla_oficina", "librero", "mesa_comedor", "silla_comedor", "isla",
        "televisor", "lampara", "alfombra", "cuadro", "planta"
    ];

    public static readonly string[] BudgetCategories =
        ["muebles", "electronica", "iluminacion", "decoracion", "materiales", "instalacion"];

    public static readonly string[] MaterialTypes =
        ["madera", "marmol", "concreto", "ceramica", "pintura", "tela", "metal", "vidrio"];

    public static readonly string[] StructuralElements = ["muro", "columna", "puerta", "ventana"];

    public static readonly string[] Walls = ["norte", "sur", "este", "oeste"];

    public static readonly string[] Priorities = ["obligatorio", "importante", "preferencia", "opcional"];

    public static readonly string[] BudgetModes = ["minimizar", "ajustar", "flexible"];

    public static readonly string[] MeasurementSources = ["medidas_usuario", "planos", "fotografias"];

    public static readonly string[] RequirementTypes =
        ["presupuesto_max", "tiene", "conservar", "estilo", "color", "categoria"];

    public static readonly string[] ProductKinds = ["real", "conceptual"];

    public static readonly string[] Availabilities = ["disponible", "agotado", "sobre_pedido", "desconocida"];

    public static readonly string[] Adapters = ["json", "csv"];

    public static readonly string[] Surfaces = ["piso", "muros", "techo"];

    /// <summary>Rotacion en grados enteros, 0 a 359 (antihoraria vista desde arriba).</summary>
    public const int MaxRotation = 359;

    public static bool IsValidRotation(int rotation) => rotation is >= 0 and <= MaxRotation;
}
