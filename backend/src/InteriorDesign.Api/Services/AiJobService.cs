using System.Net.Http.Json;
using System.Text.Json;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Services;

/// <summary>Configuracion de la integracion con n8n y con el servicio de IA en Python (variables del .env).</summary>
public record AiOptions(
    string N8nUrl,
    string CallbackBaseUrl,
    string? CallbackSecret,
    string StorageDir,
    bool DispatchEnabled,
    string AiServiceUrl = "http://127.0.0.1:8100")
{
    public const string ProposalsWebhook = "iaproyect-propuestas";
    public const string ImageWebhook = "iaproyect-imagen";
    public const string BlenderWebhook = "iaproyect-blender";
}

/// <summary>Calidad del render de Blender: muestras y resolucion (en CPU "alta" tarda ~3-4 min por vista).</summary>
public record BlenderQuality(int Samples, int Width, int Height)
{
    public static BlenderQuality From(string? name) => name switch
    {
        "alta" => new(64, 1280, 800),
        _ => new(32, 800, 500),
    };
}

/// <summary>
/// Trabajos asincronos de IA: la API los crea, n8n los orquesta, el servicio de IA en Python los procesa
/// (prompts, OpenAI, Blender) y regresa el resultado, y el motor valida lo que propone la IA antes de tocar cualquier diseno.
/// </summary>
public class AiJobService(
    InteriorDesignDbContext db,
    DesignService designs,
    AiOptions options,
    IHttpClientFactory http,
    ILogger<AiJobService> logger)
{
    private static readonly JsonSerializerOptions Json = StateJson.Options;
    public const string AiCaption = "Imagen de referencia generada por IA. No representa medidas ni productos exactos.";

    public static bool IsTerminal(string status) => status is "completado" or "fallido";

    private async Task<AiJob> NewJobAsync(string type, string designId, object input, CancellationToken ct)
    {
        var job = new AiJob
        {
            Id = $"job_{Guid.NewGuid():N}"[..16],
            Type = type,
            DesignId = designId,
            Status = "pendiente",
            Progress = 0,
            Message = "En cola",
            InputJson = JsonSerializer.Serialize(input, Json),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.AiJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    /// <summary>Manda el trabajo al webhook de n8n. Si n8n no responde, el trabajo queda como fallido.</summary>
    private async Task DispatchAsync(AiJob job, string webhook, object payload, CancellationToken ct)
    {
        if (!options.DispatchEnabled) return;
        try
        {
            using var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var res = await client.PostAsJsonAsync($"{options.N8nUrl.TrimEnd('/')}/webhook/{webhook}", payload, Json, ct);
            if (!res.IsSuccessStatusCode)
                await FailAsync(job.Id, $"n8n respondio {(int)res.StatusCode}. ¿Esta publicado el flujo '{webhook}'?", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el trabajo {Job} a n8n", job.Id);
            await FailAsync(job.Id, $"No se pudo conectar con n8n en {options.N8nUrl}. ¿Esta corriendo?", ct);
        }
    }

    private string CallbackUrl(string jobId) => $"{options.CallbackBaseUrl.TrimEnd('/')}/api/ai/jobs/{jobId}";

    private async Task SaveUploadAsync(string jobId, byte[] image, string contentType, CancellationToken ct)
    {
        var ext = contentType switch { "image/png" => "png", "image/webp" => "webp", _ => "jpg" };
        var dir = Path.Combine(options.StorageDir, "uploads");
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{jobId}.{ext}"), image, ct);
    }

    // ------------------------------------------------------------------ crear trabajos

    /// <summary>Copiar un diseno de referencia (imagen de internet) en el cuarto del usuario.</summary>
    public async Task<AiJob> StartReferenceAsync(DesignState s, DesignEngine engine, byte[] image, string contentType, string? note, CancellationToken ct)
    {
        var job = await NewJobAsync("referencia", s.Design.Id, new { note, imageType = contentType, imageBytes = image.Length }, ct);
        await SaveUploadAsync(job.Id, image, contentType, ct);
        await DispatchAsync(job, AiOptions.ProposalsWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            kind = "referencia",
            context = AiContext.DesignContext(s, engine),
            note,
            imageDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(image)}",
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    /// <summary>Instruccion del chat del editor ("hazlo mas minimalista", "cambia el sofa por uno mas chico").</summary>
    public async Task<AiJob> StartChatAsync(DesignState s, DesignEngine engine, string message, CancellationToken ct)
    {
        var job = await NewJobAsync("chat", s.Design.Id, new { message }, ct);
        await DispatchAsync(job, AiOptions.ProposalsWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            kind = "chat",
            context = AiContext.DesignContext(s, engine),
            message,
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    /// <summary>
    /// "Quitar" todo lo que no esta bloqueado: el motor decide (lo obligatorio, como la TV de 75", se queda).
    /// Es el punto de partida de "Disenar con IA".
    /// </summary>
    private static (DesignState Cleared, List<AiProposalOutcome> Outcomes) ClearForDesign(DesignState s, DesignEngine engine) =>
        AiPlan.Run(s, s.Objects.Where(o => !o.Locked).Select(o => new AiProposal("quitar", InstanceId: o.InstanceId)).ToList(), engine);

    /// <summary>"Disenar con IA": la IA arma un diseno propio para el cuarto (version nueva; la original no se toca).</summary>
    public async Task<AiJob> StartDesignAsync(DesignState s, DesignEngine engine, string? style, string? note, CancellationToken ct)
    {
        var target = style is not null && Domain.DomainValues.Styles.Contains(style) ? style : s.Design.Style;
        var job = await NewJobAsync("diseno", s.Design.Id, new { style = target, note }, ct);
        var (cleared, _) = ClearForDesign(s, engine);
        cleared.Design.Style = target;
        await DispatchAsync(job, AiOptions.ProposalsWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            kind = "diseno",
            context = AiContext.DesignContext(cleared, engine),
            style = target,
            note,
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    /// <summary>Imagen libre de como quedaria la habitacion, solo con la descripcion del diseno.</summary>
    public async Task<AiJob> StartImageAsync(DesignState s, DesignEngine engine, string? note, CancellationToken ct)
    {
        var job = await NewJobAsync("imagen", s.Design.Id, new { mode = "libre", note }, ct);
        await DispatchAsync(job, AiOptions.ImageWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            mode = "libre",
            state = s,
            products = AiContext.UsedProducts(s, engine),
            note,
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    /// <summary>
    /// Render realista: la IA convierte una captura del visor 3D en foto, respetando el acomodo.
    /// visible = muebles que el visor calculo que salen en la captura (la IA no puede agregar otros).
    /// </summary>
    /// <param name="batch">Lote de vistas (las 4 burbujas del visor); view = numero de vista dentro del lote.</param>
    public async Task<AiJob> StartRenderAsync(DesignState s, DesignEngine engine, byte[] capture, string contentType, string? note, string? batch, int? view,
        IReadOnlyList<VisibleItem>? visible, CancellationToken ct)
    {
        var job = await NewJobAsync("imagen", s.Design.Id, new { mode = "render", note, batch, view, visible }, ct);
        await SaveUploadAsync(job.Id, capture, contentType, ct);
        await DispatchAsync(job, AiOptions.ImageWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            mode = "render",
            state = s,
            products = AiContext.UsedProducts(s, engine),
            visible,
            note,
            imageDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(capture)}",
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    /// <summary>Render exacto en Blender (Cycles) del DesignState validado. La IA no interviene en la geometria.</summary>
    public async Task<AiJob> StartBlenderAsync(DesignState s, DesignEngine engine, string views, string quality, CancellationToken ct)
    {
        var q = BlenderQuality.From(quality);
        var job = await NewJobAsync("blender", s.Design.Id, new { views, quality, q.Samples, q.Width, q.Height }, ct);
        await DispatchAsync(job, AiOptions.BlenderWebhook, new
        {
            jobId = job.Id,
            callbackUrl = CallbackUrl(job.Id),
            state = s,
            products = AiContext.UsedProducts(s, engine),
            materials = engine.Catalog.Materials,
            views,
            samples = q.Samples,
            width = q.Width,
            height = q.Height,
        }, ct);
        return (await GetAsync(job.Id, ct))!;
    }

    // ------------------------------------------------------------------ avance y resultado (los manda n8n / ai-service)

    public Task<AiJob?> GetAsync(string id, CancellationToken ct) =>
        db.AiJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct);

    public async Task<bool> ProgressAsync(string id, int progress, string? message, CancellationToken ct)
    {
        var job = await db.AiJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
        if (job is null || IsTerminal(job.Status)) return false;
        job.Status = "en_proceso";
        job.Progress = Math.Clamp(progress, 0, 99);
        job.Message = message ?? job.Message;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task FailAsync(string id, string error, CancellationToken ct)
    {
        var job = await db.AiJobs.SingleOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return;
        job.Status = "fallido";
        job.Error = error;
        job.Message = "Fallo";
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task CompleteAsync(string id, object result, string message, string? resultDesignId, CancellationToken ct)
    {
        var job = await db.AiJobs.SingleAsync(j => j.Id == id, ct);
        job.Status = "completado";
        job.Progress = 100;
        job.Message = message;
        job.ResultJson = JsonSerializer.Serialize(result, Json);
        job.ResultDesignId = resultDesignId;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Respuesta de la IA en el formato de interior-designer.md.</summary>
    public record AiResponse(string? Summary, string? Style, List<AiProposal>? Proposals);

    /// <summary>Resultado de Blender que regresa ai-service (rutas relativas a storage/).</summary>
    private record BlenderImage(string View, string Path, double Seconds);
    private record BlenderResult(string Blend, List<BlenderImage> Images, double Seconds);

    public static object Outcomes(IEnumerable<AiProposalOutcome> outcomes, DesignEngine engine) => outcomes.Select(o => new
    {
        o.Proposal.Action,
        o.Proposal.InstanceId,
        o.Proposal.CatalogId,
        productName = o.Proposal.CatalogId is null ? null : engine.Product(o.Proposal.CatalogId)?.Name,
        o.Proposal.Reason,
        o.Applied,
        o.Verdict,
        reasons = o.Reasons.Select(ReasonDto.From),
        validated = o.Validated?.ToTerm().ToString(),
    }).ToList();

    /// <summary>Ruta de storage/ como URL publica (/files/...). Solo acepta rutas relativas sin "..".</summary>
    private static string FileUrl(string relative)
    {
        var clean = relative.Replace('\\', '/').TrimStart('/');
        if (clean.Split('/').Any(part => part is ".." or "")) throw new InvalidOperationException("Ruta de archivo invalida.");
        return $"/files/{clean}";
    }

    /// <summary>Procesa lo que regresa n8n. Lo que propone la IA SIEMPRE pasa por el motor.</summary>
    public async Task ResultAsync(string id, bool ok, string? error, JsonElement? data, CancellationToken ct)
    {
        var job = await GetAsync(id, ct) ?? throw new KeyNotFoundException(id);
        if (IsTerminal(job.Status)) return;
        if (!ok || data is null)
        {
            await FailAsync(id, error ?? "La IA no regreso resultado.", ct);
            return;
        }

        try
        {
            switch (job.Type)
            {
                case "imagen":
                {
                    var b64 = data.Value.GetProperty("b64").GetString()!;
                    var dir = Path.Combine(options.StorageDir, "renders");
                    Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(Path.Combine(dir, $"{id}.png"), Convert.FromBase64String(b64), ct);
                    await CompleteAsync(id, new { imageUrl = $"/files/renders/{id}.png", caption = AiCaption }, "Imagen lista", null, ct);
                    break;
                }
                case "blender":
                {
                    var result = data.Value.Deserialize<BlenderResult>(Json)!;
                    await CompleteAsync(id, new
                    {
                        images = result.Images.Select(i => new { i.View, url = FileUrl(i.Path), i.Seconds }),
                        blendUrl = FileUrl(result.Blend),
                        result.Seconds,
                        caption = "Render de Blender generado directo del DesignState (medidas y posiciones exactas).",
                    }, "Render listo", null, ct);
                    break;
                }
                case "chat":
                {
                    var response = data.Value.Deserialize<AiResponse>(Json)!;
                    if (await designs.LoadAsync(job.DesignId, ct) is not { } x) throw new InvalidOperationException("El diseno ya no existe.");
                    // Solo se SIMULA: el usuario decide si aplica
                    var (_, outcomes) = AiPlan.Run(x.State, response.Proposals ?? [], x.Engine);
                    await CompleteAsync(id, new
                    {
                        summary = response.Summary,
                        style = response.Style,
                        proposals = response.Proposals,
                        outcomes = Outcomes(outcomes, x.Engine),
                        applicable = outcomes.Count(o => o.Applied),
                    }, "Propuesta lista", null, ct);
                    break;
                }
                case "referencia":
                case "diseno":
                {
                    var response = data.Value.Deserialize<AiResponse>(Json)!;
                    if (await designs.LoadAsync(job.DesignId, ct) is not { } x) throw new InvalidOperationException("El diseno ya no existe.");
                    var input = JsonSerializer.Deserialize<JsonElement>(job.InputJson ?? "{}");
                    var isDesign = job.Type == "diseno";
                    var style = isDesign && input.TryGetProperty("style", out var st) ? st.GetString() : response.Style;
                    // Version nueva: la original no se toca
                    var newId = DesignService.NewId("d");
                    var name = isDesign ? $"Diseno IA ({style})" : "Basado en referencia";
                    var copy = await designs.Repo.DuplicateAsync(job.DesignId, newId, name, "IA", ct);
                    var start = isDesign ? ClearForDesign(copy!, x.Engine).Cleared : copy!;
                    var (final, outcomes) = AiPlan.Run(start, response.Proposals ?? [], x.Engine);
                    if (style is not null && Domain.DomainValues.Styles.Contains(style)) final.Design.Style = style;
                    var history = isDesign ? $"IA: diseno propio estilo {style}" : "IA: copia del diseno de referencia";
                    await designs.Repo.SaveAsync(final, history, ChangedIds(outcomes), "IA", ct);
                    await CompleteAsync(id, new
                    {
                        summary = response.Summary,
                        style,
                        newDesignId = newId,
                        outcomes = Outcomes(outcomes, x.Engine),
                        applied = outcomes.Count(o => o.Applied),
                    }, "Diseno creado", newId, ct);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al procesar el resultado del trabajo {Job}", id);
            await FailAsync(id, $"No se pudo procesar la respuesta: {ex.Message}", ct);
        }
    }

    private static IEnumerable<string> ChangedIds(IEnumerable<AiProposalOutcome> outcomes) =>
        outcomes.Where(o => o.Applied).Select(o => o.Validated switch
        {
            AddProposal a => a.InstanceId,
            MoveProposal m => m.InstanceId,
            ReplaceProposal r => r.InstanceId,
            RemoveProposal q => q.InstanceId,
            _ => ""
        }).Where(s => s.Length > 0);

    /// <summary>Aplica las propuestas del chat que el motor acepte (se revalidan contra el diseno actual).</summary>
    public async Task<(DesignState State, List<AiProposalOutcome> Outcomes, DesignEngine Engine)?> ApplyChatAsync(AiJob job, CancellationToken ct)
    {
        if (job.Type != "chat" || job.ResultJson is null) return null;
        var response = JsonSerializer.Deserialize<AiResponse>(job.ResultJson, Json)!;
        if (await designs.LoadAsync(job.DesignId, ct) is not { } x) return null;
        var (final, outcomes) = AiPlan.Run(x.State, response.Proposals ?? [], x.Engine);
        var applied = outcomes.Where(o => o.Applied).ToList();
        if (applied.Count == 0) return (x.State, outcomes, x.Engine);
        var input = JsonSerializer.Deserialize<JsonElement>(job.InputJson ?? "{}");
        var message = input.TryGetProperty("message", out var m) ? m.GetString() : "";
        var saved = await designs.Repo.SaveAsync(final, $"IA (chat): {message}", ChangedIds(outcomes), "IA", ct);
        return (saved, outcomes, x.Engine);
    }
}
