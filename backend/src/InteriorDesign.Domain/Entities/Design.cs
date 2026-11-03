namespace InteriorDesign.Domain.Entities;

/// <summary>Una version de diseno (pestana tipo Excel). ParentId forma el arbol de versiones.</summary>
public class Design
{
    public required string Id { get; set; }
    public required string ProjectId { get; set; }
    public required string RoomId { get; set; }
    public required string Name { get; set; }
    public string? ParentId { get; set; }
    public required string Style { get; set; }
    public required string BudgetMode { get; set; }
    public int Revision { get; set; }
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Favorite { get; set; }
    public string? Comments { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? RenderUrl { get; set; }
    public decimal? InstallationMin { get; set; }
    public decimal? InstallationMax { get; set; }
    /// <summary>JSON con modo dia/noche y luces.</summary>
    public string? LightingJson { get; set; }
    /// <summary>Elementos estructurales que el usuario autorizo modificar.</summary>
    public string[] StructuralConfirmations { get; set; } = [];

    public Project? Project { get; set; }
    public Room? Room { get; set; }
    public Design? Parent { get; set; }
    public List<Design> Children { get; set; } = [];
    public List<DesignObject> Objects { get; set; } = [];
    public List<DesignMaterial> Materials { get; set; } = [];
    public List<DesignHistoryEntry> History { get; set; } = [];
}

/// <summary>
/// Objeto colocado. InstanceId es unico dentro de la version y se conserva al duplicar,
/// para que catalogo, DesignState, Three.js y Blender hablen del mismo objeto.
/// X,Y = esquina suroeste de la huella; Z = altura de la base. cm.
/// </summary>
public class DesignObject
{
    public required string DesignId { get; set; }
    public required string InstanceId { get; set; }
    public required string CatalogId { get; set; }
    public required string Type { get; set; }
    public int XCm { get; set; }
    public int YCm { get; set; }
    public int ZCm { get; set; }
    public int Rotation { get; set; }
    public double ScaleX { get; set; } = 1;
    public double ScaleY { get; set; } = 1;
    public double ScaleZ { get; set; } = 1;
    public string? Color { get; set; }
    public string? Material { get; set; }
    public bool Locked { get; set; }

    public Design? Design { get; set; }
    public Product? Product { get; set; }
}

/// <summary>Material aplicado al diseno (aplica_material/3). AreaM2 es la unica medida en m2.</summary>
public class DesignMaterial
{
    public long Id { get; set; }
    public required string DesignId { get; set; }
    public required string MaterialId { get; set; }
    public decimal AreaM2 { get; set; }
    public string? Surface { get; set; }

    public Design? Design { get; set; }
    public CatalogMaterial? Material { get; set; }
}

/// <summary>Entrada de historial. SnapshotJson guarda el DesignState completo para poder revertir.</summary>
public class DesignHistoryEntry
{
    public long Id { get; set; }
    public required string DesignId { get; set; }
    public DateTime Date { get; set; }
    public string? Author { get; set; }
    public required string Description { get; set; }
    public string[] ChangedInstanceIds { get; set; } = [];
    public int? Revision { get; set; }
    public string? SnapshotJson { get; set; }

    public Design? Design { get; set; }
}
