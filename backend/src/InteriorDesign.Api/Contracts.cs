using InteriorDesign.Domain.State;
using InteriorDesign.Engine;

namespace InteriorDesign.Api;

/// <summary>Motivo, conflicto o advertencia del motor. Term es el texto estilo Prolog (ej. bloquea_puerta(sofa_d3,p1)).</summary>
public record ReasonDto(string Code, IReadOnlyList<string> Args, string Term)
{
    public static ReasonDto From(Term t) => t switch
    {
        Compound c => new ReasonDto(c.Functor, c.Args.Select(a => a is Atom at ? at.Value : a.ToString()).ToList(), t.ToString()),
        Atom a => new ReasonDto(a.Value, [], t.ToString()),
        _ => new ReasonDto(t.Name, [], t.ToString())
    };
}

public record ValidationReport(
    string DesignId,
    bool Valid,
    IReadOnlyList<ReasonDto> Conflicts,
    IReadOnlyList<ReasonDto> Warnings,
    BudgetSnapshot Budget)
{
    public static ValidationReport From(DesignEngine engine, DesignState s) => new(
        s.Design.Id,
        engine.IsValid(s),
        engine.Conflicts(s).Select(ReasonDto.From).ToList(),
        engine.Warnings(s).Select(ReasonDto.From).ToList(),
        engine.Budget(s));
}

/// <summary>
/// Propuesta de cambio. action: mover | agregar | reemplazar | quitar | modificar_estructura.
/// mover: instanceId, x, y, rotation. agregar: instanceId, catalogId, x, y, rotation.
/// reemplazar: instanceId, catalogId. quitar: instanceId. modificar_estructura: element.
/// </summary>
public record ProposalRequest(
    string Action,
    string? InstanceId = null,
    string? CatalogId = null,
    int? X = null,
    int? Y = null,
    int? Rotation = null,
    string? Element = null)
{
    public Proposal? ToProposal(out string? error)
    {
        error = null;
        Proposal? p = Action switch
        {
            "mover" when InstanceId is not null && X is not null && Y is not null && Rotation is not null
                => new MoveProposal(InstanceId, X.Value, Y.Value, Rotation.Value),
            "agregar" when InstanceId is not null && CatalogId is not null && X is not null && Y is not null && Rotation is not null
                => new AddProposal(InstanceId, CatalogId, X.Value, Y.Value, Rotation.Value),
            "reemplazar" when InstanceId is not null && CatalogId is not null => new ReplaceProposal(InstanceId, CatalogId),
            "quitar" when InstanceId is not null => new RemoveProposal(InstanceId),
            "modificar_estructura" when Element is not null => new ModifyStructureProposal(Element),
            _ => null
        };
        if (p is null)
            error = $"Propuesta incompleta o accion desconocida '{Action}'. Ver la descripcion de ProposalRequest.";
        else if (Rotation is not null && !InteriorDesign.Domain.DomainValues.Rotations.Contains(Rotation.Value))
        {
            error = "rotation debe ser 0, 90, 180 o 270.";
            p = null;
        }
        return p;
    }
}

public record PrologCheck(string Verdict, bool Matches);

public record VerdictDto(bool Accepted, string Verdict, IReadOnlyList<ReasonDto> Reasons, PrologCheck? Prolog = null)
{
    public static VerdictDto From(Verdict v, PrologCheck? prolog = null) =>
        new(v.Accepted, v.ToString(), v.Reasons.Select(ReasonDto.From).ToList(), prolog);
}

public record ApplyResult(VerdictDto Verdict, DesignState State, ValidationReport Report);

/// <summary>Movimiento del borrador del editor (cm y grados).</summary>
public record MoveDto(string InstanceId, int X, int Y, int Rotation);

public record MoveBatchRequest(List<MoveDto> Moves);

public record MoveCheck(string InstanceId, VerdictDto Verdict);

/// <summary>
/// Resultado del lote. Accepted = todos los movimientos son validos (se puede guardar).
/// Report es la validacion completa del diseno como quedaria. State solo viene cuando se guardo.
/// </summary>
public record MoveBatchResult(bool Accepted, IReadOnlyList<MoveCheck> Results, ValidationReport Report, DesignState? State = null);

public record DesignSummary(
    string Id, string Name, string? ParentId, string Style, string BudgetMode, int Revision,
    bool Favorite, bool Valid, PriceRange Total);

public record ProjectRequest(string? Id, string Name, string Type);

public record ProjectDto(string Id, string Name, string Type, IReadOnlyList<string> RoomIds, IReadOnlyList<DesignSummary> Designs);

public record RoomRequest(RoomState Room, List<RequirementState> Requirements);

public record DesignPatch(string? Name = null, string? Style = null, string? BudgetMode = null, bool? Favorite = null, string? Comments = null);

public record DuplicateRequest(string? NewId = null, string? Name = null);

public record LockRequest(bool Locked);

public record ConfigRequest(string? Color = null, string? Material = null);

public record StructuralRequest(string Element);

public record HistoryDto(long Id, DateTime Date, string? Author, string Description, IReadOnlyList<string> ChangedInstanceIds, int? Revision);

public record CompareDto(string A, string B, IReadOnlyList<ReasonDto> Differences, PriceRange TotalA, PriceRange TotalB);

public record SavingDto(string InstanceId, string CurrentCatalogId, string SuggestedCatalogId, string SuggestedName, decimal Saving);

public record PrologComparison(
    bool Available, bool Matches,
    string? EngineConflicts, string? PrologConflicts,
    string? EngineWarnings, string? PrologWarnings,
    string? EngineTotal, string? PrologTotal,
    string? Error = null);
