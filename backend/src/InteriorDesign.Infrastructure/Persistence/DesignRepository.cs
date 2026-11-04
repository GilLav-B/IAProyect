using System.Text.Json;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;
using InteriorDesign.Infrastructure.Mapping;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Infrastructure.Persistence;

/// <summary>
/// Lee y guarda DesignStates. Cada cambio guardado sube la revision y deja una entrada de
/// historial con el snapshot completo, para poder revertir (principio 10).
/// </summary>
public class DesignRepository(InteriorDesignDbContext db)
{
    private IQueryable<Design> DesignsWithAll() => db.Designs
        .Include(d => d.Project)
        .Include(d => d.Room!).ThenInclude(r => r.Doors)
        .Include(d => d.Room!).ThenInclude(r => r.Windows)
        .Include(d => d.Room!).ThenInclude(r => r.Columns)
        .Include(d => d.Room!).ThenInclude(r => r.Requirements)
        .Include(d => d.Objects)
        .Include(d => d.Materials)
        .Include(d => d.History)
        .AsSplitQuery();

    public async Task<CatalogState> GetCatalogAsync(CancellationToken ct = default) => new()
    {
        Products = (await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList(),
        Materials = (await db.Materials.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList(),
        Providers = (await db.Providers.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct)).Select(StateMapper.ToState).ToList()
    };

    public async Task<DesignState?> GetStateAsync(string designId, CancellationToken ct = default)
    {
        var design = await DesignsWithAll().AsNoTracking().SingleOrDefaultAsync(d => d.Id == designId, ct);
        return design is null ? null : StateMapper.ToState(design);
    }

    public Task<bool> DesignExistsAsync(string designId, CancellationToken ct = default) =>
        db.Designs.AnyAsync(d => d.Id == designId, ct);

    private static string Snapshot(DesignState s)
    {
        var copy = JsonSerializer.Deserialize<DesignState>(JsonSerializer.Serialize(s, StateJson.Options), StateJson.Options)!;
        copy.History = [];
        copy.Budget = null;
        return JsonSerializer.Serialize(copy, StateJson.Options);
    }

    /// <summary>
    /// Crea la version. Si el proyecto o la habitacion no existen, tambien se crean
    /// (con los requisitos del DesignState). Si ya existen se usan los guardados.
    /// </summary>
    public async Task<DesignState> CreateAsync(DesignState s, string description, string? author = null, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Projects.AnyAsync(p => p.Id == s.Project.Id, ct))
            db.Projects.Add(new Project { Id = s.Project.Id, Name = s.Project.Name, Type = s.Project.Type });
        if (!await db.Rooms.AnyAsync(r => r.Id == s.Room.Id, ct))
            db.Rooms.Add(StateMapper.ToEntity(s.Room, s.Project.Id, s.Requirements));
        await db.SaveChangesAsync(ct);

        var entity = StateMapper.ToEntity(s);
        entity.Author ??= author;
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        db.Designs.Add(entity);
        await db.SaveChangesAsync(ct);

        var saved = (await GetStateAsync(s.Design.Id, ct))!;
        await AddHistoryAsync(saved, description, author, s.Objects.Select(o => o.InstanceId), ct);
        await tx.CommitAsync(ct);
        return (await GetStateAsync(s.Design.Id, ct))!;
    }

    /// <summary>
    /// Guarda los cambios de la version (objetos, materiales, estilo, modo, iluminacion...).
    /// La habitacion y los requisitos NO se cambian aqui: tienen su propio endpoint (son estructurales).
    /// </summary>
    public async Task<DesignState> SaveAsync(DesignState s, string description, IEnumerable<string> changedInstanceIds,
        string? author = null, CancellationToken ct = default)
    {
        // Los objetos se reemplazan con ExecuteDelete; se limpia el tracker para no chocar con llaves ya cargadas.
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = s.Design.Id;
        var current = await db.Designs.SingleAsync(d => d.Id == id, ct);

        await db.DesignObjects.Where(o => o.DesignId == id).ExecuteDeleteAsync(ct);
        await db.DesignMaterials.Where(m => m.DesignId == id).ExecuteDeleteAsync(ct);

        var updated = StateMapper.ToEntity(s);
        current.Name = updated.Name;
        current.Style = updated.Style;
        current.BudgetMode = updated.BudgetMode;
        current.Favorite = updated.Favorite;
        current.Comments = updated.Comments;
        current.ThumbnailUrl = updated.ThumbnailUrl;
        current.RenderUrl = updated.RenderUrl;
        current.InstallationMin = updated.InstallationMin;
        current.InstallationMax = updated.InstallationMax;
        current.LightingJson = updated.LightingJson;
        current.StructuralConfirmations = updated.StructuralConfirmations;
        current.Revision += 1;
        current.UpdatedAt = DateTime.UtcNow;
        db.DesignObjects.AddRange(updated.Objects);
        db.DesignMaterials.AddRange(updated.Materials);
        await db.SaveChangesAsync(ct);

        var saved = (await GetStateAsync(id, ct))!;
        await AddHistoryAsync(saved, description, author, changedInstanceIds, ct);
        await tx.CommitAsync(ct);
        return (await GetStateAsync(id, ct))!;
    }

    private async Task AddHistoryAsync(DesignState s, string description, string? author, IEnumerable<string> changed, CancellationToken ct)
    {
        db.DesignHistory.Add(new DesignHistoryEntry
        {
            DesignId = s.Design.Id,
            Date = DateTime.UtcNow,
            Author = author,
            Description = description,
            ChangedInstanceIds = changed.Distinct().ToArray(),
            Revision = s.Design.Revision,
            SnapshotJson = Snapshot(s)
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<DesignHistoryEntry>> GetHistoryAsync(string designId, CancellationToken ct = default) =>
        await db.DesignHistory.AsNoTracking()
            .Where(h => h.DesignId == designId)
            .OrderBy(h => h.Date).ThenBy(h => h.Id)
            .ToListAsync(ct);

    /// <summary>Duplica una version: copia independiente, con el mismo instanceId por objeto y parent = origen.</summary>
    public async Task<DesignState?> DuplicateAsync(string sourceId, string newId, string? name, string? author = null, CancellationToken ct = default)
    {
        var source = await GetStateAsync(sourceId, ct);
        if (source is null) return null;
        source.Design.Id = newId;
        source.Design.Name = name ?? $"{source.Design.Name} (copia)";
        source.Design.ParentId = sourceId;
        source.Design.Revision = 0;
        source.Design.Favorite = false;
        source.History = [];
        return await CreateAsync(source, $"duplicado de {sourceId}", author, ct);
    }

    /// <summary>Vuelve al snapshot de una entrada del historial (queda como un cambio nuevo, no se borra nada).</summary>
    public async Task<DesignState?> RevertAsync(string designId, long historyId, string? author = null, CancellationToken ct = default)
    {
        var entry = await db.DesignHistory.AsNoTracking().SingleOrDefaultAsync(h => h.Id == historyId && h.DesignId == designId, ct);
        if (entry?.SnapshotJson is null) return null;
        var snapshot = JsonSerializer.Deserialize<DesignState>(entry.SnapshotJson, StateJson.Options)!;
        var current = (await GetStateAsync(designId, ct))!;
        var changed = snapshot.Objects.Select(o => o.InstanceId).Union(current.Objects.Select(o => o.InstanceId));
        return await SaveAsync(snapshot, $"revertido a la revision {entry.Revision}", changed, author, ct);
    }

    /// <summary>ancestro/2: cadena de versiones padre, de la mas cercana a la inicial.</summary>
    public async Task<List<string>> GetAncestorsAsync(string designId, CancellationToken ct = default)
    {
        var parents = await db.Designs.AsNoTracking().Select(d => new { d.Id, d.ParentId }).ToDictionaryAsync(d => d.Id, d => d.ParentId, ct);
        var result = new List<string>();
        var current = parents.GetValueOrDefault(designId);
        while (current is not null && !result.Contains(current))
        {
            result.Add(current);
            current = parents.GetValueOrDefault(current);
        }
        return result;
    }
}
