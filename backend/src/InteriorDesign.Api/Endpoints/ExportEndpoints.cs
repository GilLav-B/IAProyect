using InteriorDesign.Api.Services;

namespace InteriorDesign.Api.Endpoints;

/// <summary>Fase 7: listas de compras y materiales, y exportaciones (PDF, Excel, CSV, plano 2D).</summary>
public static class ExportEndpoints
{
    public static void MapExports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/designs/{id}").WithTags("Exportacion");

        g.MapGet("/lists", async (string id, DesignService svc, CancellationToken ct) =>
            await svc.LoadAsync(id, ct) is { } x ? Results.Ok(ExportService.Lists(x.Engine, x.State)) : Results.NotFound())
            .WithSummary("Lista de compras (lista_compras/2) y de materiales con desperdicio (lista_materiales/2)");

        g.MapGet("/export/{format}", async (string id, string format, DesignService svc, ExportService exports, CancellationToken ct) =>
        {
            if (!ExportService.Formats.TryGetValue(format, out var f))
                return Results.BadRequest($"Formato no soportado. Usa: {string.Join(", ", ExportService.Formats.Keys)}.");
            var state = await svc.LoadAsync(id, ct);
            if (state is null) return Results.NotFound();
            try
            {
                var bytes = await exports.ExportAsync(id, format, ct);
                return bytes is null ? Results.NotFound() : Results.File(bytes, f.ContentType, $"{id}_{f.Extension}");
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                return Results.Problem($"No se pudo generar el archivo: el servicio de IA (Python) no respondio. {ex.Message}", statusCode: 503);
            }
        }).WithSummary("Descarga: pdf (reporte), xlsx, csv, plan.svg o plan.pdf. Lo genera el servicio de IA en Python.");
    }
}
