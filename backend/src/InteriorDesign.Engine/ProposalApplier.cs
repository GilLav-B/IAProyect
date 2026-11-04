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
