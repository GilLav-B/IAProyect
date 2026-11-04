using System.Text.Json;
using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>
/// Aplica una propuesta YA ACEPTADA sobre una copia del DesignState (el original no se toca).
/// El flujo es: IA propone -> DesignEngine.Judge -> si es aceptada, Apply -> se guarda.
/// </summary>
public static class ProposalApplier
{
    public static DesignState Clone(DesignState s) =>
        JsonSerializer.Deserialize<DesignState>(JsonSerializer.Serialize(s, StateJson.Options), StateJson.Options)!;

    /// <summary>
    /// Varios movimientos a la vez (borrador del editor). Se aplican todos y cada uno se juzga
    /// contra el estado FINAL (los demas ya en su lugar nuevo), asi el orden no importa.
    /// Solo se puede guardar si todos son aceptados.
    /// </summary>
    public static (DesignState Final, List<(MoveProposal Move, Verdict Verdict)> Results) JudgeMoves(
        DesignState s, IReadOnlyList<MoveProposal> moves, DesignEngine engine)
    {
        var final = Clone(s);
        final.Budget = null;
        var results = new List<(MoveProposal, Verdict)>();
        foreach (var m in moves)
        {
            var o = final.Objects.FirstOrDefault(x => x.InstanceId == m.InstanceId);
            if (o is null) continue;
            o.Position.X = m.X;
            o.Position.Y = m.Y;
            o.Rotation = m.Rotation;
        }
        foreach (var m in moves)
            results.Add((m, engine.Judge(final, m)));
        return (final, results);
    }

    /// <summary>Devuelve la copia modificada y los instanceId que cambiaron.</summary>
    public static (DesignState State, List<string> Changed) Apply(DesignState s, Proposal proposal, DesignEngine engine)
    {
        var copy = Clone(s);
        copy.Budget = null;
        switch (proposal)
        {
            case MoveProposal m:
            {
                var o = copy.Objects.Single(x => x.InstanceId == m.InstanceId);
                o.Position.X = m.X;
                o.Position.Y = m.Y;
                o.Rotation = m.Rotation;
                return (copy, [m.InstanceId]);
            }
            case AddProposal a:
            {
                var p = engine.Product(a.CatalogId) ?? throw new InvalidOperationException($"Producto {a.CatalogId} no existe.");
                copy.Objects.Add(new PlacedObject
                {
                    InstanceId = a.InstanceId, CatalogId = p.Id, Type = p.Category,
                    Position = new Position { X = a.X, Y = a.Y }, Rotation = a.Rotation
                });
                return (copy, [a.InstanceId]);
            }
            case ReplaceProposal r:
            {
                var p = engine.Product(r.CatalogId) ?? throw new InvalidOperationException($"Producto {r.CatalogId} no existe.");
                var o = copy.Objects.Single(x => x.InstanceId == r.InstanceId);
                o.CatalogId = p.Id;
                o.Type = p.Category;
                // [SUPUESTO] si el color elegido no existe en el producto nuevo, se quita la eleccion de color
                if (o.Config?.Color is { } color && !p.Colors.Contains(color))
                    o.Config.Color = null;
                if (o.Config?.Material is { } material && !p.Materials.Contains(material))
                    o.Config.Material = null;
                if (o.Config is { Color: null, Material: null })
                    o.Config = null;
                return (copy, [r.InstanceId]);
            }
            case RemoveProposal q:
                copy.Objects.RemoveAll(x => x.InstanceId == q.InstanceId);
                return (copy, [q.InstanceId]);
            default:
                throw new InvalidOperationException("Esta propuesta no cambia el DesignState (la estructura se edita en la habitacion).");
        }
    }
}
