using InteriorDesign.Api.Services;
using InteriorDesign.Domain;
using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Endpoints;

public static class DesignEndpoints
{
    private static IResult Problems(string key, IEnumerable<string> errors) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = errors.ToArray() });

    /// <summary>instanceId nuevo y legible: categoria_N (ej. planta_2), unico dentro de la version.</summary>
    private static string NewInstanceId(string category, DesignState s)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"{category}_{n}";
            if (s.Objects.All(o => o.InstanceId != candidate)) return candidate;
        }
    }

    public static void MapDesigns(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/designs").WithTags("Disenos (versiones)");

        // ---------------- CRUD ----------------

        g.MapPost("/", async (DesignState state, DesignService svc, CancellationToken ct) =>
        {
            state.Budget = null;
            var errors = svc.SchemaErrors(state);
            if (errors.Count == 0) errors.AddRange(RoomRules.Validate(state.Room));
            if (errors.Count > 0) return Problems("designState", errors);
            var engine = await svc.EngineAsync(ct);
            var refs = DesignService.ReferenceErrors(state, engine);
            if (refs.Count > 0) return Problems("references", refs);
            if (await svc.Repo.DesignExistsAsync(state.Design.Id, ct)) return Results.Conflict($"Ya existe la version {state.Design.Id}.");
            if (state.Design.ParentId is { } parent && !await svc.Repo.DesignExistsAsync(parent, ct))
                return Results.BadRequest($"La version padre {parent} no existe.");
            var saved = await svc.Repo.CreateAsync(state, "version creada", state.Design.Author, ct);
            saved.Budget = engine.Budget(saved);
            return Results.Created($"/api/designs/{saved.Design.Id}", new { state = saved, report = ValidationReport.From(engine, saved) });
        }).WithSummary("Crea una version a partir de un DesignState completo (valida esquema y referencias; si la habitacion no existe la crea)");

        g.MapGet("/{id}", async (string id, DesignService svc, CancellationToken ct) =>
            await svc.LoadAsync(id, ct) is { } x ? Results.Ok(x.State) : Results.NotFound())
            .WithSummary("DesignState completo, con el presupuesto calculado por el motor");

        g.MapPut("/{id}", async (string id, DesignState state, DesignService svc, CancellationToken ct) =>
        {
            if (state.Design.Id != id) return Results.BadRequest("El id del cuerpo no coincide con la ruta.");
            state.Budget = null;
            var errors = svc.SchemaErrors(state);
            if (errors.Count > 0) return Problems("designState", errors);
            if (await svc.LoadAsync(id, ct) is not { } current) return Results.NotFound();
            var refs = DesignService.ReferenceErrors(state, current.Engine);
            if (refs.Count > 0) return Problems("references", refs);
            var changed = state.Objects.Select(o => o.InstanceId).Union(current.State.Objects.Select(o => o.InstanceId));
            var saved = await svc.Repo.SaveAsync(state, "version reemplazada completa", changed, state.Design.Author, ct);
            saved.Budget = current.Engine.Budget(saved);
            return Results.Ok(new { state = saved, report = ValidationReport.From(current.Engine, saved) });
        }).WithSummary("Reemplaza objetos, materiales y datos de la version (la habitacion y requisitos se cambian en /api/rooms). Devuelve el reporte del motor.");

        g.MapPatch("/{id}", async (string id, DesignPatch patch, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (patch.Style is not null && !DomainValues.Styles.Contains(patch.Style)) return Results.BadRequest("Estilo invalido.");
            if (patch.BudgetMode is not null && !DomainValues.BudgetModes.Contains(patch.BudgetMode)) return Results.BadRequest("Modo de presupuesto invalido.");
            var s = x.State;
            s.Design.Name = patch.Name ?? s.Design.Name;
            s.Design.Style = patch.Style ?? s.Design.Style;
            s.Design.BudgetMode = patch.BudgetMode ?? s.Design.BudgetMode;
            s.Design.Favorite = patch.Favorite ?? s.Design.Favorite;
            s.Design.Comments = patch.Comments ?? s.Design.Comments;
            var saved = await svc.Repo.SaveAsync(s, "datos de la version actualizados", [], null, ct);
            return Results.Ok(ValidationReport.From(x.Engine, saved));
        }).WithSummary("Cambia nombre, estilo, modo de presupuesto (minimizar|ajustar|flexible), favorito o comentarios");

        g.MapDelete("/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.Designs.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        }).WithSummary("Borra la version (sus versiones hijas se conservan, sin padre)");

        // ---------------- validacion y presupuesto ----------------

        g.MapGet("/{id}/validation", async (string id, DesignService svc, CancellationToken ct) =>
            await svc.LoadAsync(id, ct) is { } x ? Results.Ok(ValidationReport.From(x.Engine, x.State)) : Results.NotFound())
            .WithSummary("Conflictos (bloquean) y advertencias (no bloquean) del motor");

        g.MapGet("/{id}/budget", async (string id, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var b = x.Engine.Budget(x.State);
            var status = b.EffectiveCap is not { } cap ? "sin_tope"
                : b.Total.Min > cap ? "excedido"
                : b.Total.Max > cap ? "puede_excederse"
                : "dentro";
            return Results.Ok(new
            {
                designId = id, budgetMode = x.State.Design.BudgetMode, budgetMax = DesignEngine.BudgetMax(x.State),
                b.EffectiveCap, b.ByCategory, b.Total, status
            });
        }).WithSummary("Presupuesto por rubro con rangos min-max y estado frente al tope");

        g.MapGet("/{id}/verify-prolog", async (string id, DesignService svc, PrologVerifier prolog, CancellationToken ct) =>
            await svc.LoadAsync(id, ct) is { } x ? Results.Ok(await prolog.CompareAsync(x.State, x.Engine, ct)) : Results.NotFound())
            .WithSummary("Corre la misma validacion en diseno.pl (SWI-Prolog) y la compara con el motor en C#");

        // ---------------- propuestas: IA propone -> motor valida -> se aplica ----------------

        g.MapPost("/{id}/proposals/validate", async (string id, ProposalRequest req, bool? verifyWithProlog,
            DesignService svc, PrologVerifier prolog, CancellationToken ct) =>
        {
            if (req.ToProposal(out var error) is not { } proposal) return Results.BadRequest(error);
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var verdict = x.Engine.Judge(x.State, proposal);
            var check = verifyWithProlog == true ? await prolog.VerdictAsync(x.State, x.Engine, proposal, verdict, ct) : null;
            return Results.Ok(VerdictDto.From(verdict, check));
        }).WithSummary("Valida una propuesta SIN aplicarla (equivale a veredicto/3)");

        g.MapPost("/{id}/proposals/apply", async (string id, ProposalRequest req, DesignService svc, CancellationToken ct) =>
        {
            if (req.ToProposal(out var error) is not { } proposal) return Results.BadRequest(error);
            if (proposal is ModifyStructureProposal)
                return Results.BadRequest("modificar_estructura no cambia el diseno: confirma el elemento en /structural-confirmations y edita la habitacion en /api/rooms.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var verdict = x.Engine.Judge(x.State, proposal);
            if (!verdict.Accepted) return Results.Conflict(VerdictDto.From(verdict));
            var (next, changed) = ProposalApplier.Apply(x.State, proposal, x.Engine);
            var saved = await svc.Repo.SaveAsync(next, proposal.ToTerm().ToString(), changed, null, ct);
            saved.Budget = x.Engine.Budget(saved);
            return Results.Ok(new ApplyResult(VerdictDto.From(verdict), saved, ValidationReport.From(x.Engine, saved)));
        }).WithSummary("Valida y, solo si es aceptada, aplica la propuesta y la guarda en el historial. Rechazada -> 409 con los motivos.");

        g.MapPost("/{id}/proposals/moves", async (string id, MoveBatchRequest req, bool? dryRun, DesignService svc, CancellationToken ct) =>
        {
            if (req.Moves.Count == 0) return Results.BadRequest("No hay movimientos.");
            if (req.Moves.Select(m => m.InstanceId).Distinct().Count() != req.Moves.Count)
                return Results.BadRequest("Cada objeto puede aparecer una sola vez en el lote.");
            if (req.Moves.Any(m => !DomainValues.Rotations.Contains(m.Rotation)))
                return Results.BadRequest("rotation debe ser 0, 90, 180 o 270.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var missing = req.Moves.Where(m => x.State.Objects.All(o => o.InstanceId != m.InstanceId)).Select(m => m.InstanceId).ToList();
            if (missing.Count > 0) return Results.BadRequest($"No existen: {string.Join(", ", missing)}");

            var moves = req.Moves.Select(m => new MoveProposal(m.InstanceId, m.X, m.Y, m.Rotation)).ToList();
            var (final, results) = ProposalApplier.JudgeMoves(x.State, moves, x.Engine);
            var accepted = results.All(r => r.Verdict.Accepted);
            var checks = results.Select(r => new MoveCheck(r.Move.InstanceId, VerdictDto.From(r.Verdict))).ToList();
            final.Budget = x.Engine.Budget(final);

            if (dryRun == true) return Results.Ok(new MoveBatchResult(accepted, checks, ValidationReport.From(x.Engine, final)));
            if (!accepted) return Results.Conflict(new MoveBatchResult(false, checks, ValidationReport.From(x.Engine, final)));

            var description = moves.Count == 1 ? moves[0].ToTerm().ToString() : $"{moves.Count} movimientos: {string.Join(", ", moves.Select(m => m.ToTerm()))}";
            var saved = await svc.Repo.SaveAsync(final, description, moves.Select(m => m.InstanceId), null, ct);
            saved.Budget = x.Engine.Budget(saved);
            return Results.Ok(new MoveBatchResult(true, checks, ValidationReport.From(x.Engine, saved), saved));
        }).WithSummary("Lote de movimientos del editor. dryRun=true solo revisa (para pintar en vivo); sin dryRun guarda solo si TODOS son validos (si no, 409).");

        g.MapPost("/{id}/objects", async (string id, AddObjectRequest req, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (x.Engine.Product(req.CatalogId) is not { } product) return Results.BadRequest($"El producto {req.CatalogId} no existe.");
            var instanceId = req.InstanceId ?? NewInstanceId(product.Category, x.State);
            var (placement, verdict) = x.Engine.SuggestPlacement(x.State, instanceId, req.CatalogId);
            if (placement is null) return Results.Conflict(VerdictDto.From(verdict));
            var (next, changed) = ProposalApplier.Apply(x.State, placement, x.Engine);
            var saved = await svc.Repo.SaveAsync(next, placement.ToTerm().ToString(), changed, null, ct);
            saved.Budget = x.Engine.Budget(saved);
            return Results.Ok(new ApplyResult(VerdictDto.From(verdict), saved, ValidationReport.From(x.Engine, saved)));
        }).WithSummary("Agrega un producto del catalogo: el motor busca el primer lugar libre valido (del centro hacia afuera). Si no se puede (presupuesto, categoria, sin espacio) -> 409 con motivos.");

        // ---------------- objetos ----------------

        g.MapPatch("/{id}/objects/{instanceId}/lock", async (string id, string instanceId, LockRequest req, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var o = x.State.Objects.SingleOrDefault(o => o.InstanceId == instanceId);
            if (o is null) return Results.NotFound($"No existe el objeto {instanceId}.");
            o.Locked = req.Locked;
            var saved = await svc.Repo.SaveAsync(x.State, req.Locked ? $"bloqueado {instanceId}" : $"desbloqueado {instanceId}", [instanceId], null, ct);
            return Results.Ok(saved.Objects.Single(p => p.InstanceId == instanceId));
        }).WithSummary("Bloquea o desbloquea un objeto (la IA no puede mover, reemplazar ni quitar uno bloqueado)");

        g.MapPatch("/{id}/objects/{instanceId}/config", async (string id, string instanceId, ConfigRequest req, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            var o = x.State.Objects.SingleOrDefault(o => o.InstanceId == instanceId);
            if (o is null) return Results.NotFound($"No existe el objeto {instanceId}.");
            var p = x.Engine.Product(o.CatalogId)!;
            if (req.Color is not null && !p.Colors.Contains(req.Color))
                return Results.Conflict(new { code = "color_no_disponible", available = p.Colors });
            if (req.Material is not null && !p.Materials.Contains(req.Material))
                return Results.Conflict(new { code = "material_no_disponible", available = p.Materials });
            o.Config ??= new ObjectConfig();
            o.Config.Color = req.Color ?? o.Config.Color;
            o.Config.Material = req.Material ?? o.Config.Material;
            var saved = await svc.Repo.SaveAsync(x.State, $"configuracion de {instanceId}", [instanceId], null, ct);
            saved.Budget = x.Engine.Budget(saved);
            return Results.Ok(new { @object = saved.Objects.Single(q => q.InstanceId == instanceId), report = ValidationReport.From(x.Engine, saved) });
        }).WithSummary("Cambia color o material del objeto (solo los que ofrece el producto)");

        g.MapGet("/{id}/objects/{instanceId}/alternatives", async (string id, string instanceId, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (x.State.Objects.All(o => o.InstanceId != instanceId)) return Results.NotFound($"No existe el objeto {instanceId}.");
            return Results.Ok(x.Engine.Alternatives(x.State, instanceId));
        }).WithSummary("Productos similares que SI se pueden poner en el lugar del objeto (alternativas/3)");

        g.MapGet("/{id}/savings", async (string id, DesignService svc, CancellationToken ct) =>
            await svc.LoadAsync(id, ct) is { } x
                ? Results.Ok(x.Engine.Savings(x.State).Select(s => new SavingDto(
                    s.InstanceId, x.State.Objects.Single(o => o.InstanceId == s.InstanceId).CatalogId, s.Product.Id, s.Product.Name, s.Saving)))
                : Results.NotFound())
            .WithSummary("Reemplazos validos que ahorran dinero, sin tocar los bloqueados (sustitucion_ahorro/4)");

        // ---------------- estructura ----------------

        g.MapPost("/{id}/structural-confirmations", async (string id, StructuralRequest req, DesignService svc, CancellationToken ct) =>
        {
            if (!DomainValues.StructuralElements.Contains(req.Element)) return Results.BadRequest("element debe ser muro, columna, puerta o ventana.");
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            if (!x.State.StructuralConfirmations.Contains(req.Element)) x.State.StructuralConfirmations.Add(req.Element);
            var saved = await svc.Repo.SaveAsync(x.State, $"el usuario autorizo modificar {req.Element}", [], null, ct);
            return Results.Ok(saved.StructuralConfirmations);
        }).WithSummary("El usuario autoriza EXPLICITAMENTE modificar un elemento estructural");

        g.MapDelete("/{id}/structural-confirmations/{element}", async (string id, string element, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } x) return Results.NotFound();
            x.State.StructuralConfirmations.Remove(element);
            var saved = await svc.Repo.SaveAsync(x.State, $"se retiro el permiso para modificar {element}", [], null, ct);
            return Results.Ok(saved.StructuralConfirmations);
        }).WithSummary("Retira la autorizacion");

        // ---------------- versiones ----------------

        g.MapPost("/{id}/duplicate", async (string id, DuplicateRequest req, DesignService svc, CancellationToken ct) =>
        {
            var newId = req.NewId ?? DesignService.NewId("d");
            if (await svc.Repo.DesignExistsAsync(newId, ct)) return Results.Conflict($"Ya existe la version {newId}.");
            var copy = await svc.Repo.DuplicateAsync(id, newId, req.Name, null, ct);
            return copy is null ? Results.NotFound() : Results.Created($"/api/designs/{newId}", copy);
        }).WithSummary("Duplica (o ramifica) una version: copia independiente con la original como padre");

        g.MapGet("/{id}/compare/{otherId}", async (string id, string otherId, DesignService svc, CancellationToken ct) =>
        {
            if (await svc.LoadAsync(id, ct) is not { } a) return Results.NotFound($"No existe {id}.");
            var b = await svc.Repo.GetStateAsync(otherId, ct);
            if (b is null) return Results.NotFound($"No existe {otherId}.");
            return Results.Ok(new CompareDto(id, otherId, a.Engine.Compare(a.State, b).Select(ReasonDto.From).ToList(),
                a.Engine.BudgetTotal(a.State), a.Engine.BudgetTotal(b)));
        }).WithSummary("Diferencias por categoria entre dos versiones y sus presupuestos (comparar_versiones/3)");

        g.MapGet("/{id}/history", async (string id, DesignService svc, CancellationToken ct) =>
        {
            if (!await svc.Repo.DesignExistsAsync(id, ct)) return Results.NotFound();
            return Results.Ok((await svc.Repo.GetHistoryAsync(id, ct)).Select(h =>
                new HistoryDto(h.Id, DateTime.SpecifyKind(h.Date, DateTimeKind.Utc), h.Author, h.Description, h.ChangedInstanceIds, h.Revision)));
        }).WithSummary("Historial de cambios de la version");

        g.MapPost("/{id}/history/{historyId:long}/revert", async (string id, long historyId, DesignService svc, CancellationToken ct) =>
        {
            var reverted = await svc.Repo.RevertAsync(id, historyId, null, ct);
            return reverted is null ? Results.NotFound() : Results.Ok(reverted);
        }).WithSummary("Regresa la version al estado de esa entrada del historial (se registra como un cambio nuevo)");

        g.MapGet("/{id}/ancestors", async (string id, DesignService svc, CancellationToken ct) =>
            await svc.Repo.DesignExistsAsync(id, ct) ? Results.Ok(await svc.Repo.GetAncestorsAsync(id, ct)) : Results.NotFound())
            .WithSummary("Versiones padre, de la mas cercana a la inicial (ancestro/2)");
    }
}
