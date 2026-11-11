using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>
/// Propuesta tal como la regresa la IA (JSON). La IA no decide: el motor valida cada una.
/// action: agregar | mover | reemplazar | quitar. x, y, rotation son opcionales en "agregar":
/// si faltan, o si el lugar propuesto no es valido, el motor busca un lugar libre.
/// </summary>
public record AiProposal(
    string Action,
    string? InstanceId = null,
    string? CatalogId = null,
    int? X = null,
    int? Y = null,
    int? Rotation = null,
    string? Reason = null);

/// <summary>Resultado de una propuesta: que se pidio, que se valido y si se aplico.</summary>
public record AiProposalOutcome(AiProposal Proposal, bool Applied, string Verdict, IReadOnlyList<Term> Reasons, Proposal? Validated);

/// <summary>
/// Aplica una lista de propuestas de la IA en orden sobre una COPIA del DesignState:
/// cada una se juzga contra el estado que dejaron las anteriores y solo se aplica si el motor la acepta.
/// IA propone -> Design Engine valida -> sistema acepta/rechaza.
/// </summary>
public static class AiPlan
{
    private static readonly HashSet<string> PositionReasons = ["fuera_de_habitacion", "colision_con", "bloquea_puerta", "bloquea_ventana"];

    public static (DesignState Final, List<AiProposalOutcome> Outcomes) Run(DesignState state, IEnumerable<AiProposal> proposals, DesignEngine engine)
    {
        var current = ProposalApplier.Clone(state);
        var outcomes = new List<AiProposalOutcome>();

        foreach (var p in proposals)
        {
            var proposal = ToProposal(current, p, engine, out var error);
            if (proposal is null)
            {
                outcomes.Add(new AiProposalOutcome(p, false, "rechazada", [Term.A(error ?? "propuesta_incompleta")], null));
                continue;
            }

            var verdict = engine.Judge(current, proposal);

            // "agregar" con un lugar invalido: el motor busca otro lugar para el mismo producto
            if (!verdict.Accepted && proposal is AddProposal add && verdict.Reasons.All(r => PositionReasons.Contains(r.Name)))
            {
                var (placement, v) = engine.SuggestPlacement(current, add.InstanceId, add.CatalogId);
                if (placement is not null)
                {
                    proposal = placement;
                    verdict = v;
                }
            }

            if (verdict.Accepted)
            {
                (current, _) = ProposalApplier.Apply(current, proposal, engine);
                outcomes.Add(new AiProposalOutcome(p, true, verdict.ToString(), verdict.Reasons, proposal));
            }
            else
            {
                outcomes.Add(new AiProposalOutcome(p, false, verdict.ToString(), verdict.Reasons, proposal));
            }
        }
        return (current, outcomes);
    }

    private static Proposal? ToProposal(DesignState s, AiProposal p, DesignEngine engine, out string? error)
    {
        error = null;
        int Rot(int? r) => Geometry.Normalize(r ?? 0);
        switch (p.Action)
        {
            case "agregar":
            {
                if (p.CatalogId is null || engine.Product(p.CatalogId) is not { } product)
                {
                    error = "producto_inexistente";
                    return null;
                }
                var id = p.InstanceId is { Length: > 0 } given && s.Objects.All(o => o.InstanceId != given)
                    ? given
                    : NewInstanceId(product.Category, s);
                if (p.X is null || p.Y is null)
                {
                    var (placement, _) = engine.SuggestPlacement(s, id, product.Id);
                    return placement ?? (Proposal)new AddProposal(id, product.Id, 0, 0, 0);
                }
                return new AddProposal(id, product.Id, p.X.Value, p.Y.Value, Rot(p.Rotation));
            }
            case "mover":
            {
                var obj = s.Objects.FirstOrDefault(o => o.InstanceId == p.InstanceId);
                if (obj is null) { error = "objeto_inexistente"; return null; }
                var move = new MoveProposal(obj.InstanceId, p.X ?? obj.Position.X, p.Y ?? obj.Position.Y, p.Rotation is null ? obj.Rotation : Rot(p.Rotation));
                if (move.X == obj.Position.X && move.Y == obj.Position.Y && move.Rotation == obj.Rotation) { error = "sin_cambio"; return null; }
                return move;
            }
            case "reemplazar":
                if (p.InstanceId is null || p.CatalogId is null) { error = "propuesta_incompleta"; return null; }
                // La IA a veces "reemplaza" por el mismo producto: no cambia nada
                if (s.Objects.FirstOrDefault(o => o.InstanceId == p.InstanceId)?.CatalogId == p.CatalogId) { error = "sin_cambio"; return null; }
                return new ReplaceProposal(p.InstanceId, p.CatalogId);
            case "quitar":
                if (p.InstanceId is null) { error = "propuesta_incompleta"; return null; }
                return new RemoveProposal(p.InstanceId);
            default:
                error = "accion_desconocida";
                return null;
        }
    }

    /// <summary>instanceId nuevo y legible: categoria_N, unico en la version.</summary>
    public static string NewInstanceId(string category, DesignState s)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"{category}_{n}";
            if (s.Objects.All(o => o.InstanceId != candidate)) return candidate;
        }
    }
}
