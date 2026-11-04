using InteriorDesign.Api.Services;
using InteriorDesign.Domain;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Mapping;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjects(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projects").WithTags("Proyectos y habitaciones");

        g.MapGet("/", async (InteriorDesignDbContext db, CancellationToken ct) =>
            await db.Projects.AsNoTracking().OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Name, p.Type, p.CreatedAt }).ToListAsync(ct))
            .WithSummary("Lista proyectos");

        g.MapPost("/", async (ProjectRequest req, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (!DomainValues.ProjectTypes.Contains(req.Type)) return Results.BadRequest("type debe ser comercial o domestico.");
            var id = req.Id ?? DesignService.NewId("proy");
            if (await db.Projects.AnyAsync(p => p.Id == id, ct)) return Results.Conflict($"Ya existe el proyecto {id}.");
            db.Projects.Add(new Project { Id = id, Name = req.Name, Type = req.Type, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/projects/{id}", new { id, req.Name, req.Type });
        }).WithSummary("Crea un proyecto");

        g.MapGet("/{id}", async (string id, InteriorDesignDbContext db, DesignService svc, CancellationToken ct) =>
        {
            var p = await db.Projects.AsNoTracking().Include(x => x.Rooms).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return Results.NotFound();
            var designIds = await db.Designs.AsNoTracking().Where(d => d.ProjectId == id)
                .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).Select(d => d.Id).ToListAsync(ct);
            var engine = await svc.EngineAsync(ct);
            var summaries = new List<DesignSummary>();
            foreach (var did in designIds)
            {
                var s = (await svc.Repo.GetStateAsync(did, ct))!;
                summaries.Add(new DesignSummary(s.Design.Id, s.Design.Name, s.Design.ParentId, s.Room.Id, s.Design.Style,
                    s.Design.BudgetMode, s.Design.Revision, s.Design.Favorite, engine.IsValid(s), engine.BudgetTotal(s)));
            }
            return Results.Ok(new ProjectDto(p.Id, p.Name, p.Type, p.Rooms.Select(r => r.Id).OrderBy(x => x).ToList(), summaries));
        }).WithSummary("Proyecto con sus habitaciones y sus versiones de diseno (las pestanas Diseno 1, 2, 3...)");

        g.MapPut("/{id}", async (string id, ProjectRequest req, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (!DomainValues.ProjectTypes.Contains(req.Type)) return Results.BadRequest("type debe ser comercial o domestico.");
            var p = await db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return Results.NotFound();
            p.Name = req.Name;
            p.Type = req.Type;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { p.Id, p.Name, p.Type });
        }).WithSummary("Cambia nombre o tipo del proyecto");

        g.MapDelete("/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Primero las versiones (las habitaciones no se pueden borrar mientras tengan versiones)
            await db.Designs.Where(d => d.ProjectId == id).ExecuteDeleteAsync(ct);
            var deleted = await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        }).WithSummary("Borra el proyecto con TODAS sus habitaciones y versiones");

        // ---------------- habitaciones ----------------

        g.MapPost("/{projectId}/rooms", async (string projectId, RoomRequest req, DesignService svc, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) return Results.NotFound($"No existe el proyecto {projectId}.");
            var errors = svc.SchemaErrors("room", req.Room).Concat(svc.SchemaErrors("requirements", req.Requirements)).ToList();
            if (errors.Count == 0) errors.AddRange(RoomRules.Validate(req.Room));
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["room"] = [.. errors] });
            if (await db.Rooms.AnyAsync(r => r.Id == req.Room.Id, ct)) return Results.Conflict($"Ya existe la habitacion {req.Room.Id}.");
            db.Rooms.Add(StateMapper.ToEntity(req.Room, projectId, req.Requirements));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/rooms/{req.Room.Id}", req);
        }).WithSummary("Crea una habitacion con sus puertas, ventanas y requisitos");

        var rooms = app.MapGroup("/api/rooms").WithTags("Proyectos y habitaciones");

        rooms.MapGet("/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            var r = await LoadRoom(db, id, ct);
            return r is null ? Results.NotFound()
                : Results.Ok(new RoomRequest(StateMapper.ToState(r), r.Requirements.OrderBy(q => q.Id).Select(StateMapper.ToState).ToList()));
        }).WithSummary("Habitacion con sus requisitos");

        rooms.MapPut("/{id}", async (string id, RoomRequest req, DesignService svc, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (req.Room.Id != id) return Results.BadRequest("El id del cuerpo no coincide con la ruta.");
            var errors = svc.SchemaErrors("room", req.Room).Concat(svc.SchemaErrors("requirements", req.Requirements)).ToList();
            if (errors.Count == 0) errors.AddRange(RoomRules.Validate(req.Room));
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["room"] = [.. errors] });
            var current = await LoadRoom(db, id, ct, tracking: true);
            if (current is null) return Results.NotFound();

            var updated = StateMapper.ToEntity(req.Room, current.ProjectId, req.Requirements);
            db.RoomDoors.RemoveRange(current.Doors);
            db.RoomWindows.RemoveRange(current.Windows);
            db.RoomColumns.RemoveRange(current.Columns);
            db.Requirements.RemoveRange(current.Requirements);
            await db.SaveChangesAsync(ct);

            current.Name = updated.Name;
            current.SpaceType = updated.SpaceType;
            current.LengthCm = updated.LengthCm;
            current.WidthCm = updated.WidthCm;
            current.HeightCm = updated.HeightCm;
            current.MeasurementSource = updated.MeasurementSource;
            current.SurfacesJson = updated.SurfacesJson;
            current.CommercialJson = updated.CommercialJson;
            db.RoomDoors.AddRange(updated.Doors);
            db.RoomWindows.AddRange(updated.Windows);
            db.RoomColumns.AddRange(updated.Columns);
            db.Requirements.AddRange(updated.Requirements);
            await db.SaveChangesAsync(ct);
            return Results.Ok(req);
        }).WithSummary("Cambia la habitacion (accion EXPLICITA del usuario: medidas, puertas, ventanas, requisitos). Afecta a todas sus versiones.");

        rooms.MapDelete("/{id}", async (string id, InteriorDesignDbContext db, CancellationToken ct) =>
        {
            if (await db.Designs.AnyAsync(d => d.RoomId == id, ct))
                return Results.Conflict("La habitacion tiene versiones de diseno; borralas primero.");
            var deleted = await db.Rooms.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        }).WithSummary("Borra una habitacion sin versiones");
    }

    private static Task<Room?> LoadRoom(InteriorDesignDbContext db, string id, CancellationToken ct, bool tracking = false)
    {
        var q = db.Rooms.Include(r => r.Doors).Include(r => r.Windows).Include(r => r.Columns).Include(r => r.Requirements).AsSplitQuery();
        return (tracking ? q : q.AsNoTracking()).SingleOrDefaultAsync(r => r.Id == id, ct);
    }
}
