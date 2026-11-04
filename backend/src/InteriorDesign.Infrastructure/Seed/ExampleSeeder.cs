using System.Text.Json;
using InteriorDesign.Domain.State;
using InteriorDesign.Infrastructure.Mapping;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Infrastructure.Seed;

/// <summary>
/// Carga los datos [EJEMPLO] de schemas/examples (catalogo de diseno.pl y disenos d1-d4)
/// solo si la BD no tiene productos. Sirve para probar en desarrollo; no son datos reales.
/// </summary>
public static class ExampleSeeder
{
    public static async Task<bool> SeedIfEmptyAsync(InteriorDesignDbContext db, string schemasDir, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return false;

        var examples = Path.Combine(schemasDir, "examples");
        var catalog = JsonSerializer.Deserialize<CatalogState>(
            await File.ReadAllTextAsync(Path.Combine(examples, "catalog.json"), ct), StateJson.Options)!;
        db.Providers.AddRange(catalog.Providers.Select(StateMapper.ToEntity));
        db.Products.AddRange(catalog.Products.Select(StateMapper.ToEntity));
        db.Materials.AddRange(catalog.Materials.Select(StateMapper.ToEntity));
        await db.SaveChangesAsync(ct);

        var repo = new DesignRepository(db);
        // d1 primero: d2 y d3 lo tienen como padre
        foreach (var file in new[] { "d1.json", "d2.json", "d3.json", "d4.json" })
        {
            var state = JsonSerializer.Deserialize<DesignState>(
                await File.ReadAllTextAsync(Path.Combine(examples, file), ct), StateJson.Options)!;
            var description = state.History.LastOrDefault()?.Description ?? "diseno de ejemplo (diseno.pl)";
            state.History = [];
            await repo.CreateAsync(state, description, "ejemplo", ct);
        }
        return true;
    }
}
