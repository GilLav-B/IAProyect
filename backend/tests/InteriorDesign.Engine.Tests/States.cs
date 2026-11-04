using System.Text.Json;
using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine.Tests;

/// <summary>Carga los ejemplos de schemas/examples como DesignState / CatalogState.</summary>
public static class States
{
    public static DesignState Load(string file) =>
        JsonSerializer.Deserialize<DesignState>(File.ReadAllText(Path.Combine(ExampleImporter.SchemasDir, "examples", file)), StateJson.Options)!;

    public static CatalogState Catalog() =>
        JsonSerializer.Deserialize<CatalogState>(File.ReadAllText(Path.Combine(ExampleImporter.SchemasDir, "examples", "catalog.json")), StateJson.Options)!;

    public static DesignState Clone(DesignState s) =>
        JsonSerializer.Deserialize<DesignState>(JsonSerializer.Serialize(s, StateJson.Options), StateJson.Options)!;

    public static DesignState D1 => Load("d1.json");
    public static DesignState D2 => Load("d2.json");
    public static DesignState D3 => Load("d3.json");
    public static DesignState D4 => Load("d4.json");
}
