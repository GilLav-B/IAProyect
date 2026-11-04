using InteriorDesign.Api.Services;
using InteriorDesign.Domain.State;
using InteriorDesign.Infrastructure.Mapping;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalog(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/catalog").WithTags("Catalogo");

        g.MapGet("/products", async (string? category, string? style, string? kind, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            var q = db.Products.AsNoTracking();
            if (category is not null) q = q.Where(p => p.Category == category);
            if (style is not null) q = q.Where(p => p.Styles.Contains(style));
            if (kind is not null) q = q.Where(p => p.Kind == kind);
            return (await q.OrderBy(p => p.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList();
        }).WithSummary("Lista productos (filtros opcionales: category, style, kind)");

        g.MapGet("/products/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) is { } p
                ? Results.Ok(StateMapper.ToState(p))
                : Results.NotFound())
            .WithSummary("Un producto");

        g.MapPost("/products", async (ProductState product, DesignService svc, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            var errors = svc.SchemaErrors("product", product);
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = [.. errors] });
            if (await db.Products.AnyAsync(p => p.Id == product.Id, ct)) return Results.Conflict($"Ya existe el producto {product.Id}.");
            db.Products.Add(StateMapper.ToEntity(product));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/products/{product.Id}", product);
        }).WithSummary("Agrega un producto al catalogo (valida contra catalog.schema.json)");

        g.MapPut("/products/{id}", async (string id, ProductState product, DesignService svc, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (product.Id != id) return Results.BadRequest("El id del cuerpo no coincide con la ruta.");
            var errors = svc.SchemaErrors("product", product);
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = [.. errors] });
            var existing = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (existing is null) return Results.NotFound();
            db.Entry(existing).CurrentValues.SetValues(StateMapper.ToEntity(product));
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        }).WithSummary("Actualiza un producto");

        g.MapDelete("/products/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (await db.DesignObjects.AnyAsync(o => o.CatalogId == id, ct))
                return Results.Conflict("El producto esta usado en algun diseno; reemplazalo antes de borrarlo.");
            var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        }).WithSummary("Borra un producto (solo si ningun diseno lo usa)");

        g.MapGet("/products/{id}/similar", async (string id, DesignService svc, CancellationToken ct) =>
        {
            var engine = await svc.EngineAsync(ct);
            return engine.Product(id) is null ? Results.NotFound() : Results.Ok(engine.SimilarByPrice(id));
        }).WithSummary("Productos similares: misma categoria y algun estilo en comun, del mas barato al mas caro (buscar_similares/2)");

        g.MapGet("/materials", async (InteriorDesignDbContext db, CancellationToken ct) =>
            (await db.Materials.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList())
            .WithSummary("Materiales con precio por m2");

        g.MapGet("/providers", async (InteriorDesignDbContext db, CancellationToken ct) =>
            (await db.Providers.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList())
            .WithSummary("Proveedores y su adaptador");
    }
}
