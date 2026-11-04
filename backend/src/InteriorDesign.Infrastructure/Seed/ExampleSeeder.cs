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

    /// <summary>
    /// Sincroniza el catalogo de ejemplo (catalog.json de diseno.pl + catalog-extra.json ampliado) con la BD:
    /// agrega los productos que falten y llena el modelo 3D (asset3D) que aun no tengan.
    /// No borra ni cambia nada mas (ni precios ni productos editados a mano).
    /// </summary>
    public static async Task<(int Added, int Assets)> SyncExampleCatalogAsync(InteriorDesignDbContext db, string schemasDir, CancellationToken ct = default)
    {
        var products = new List<ProductState>();
        foreach (var file in new[] { "catalog.json", "catalog-extra.json" })
        {
            var path = Path.Combine(schemasDir, "examples", file);
            if (!File.Exists(path)) continue;
            products.AddRange(JsonSerializer.Deserialize<CatalogState>(await File.ReadAllTextAsync(path, ct), StateJson.Options)!.Products);
        }

        var existing = await db.Products.Select(p => p.Id).ToHashSetAsync(ct);
        var missing = products.Where(p => !existing.Contains(p.Id)).ToList();
        db.Products.AddRange(missing.Select(StateMapper.ToEntity));

        var assets = products.Where(p => p.Asset3D is not null).ToDictionary(p => p.Id, p => p.Asset3D!);
        var withoutAsset = await db.Products.Where(p => p.Asset3D == null && assets.Keys.Contains(p.Id)).ToListAsync(ct);
        foreach (var p in withoutAsset) p.Asset3D = assets[p.Id];

        await db.SaveChangesAsync(ct);
        return (missing.Count, withoutAsset.Count);
    }
}
