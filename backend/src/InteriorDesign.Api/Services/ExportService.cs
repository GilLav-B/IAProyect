using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Services;

/// <summary>
/// Fase 7: el motor calcula listas, presupuesto y validacion; el servicio de IA en Python (ai-service) dibuja
/// el PDF, el Excel, el CSV y el plano 2D. Las imagenes que entran al reporte son las ultimas del diseno.
/// </summary>
public class ExportService(DesignService designs, InteriorDesignDbContext db, AiOptions options, IHttpClientFactory http)
{
    private static readonly JsonSerializerOptions Json = StateJson.Options;

    /// <summary>Formato -> (tipo de contenido, extension del archivo).</summary>
    public static readonly IReadOnlyDictionary<string, (string ContentType, string Extension)> Formats = new Dictionary<string, (string, string)>
    {
        ["pdf"] = ("application/pdf", "pdf"),
        ["xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        ["csv"] = ("text/csv; charset=utf-8", "csv"),
        ["plan.svg"] = ("image/svg+xml", "plano.svg"),
        ["plan.pdf"] = ("application/pdf", "plano.pdf"),
    };

    public static object Lists(DesignEngine engine, DesignState s) => new
    {
        shopping = engine.ShoppingList(s).Select(i => new
        {
            i.Rubro, i.CatalogId, i.Name, i.Kind, i.Quantity,
            unitMin = i.Unit.Min, unitMax = i.Unit.Max, totalMin = i.Total.Min, totalMax = i.Total.Max,
        }),
        materials = engine.MaterialList(s).Select(m => new
        {
            m.MaterialId, m.Name, m.Type, m2 = m.AreaM2, m2WithWaste = m.AreaWithWaste,
            priceM2Min = m.PricePerM2.Min, priceM2Max = m.PricePerM2.Max, totalMin = m.Total.Min, totalMax = m.Total.Max,
        }),
        wasteFactor = engine.Rules.MaterialWaste,
    };

    /// <summary>Todo lo que necesita el reporte, ya calculado por el motor.</summary>
    public async Task<object?> PayloadAsync(string designId, CancellationToken ct)
    {
        if (await designs.LoadAsync(designId, ct) is not { } x) return null;
        var (s, engine) = x;
        var budget = engine.Budget(s);
        var lists = JsonSerializer.SerializeToElement(Lists(engine, s), Json);
        return new
        {
            project = s.Project,
            design = s.Design,
            room = s.Room,
            objects = s.Objects.Select(o => (o, p: engine.Product(o.CatalogId))).Where(t => t.p is not null).Select(t => new
            {
                t.o.InstanceId, t.o.CatalogId, t.p!.Name, t.p.Category, t.p.Kind,
                x = t.o.Position.X, y = t.o.Position.Y, t.o.Rotation,
                widthCm = t.p.Dimensions.Width, depthCm = t.p.Dimensions.Depth, heightCm = t.p.Dimensions.Height,
                color = t.o.Config?.Color ?? t.p.Colors.FirstOrDefault(),
                material = t.o.Config?.Material ?? t.p.Materials.FirstOrDefault(),
                t.o.Locked,
            }),
            shopping = lists.GetProperty("shopping"),
            materials = lists.GetProperty("materials"),
            wasteFactor = engine.Rules.MaterialWaste,
            budget = new
            {
                total = new { min = budget.Total.Min, max = budget.Total.Max },
                cap = budget.EffectiveCap,
                byCategory = budget.ByCategory.Select(kv => new { rubro = kv.Key, min = kv.Value.Min, max = kv.Value.Max }),
            },
            report = new
            {
                valid = engine.IsValid(s),
                conflicts = engine.Conflicts(s).Select(t => t.ToString()),
                warnings = engine.Warnings(s).Select(t => t.ToString()),
            },
            images = await ImagesAsync(designId, ct),
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };
    }

    /// <summary>Ultimo render de Blender (exacto) y ultimas imagenes de IA (con su leyenda), como rutas de storage/.</summary>
    private async Task<List<object>> ImagesAsync(string designId, CancellationToken ct)
    {
        var jobs = await db.AiJobs.AsNoTracking()
            .Where(j => j.DesignId == designId && j.Status == "completado" && (j.Type == "blender" || j.Type == "imagen"))
            .OrderByDescending(j => j.CreatedAt).Take(30).ToListAsync(ct);
        var images = new List<object>();
        static string Relative(string url) => url.StartsWith("/files/") ? url["/files/".Length..] : url;

        if (jobs.FirstOrDefault(j => j.Type == "blender") is { ResultJson: { } blenderJson })
        {
            var r = JsonDocument.Parse(blenderJson).RootElement;
            foreach (var img in r.GetProperty("images").EnumerateArray())
                images.Add(new
                {
                    title = $"Render de Blender, vista {img.GetProperty("view").GetString()}",
                    path = Relative(img.GetProperty("url").GetString()!),
                    caption = "Generado directo del modelo: medidas y posiciones exactas.",
                });
        }
        foreach (var j in jobs.Where(j => j.Type == "imagen").Take(4))
        {
            var r = JsonDocument.Parse(j.ResultJson!).RootElement;
            images.Add(new { title = "Imagen con IA", path = Relative(r.GetProperty("imageUrl").GetString()!), caption = AiJobService.AiCaption });
        }
        return images;
    }

    /// <summary>Pide el archivo al servicio de IA. null si el diseno no existe.</summary>
    public async Task<byte[]?> ExportAsync(string designId, string format, CancellationToken ct)
    {
        if (await PayloadAsync(designId, ct) is not { } payload) return null;
        using var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        using var res = await client.PostAsJsonAsync($"{options.AiServiceUrl.TrimEnd('/')}/export/{format}", payload, Json, ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"El servicio de IA respondio {(int)res.StatusCode} al exportar.");
        return await res.Content.ReadAsByteArrayAsync(ct);
    }
}
