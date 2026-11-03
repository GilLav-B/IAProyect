using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Solo corre si existe la variable IAPROYECT_TEST_DB con una cadena de conexion
/// a una BD de PRUEBAS (se borra y se recrea en cada corrida). Ej.:
/// Host=localhost;Port=5432;Database=iaproyect_test;Username=postgres;Password=...
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "IAPROYECT_TEST_DB";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Sin PostgreSQL: define {Variable} para correr esta prueba.";
    }
}

public class PostgresMigrationTests
{
    private static InteriorDesignDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
        var database = new NpgsqlConnectionStringBuilder(connection).Database;
        if (database is null || !database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Por seguridad la BD de {PostgresFactAttribute.Variable} debe llevar 'test' en el nombre (se borra en cada prueba).");

        var builder = new DbContextOptionsBuilder<InteriorDesignDbContext>();
        InteriorDesignDbContext.Configure(builder, connection);
        return new InteriorDesignDbContext(builder.Options);
    }

    [PostgresFact]
    public async Task Migraciones_se_aplican_y_guardan_d1_a_d4()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();

            var (providers, products, materials) = ExampleImporter.Catalog(ExampleImporter.Load("catalog.json"));
            context.Providers.AddRange(providers);
            context.Products.AddRange(products);
            context.Materials.AddRange(materials);

            var states = new[] { "d1.json", "d2.json", "d3.json", "d4.json" }.Select(ExampleImporter.Load).ToList();
            context.Projects.Add(ExampleImporter.Project(states[0]));
            foreach (var room in states.Select(ExampleImporter.Room).DistinctBy(r => r.Id))
                context.Rooms.Add(room);
            await context.SaveChangesAsync();

            // d1 primero: d2 y d3 lo tienen como padre
            foreach (var state in states)
            {
                context.Designs.Add(ExampleImporter.Design(state));
                await context.SaveChangesAsync();
            }
        }

        await using (var context = CreateContext())
        {
            Assert.Equal(20, await context.Products.CountAsync());
            Assert.Equal(2, await context.Rooms.CountAsync());
            Assert.Equal(4, await context.Designs.CountAsync());
            Assert.Equal(17, await context.DesignObjects.CountAsync());

            var d1 = await context.Designs.Include(d => d.Objects).Include(d => d.Children).SingleAsync(d => d.Id == "d1");
            Assert.True(d1.Objects.Single(o => o.InstanceId == "tv_d1").Locked);
            Assert.Equal(["d2", "d3"], d1.Children.Select(c => c.Id).Order());

            var tvReq = await context.Requirements.SingleAsync(r => r.RoomId == "h1" && r.Type == "tiene");
            Assert.Equal("75", tvReq.ValueJson);
        }
    }

    [PostgresFact]
    public async Task La_BD_rechaza_datos_invalidos()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        // Rotacion de 45 grados: la rechaza el CHECK ck_design_objects_rotation
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO projects (id, name, type) VALUES ('p', 'P', 'domestico');" +
            "INSERT INTO rooms (id, project_id, space_type, length_cm, width_cm, height_cm) VALUES ('h', 'p', 'sala', 500, 400, 270);" +
            "INSERT INTO designs (id, project_id, room_id, name, style, budget_mode, revision, favorite, structural_confirmations) " +
            "VALUES ('d', 'p', 'h', 'D', 'moderno', 'ajustar', 0, false, ARRAY[]::text[]);" +
            "INSERT INTO products (id, name, category, kind, width_cm, depth_cm, height_cm, materials, colors, styles, price_min, price_max, images, variants) " +
            "VALUES ('x', 'X', 'sofa', 'real', 100, 80, 80, ARRAY['tela'], ARRAY['gris'], ARRAY['moderno'], 1, 2, ARRAY[]::text[], ARRAY[]::text[]);");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO design_objects (design_id, instance_id, catalog_id, type, x_cm, y_cm, z_cm, rotation, locked) " +
            "VALUES ('d', 'o', 'x', 'sofa', 0, 0, 0, 45, false);"));
        Assert.Equal("ck_design_objects_rotation", ex.ConstraintName);

        // Producto conceptual con SKU
        ex = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO products (id, name, category, kind, width_cm, depth_cm, height_cm, materials, colors, styles, price_min, price_max, sku, images, variants) " +
            "VALUES ('c', 'C', 'sillon', 'conceptual', 90, 90, 80, ARRAY['tela'], ARRAY['beige'], ARRAY['japandi'], 0, 0, 'SKU-1', ARRAY[]::text[], ARRAY[]::text[]);"));
        Assert.Equal("ck_products_conceptual", ex.ConstraintName);
    }
}
