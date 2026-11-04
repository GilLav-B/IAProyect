using System.Text.Json;
using System.Text.Json.Nodes;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;
using InteriorDesign.Infrastructure.Mapping;
using InteriorDesign.Infrastructure.Validation;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Convierte los JSON de schemas/examples a entidades de BD con el mismo StateMapper que usa la API.
/// Sirve para comprobar que cada campo del DesignState tiene donde guardarse.
/// </summary>
public static class ExampleImporter
{
    public static string SchemasDir { get; } = SchemaValidator.FindSchemasDir(AppContext.BaseDirectory)
        ?? throw new DirectoryNotFoundException("No se encontro la carpeta schemas/ del repositorio.");

    public static JsonNode Load(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(SchemasDir, "examples", file)))!;

    private static T As<T>(JsonNode node) => node.Deserialize<T>(StateJson.Options)!;

    public static (List<Provider> Providers, List<Product> Products, List<CatalogMaterial> Materials) Catalog(JsonNode c)
    {
        var catalog = As<CatalogState>(c);
        return (catalog.Providers.Select(StateMapper.ToEntity).ToList(),
                catalog.Products.Select(StateMapper.ToEntity).ToList(),
                catalog.Materials.Select(StateMapper.ToEntity).ToList());
    }

    public static Project Project(JsonNode s)
    {
        var p = As<DesignState>(s).Project;
        return new Project { Id = p.Id, Name = p.Name, Type = p.Type };
    }

    public static Room Room(JsonNode s)
    {
        var state = As<DesignState>(s);
        return StateMapper.ToEntity(state.Room, state.Project.Id, state.Requirements);
    }

    public static Design Design(JsonNode s)
    {
        var state = As<DesignState>(s);
        var design = StateMapper.ToEntity(state);
        design.History = state.History.Select(h => new DesignHistoryEntry
        {
            DesignId = state.Design.Id, Date = h.Date, Author = h.Author, Description = h.Description,
            ChangedInstanceIds = [.. h.ChangedInstanceIds], Revision = h.Revision
        }).ToList();
        return design;
    }
}
