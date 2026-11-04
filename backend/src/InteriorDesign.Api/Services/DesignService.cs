using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Persistence;
using InteriorDesign.Infrastructure.Validation;

namespace InteriorDesign.Api.Services;

/// <summary>Une repositorio, validador de esquemas y motor para los endpoints.</summary>
public class DesignService(DesignRepository repo, SchemaValidator schemas, Rules rules)
{
    public DesignRepository Repo => repo;

    public async Task<DesignEngine> EngineAsync(CancellationToken ct = default) =>
        new(await repo.GetCatalogAsync(ct), rules);

    /// <summary>Carga el DesignState con el presupuesto calculado por el motor.</summary>
    public async Task<(DesignState State, DesignEngine Engine)?> LoadAsync(string designId, CancellationToken ct = default)
    {
        var state = await repo.GetStateAsync(designId, ct);
        if (state is null) return null;
        var engine = await EngineAsync(ct);
        state.Budget = engine.Budget(state);
        return (state, engine);
    }

    /// <summary>Errores de esquema (forma del JSON).</summary>
    public List<string> SchemaErrors(DesignState s) =>
        schemas.Validate("design-state", s, StateJson.Options);

    public List<string> SchemaErrors<T>(string schema, T value) =>
        schemas.Validate(schema, value, StateJson.Options);

    /// <summary>Errores de referencia: productos o materiales que no existen, tipos que no coinciden, ids repetidos.</summary>
    public static List<string> ReferenceErrors(DesignState s, DesignEngine engine)
    {
        var errors = new List<string>();
        foreach (var dup in s.Objects.GroupBy(o => o.InstanceId).Where(g => g.Count() > 1))
            errors.Add($"instanceId repetido: {dup.Key}");
        foreach (var o in s.Objects)
        {
            var p = engine.Product(o.CatalogId);
            if (p is null) errors.Add($"{o.InstanceId}: el producto {o.CatalogId} no existe en el catalogo");
            else if (p.Category != o.Type) errors.Add($"{o.InstanceId}: type '{o.Type}' no coincide con la categoria del producto '{p.Category}'");
        }
        var materials = engine.Catalog.Materials.Select(m => m.Id).ToHashSet();
        foreach (var m in s.Materials.Where(m => !materials.Contains(m.MaterialId)))
            errors.Add($"el material {m.MaterialId} no existe en el catalogo");
        return errors;
    }

    public static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 9)];
}
