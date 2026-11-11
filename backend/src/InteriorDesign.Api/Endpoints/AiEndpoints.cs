using System.Text.Json;
using InteriorDesign.Api.Services;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Endpoints;

public record ChatRequest(string Message);

public record ImageRequest(string? Note = null);

/// <summary>"Disenar con IA": estilo destino (opcional) y nota.</summary>
public record DesignRequest(string? Style = null, string? Note = null);

/// <summary>Render de Blender: vistas (se, so, ne, no o all) y calidad (rapida o alta).</summary>
public record BlenderRequest(string? Views = null, string? Quality = null);

/// <summary>n8n -> API: avance de un trabajo.</summary>
public record JobProgressRequest(int Progress, string? Message);

/// <summary>n8n -> API: resultado. data = JSON de la IA (propuestas) o { b64 } para imagenes.</summary>
public record JobResultRequest(bool Ok, string? Error, JsonElement? Data);

/// <param name="Mode">imagen: "render" (desde captura 3D) o null (imagen libre). Batch/View: lote de vistas del visor.</param>
public record JobDto(
    string Id, string Type, string DesignId, string? ResultDesignId, string Status, int Progress,
    string? Message, JsonElement? Result, string? Error, DateTime CreatedAt, DateTime UpdatedAt,
    string? Mode = null, string? Batch = null, int? View = null)
{
    public static JobDto From(AiJob j)
    {
        string? mode = null, batch = null;
        int? view = null;
        if (j.InputJson is not null)
        {
            var input = JsonDocument.Parse(j.InputJson).RootElement;
            if (input.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String) mode = m.GetString();
            if (input.TryGetProperty("batch", out var b) && b.ValueKind == JsonValueKind.String) batch = b.GetString();
            if (input.TryGetProperty("view", out var v) && v.ValueKind == JsonValueKind.Number) view = v.GetInt32();
        }
        return new(
            j.Id, j.Type, j.DesignId, j.ResultDesignId, j.Status, j.Progress, j.Message,
            j.ResultJson is null ? null : JsonDocument.Parse(j.ResultJson).RootElement.Clone(),
            j.Error, DateTime.SpecifyKind(j.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(j.UpdatedAt, DateTimeKind.Utc),
            mode, batch, view);
    }
}

public static class AiEndpoints
{
    private static readonly HashSet<string> ImageTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageBytes = 8 * 1024 * 1024;

    public static void MapAi(this IEndpointRouteBuilder app)
    {
        var designs = app.MapGroup("/api/designs/{id}/ai").WithTags("IA");

        designs.MapPost("/reference", async (string id, HttpRequest request, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Manda la imagen como multipart/form-data (campo 'image').");
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("image");
            if (file is null || file.Length == 0) return Results.BadRequest("Falta la imagen de referencia (campo 'image').");
            if (!ImageTypes.Contains(file.ContentType)) return Results.BadRequest("La imagen debe ser JPG, PNG o WEBP.");
            if (file.Length > MaxImageBytes) return Results.BadRequest("La imagen no debe pasar de 8 MB.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var job = await jobs.StartReferenceAsync(x.State, x.Engine, ms.ToArray(), file.ContentType, form["note"].ToString(), ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithSummary("Copiar un diseno de referencia (imagen de internet) en este cuarto. Crea una version nueva al terminar.")
          .DisableAntiforgery();

        designs.MapPost("/chat", async (string id, ChatRequest req, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Message)) return Results.BadRequest("Escribe una instruccion.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var job = await jobs.StartChatAsync(x.State, x.Engine, req.Message.Trim(), ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithSummary("Instruccion en texto para la IA. Responde con propuestas validadas por el motor; no aplica nada sola.");

        designs.MapPost("/image", async (string id, ImageRequest req, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (x.State.Objects.Count == 0) return Results.BadRequest("Agrega muebles antes de generar la imagen.");
            var job = await jobs.StartImageAsync(x.State, x.Engine, req.Note, ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithSummary("Imagen referencial de como quedaria la habitacion (ayuda visual, no exacta).");

        designs.MapPost("/render", async (string id, HttpRequest request, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Manda la captura como multipart/form-data (campo 'image').");
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("image");
            if (file is null || file.Length == 0) return Results.BadRequest("Falta la captura del visor 3D (campo 'image').");
            if (!ImageTypes.Contains(file.ContentType)) return Results.BadRequest("La captura debe ser JPG, PNG o WEBP.");
            if (file.Length > MaxImageBytes) return Results.BadRequest("La captura no debe pasar de 8 MB.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (x.State.Objects.Count == 0) return Results.BadRequest("Agrega muebles antes de generar el render.");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var batch = form["batch"].ToString() is { Length: > 0 and <= 40 } b ? b : null;
            int? view = int.TryParse(form["view"], out var v) && v is >= 0 and < 8 ? v : null;
            // Muebles que el visor calculo que salen en la captura (para que la IA no invente ni acomode distinto)
            List<VisibleItem>? visible = null;
            if (form["visible"].ToString() is { Length: > 0 } rawVisible)
            {
                try { visible = JsonSerializer.Deserialize<List<VisibleItem>>(rawVisible, StateJson.Options); }
                catch (JsonException) { return Results.BadRequest("El campo 'visible' no es JSON valido."); }
            }
            var job = await jobs.StartRenderAsync(x.State, x.Engine, ms.ToArray(), file.ContentType, form["note"].ToString(), batch, view, visible, ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithSummary("Render realista con IA a partir de una captura del visor 3D (respeta el acomodo; ayuda visual).")
          .DisableAntiforgery();

        designs.MapPost("/design", async (string id, DesignRequest req, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            if (req.Style is not null && !Domain.DomainValues.Styles.Contains(req.Style))
                return Results.BadRequest($"Estilo desconocido: {req.Style}.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var job = await jobs.StartDesignAsync(x.State, x.Engine, req.Style, req.Note, ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithSummary("La IA arma un diseno propio para el cuarto (version nueva). El motor valida cada mueble.");

        app.MapPost("/api/designs/{id}/blender", async (string id, BlenderRequest req, DesignService svc, AiJobService jobs, CancellationToken ct) =>
        {
            var views = req.Views ?? "se";
            if (views != "all" && views.Split(',').Any(v => v.Trim() is not ("se" or "so" or "ne" or "no")))
                return Results.BadRequest("Vistas validas: se, so, ne, no o all.");
            if (req.Quality is not (null or "rapida" or "alta")) return Results.BadRequest("Calidad valida: rapida o alta.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (x.State.Objects.Count == 0) return Results.BadRequest("Agrega muebles antes de renderizar.");
            var job = await jobs.StartBlenderAsync(x.State, x.Engine, views, req.Quality ?? "rapida", ct);
            return Results.Accepted($"/api/ai/jobs/{job.Id}", JobDto.From(job));
        }).WithTags("Blender").WithSummary("Render exacto en Blender (Cycles) y archivo .blend. En CPU tarda minutos.");

        designs.MapGet("/jobs", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
            (await db.AiJobs.AsNoTracking().Where(j => j.DesignId == id).OrderByDescending(j => j.CreatedAt).Take(20).ToListAsync(ct))
                .Select(JobDto.From))
            .WithSummary("Ultimos trabajos de IA de esta version");

        var jobsGroup = app.MapGroup("/api/ai/jobs").WithTags("IA");

        jobsGroup.MapGet("/{jobId}", async (string jobId, AiJobService jobs, CancellationToken ct) =>
            await jobs.GetAsync(jobId, ct) is { } j ? Results.Ok(JobDto.From(j)) : Results.NotFound())
            .WithSummary("Estado del trabajo: pendiente, en_proceso, completado o fallido");

        // Avance en vivo con Server-Sent Events
        jobsGroup.MapGet("/{jobId}/events", async (string jobId, HttpContext http, IServiceScopeFactory scopes, CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            DateTime? last = null;
            var until = DateTime.UtcNow.AddMinutes(10);
            while (!ct.IsCancellationRequested && DateTime.UtcNow < until)
            {
                using var scope = scopes.CreateScope();
                var job = await scope.ServiceProvider.GetRequiredService<AiJobService>().GetAsync(jobId, ct);
                if (job is null)
                {
                    await http.Response.WriteAsync("event: error\ndata: {\"error\":\"no existe\"}\n\n", ct);
                    break;
                }
                if (job.UpdatedAt != last)
                {
                    last = job.UpdatedAt;
                    await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(JobDto.From(job), StateJson.Options)}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
                if (AiJobService.IsTerminal(job.Status)) break;
                await Task.Delay(700, ct);
            }
        }).WithSummary("Avance en vivo del trabajo (SSE)").ExcludeFromDescription();

        // ---- llamadas de n8n (protegidas con X-Callback-Secret) ----

        jobsGroup.MapPost("/{jobId}/progress", async (string jobId, JobProgressRequest req, HttpRequest http, AiOptions options, AiJobService jobs, CancellationToken ct) =>
        {
            if (!Authorized(http, options)) return Results.Unauthorized();
            return await jobs.ProgressAsync(jobId, req.Progress, req.Message, ct) ? Results.NoContent() : Results.NotFound();
        }).ExcludeFromDescription();

        jobsGroup.MapPost("/{jobId}/result", async (string jobId, JobResultRequest req, HttpRequest http, AiOptions options, AiJobService jobs, CancellationToken ct) =>
        {
            if (!Authorized(http, options)) return Results.Unauthorized();
            if (await jobs.GetAsync(jobId, ct) is null) return Results.NotFound();
            await jobs.ResultAsync(jobId, req.Ok, req.Error, req.Data, ct);
            return Results.NoContent();
        }).ExcludeFromDescription();

        jobsGroup.MapPost("/{jobId}/apply", async (string jobId, AiJobService jobs, CancellationToken ct) =>
        {
            var job = await jobs.GetAsync(jobId, ct);
            if (job is null) return Results.NotFound();
            if (job is not { Type: "chat", Status: "completado" }) return Results.BadRequest("Solo se aplican propuestas de chat completadas.");
            if (await jobs.ApplyChatAsync(job, ct) is not { } r) return Results.NotFound();
            r.State.Budget = r.Engine.Budget(r.State);
            return Results.Ok(new
            {
                state = r.State,
                report = ValidationReport.From(r.Engine, r.State),
                outcomes = AiJobService.Outcomes(r.Outcomes, r.Engine),
                applied = r.Outcomes.Count(o => o.Applied),
            });
        }).WithSummary("Aplica las propuestas del chat que el motor acepte (se revalidan contra el diseno actual)");
    }

    private static bool Authorized(HttpRequest http, AiOptions options) =>
        !string.IsNullOrEmpty(options.CallbackSecret)
        && http.Headers.TryGetValue("X-Callback-Secret", out var secret)
        && secret == options.CallbackSecret;
}
