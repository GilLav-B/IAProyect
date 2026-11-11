using System.Text.Json.Nodes;
using InteriorDesign.Domain;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Fase 1: modelo de BD (EF Core + PostgreSQL). Estas pruebas no necesitan PostgreSQL.
/// Las que si lo necesitan estan en PostgresMigrationTests.
/// </summary>
public class DataModelTests
{
    private static readonly string[] Examples = ["d1.json", "d2.json", "d3.json", "d4.json"];

    private static InteriorDesignDbContext CreateContext()
    {
        // Solo se construye el modelo; no se abre ninguna conexion.
        var builder = new DbContextOptionsBuilder<InteriorDesignDbContext>();
        InteriorDesignDbContext.Configure(builder, "Host=localhost;Database=sin_conexion");
        return new InteriorDesignDbContext(builder.Options);
    }

    public static TheoryData<string, string[]> EnumsDelEsquema => new()
    {
        { "projectType", DomainValues.ProjectTypes },
        { "spaceType", DomainValues.SpaceTypes },
        { "style", DomainValues.Styles },
        { "category", DomainValues.Categories },
        { "budgetCategory", DomainValues.BudgetCategories },
        { "materialType", DomainValues.MaterialTypes },
        { "structuralElement", DomainValues.StructuralElements },
        { "wall", DomainValues.Walls },
        { "priority", DomainValues.Priorities },
        { "budgetMode", DomainValues.BudgetModes },
        { "measurementSource", DomainValues.MeasurementSources },
    };

    [Theory]
    [MemberData(nameof(EnumsDelEsquema))]
    public void DomainValues_coinciden_con_common_schema(string definition, string[] values)
    {
        var common = JsonNode.Parse(File.ReadAllText(Path.Combine(ExampleImporter.SchemasDir, "common.schema.json")))!;
        var schemaValues = common["$defs"]![definition]!["enum"]!.AsArray().Select(v => v!.GetValue<string>());
        Assert.Equal(schemaValues.Order(), values.Order());
    }

    [Fact]
    public void Modelo_EF_no_tiene_cambios_sin_migracion()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges(),
            "El modelo cambio y falta una migracion: dotnet ef migrations add <Nombre>");
    }

    [Fact]
    public void Modelo_EF_tiene_todas_las_tablas()
    {
        using var context = CreateContext();
        var tables = context.Model.GetEntityTypes().Select(e => e.GetTableName()).Order();
        Assert.Equal(
            new[]
            {
                "ai_jobs", "design_history", "design_materials", "design_objects", "designs", "materials",
                "products", "projects", "providers", "requirements", "room_columns",
                "room_doors", "room_windows", "rooms"
            },
            tables);
    }

    [Fact]
    public void DesignObject_usa_designId_e_instanceId_como_llave()
    {
        using var context = CreateContext();
        var key = context.Model.FindEntityType(typeof(Domain.Entities.DesignObject))!.FindPrimaryKey()!;
        Assert.Equal(["DesignId", "InstanceId"], key.Properties.Select(p => p.Name));
    }

    [Theory]
    [InlineData("d1.json", "h1", 5, 6)]
    [InlineData("d2.json", "h1", 5, 6)]
    [InlineData("d3.json", "h1", 5, 6)]
    [InlineData("d4.json", "h2", 2, 3)]
    public void Ejemplos_se_convierten_a_entidades(string file, string roomId, int objects, int requirements)
    {
        var state = ExampleImporter.Load(file);
        var room = ExampleImporter.Room(state);
        var design = ExampleImporter.Design(state);

        Assert.Equal(roomId, room.Id);
        Assert.Equal(roomId, design.RoomId);
        Assert.Equal(objects, design.Objects.Count);
        Assert.Equal(requirements, room.Requirements.Count);
        Assert.All(design.Objects, o => Assert.Equal(design.Id, o.DesignId));
    }

    [Fact]
    public void Ejemplos_solo_usan_productos_del_catalogo()
    {
        var (_, products, materials) = ExampleImporter.Catalog(ExampleImporter.Load("catalog.json"));
        var productIds = products.Select(p => p.Id).ToHashSet();
        var productCategory = products.ToDictionary(p => p.Id, p => p.Category);
        var materialIds = materials.Select(m => m.Id).ToHashSet();

        foreach (var file in Examples)
        {
            var design = ExampleImporter.Design(ExampleImporter.Load(file));
            Assert.All(design.Objects, o =>
            {
                Assert.Contains(o.CatalogId, productIds);
                Assert.Equal(productCategory[o.CatalogId], o.Type);
            });
            Assert.All(design.Materials, m => Assert.Contains(m.MaterialId, materialIds));
        }
    }

    [Fact]
    public void D1_conserva_el_bloqueo_de_la_tv_y_d2_d3_apuntan_a_d1()
    {
        var d1 = ExampleImporter.Design(ExampleImporter.Load("d1.json"));
        var d2 = ExampleImporter.Design(ExampleImporter.Load("d2.json"));
        var d3 = ExampleImporter.Design(ExampleImporter.Load("d3.json"));

        Assert.True(d1.Objects.Single(o => o.InstanceId == "tv_d1").Locked);
        Assert.Single(d1.Objects, o => o.Locked);
        Assert.Equal("d1", d2.ParentId);
        Assert.Equal("d1", d3.ParentId);
        Assert.Null(d1.ParentId);
    }
}
