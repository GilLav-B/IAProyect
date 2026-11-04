namespace InteriorDesign.Engine;

/// <summary>
/// Propuesta de cambio (de la IA o del usuario). Equivale a las acciones de validar_propuesta/3.
/// </summary>
public abstract record Proposal
{
    public abstract Term ToTerm();
}

/// <summary>mover(Inst, X, Y, Rot)</summary>
public sealed record MoveProposal(string InstanceId, int X, int Y, int Rotation) : Proposal
{
    public override Term ToTerm() => Term.C("mover", Term.A(InstanceId), Term.N(X), Term.N(Y), Term.N(Rotation));
}

/// <summary>agregar(Inst, Id, X, Y, Rot)</summary>
public sealed record AddProposal(string InstanceId, string CatalogId, int X, int Y, int Rotation) : Proposal
{
    public override Term ToTerm() =>
        Term.C("agregar", Term.A(InstanceId), Term.A(CatalogId), Term.N(X), Term.N(Y), Term.N(Rotation));
}

/// <summary>reemplazar(Inst, NuevoId): conserva posicion y rotacion.</summary>
public sealed record ReplaceProposal(string InstanceId, string CatalogId) : Proposal
{
    public override Term ToTerm() => Term.C("reemplazar", Term.A(InstanceId), Term.A(CatalogId));
}

/// <summary>quitar(Inst)</summary>
public sealed record RemoveProposal(string InstanceId) : Proposal
{
    public override Term ToTerm() => Term.C("quitar", Term.A(InstanceId));
}

/// <summary>modificar_estructura(Elem): solo con confirmacion explicita del usuario.</summary>
public sealed record ModifyStructureProposal(string Element) : Proposal
{
    public override Term ToTerm() => Term.C("modificar_estructura", Term.A(Element));
}

/// <summary>Resultado de veredicto/3: aceptada o rechazada(Motivos).</summary>
public sealed record Verdict(bool Accepted, IReadOnlyList<Term> Reasons)
{
    public static Verdict Invalid { get; } = new(false, [Term.A("accion_invalida")]);

    public static Verdict From(IReadOnlyList<Term> reasons) => new(reasons.Count == 0, reasons);

    public Term ToTerm() => Accepted ? Term.A("aceptada") : Term.C("rechazada", Term.L(Reasons));

    public override string ToString() => ToTerm().ToString();
}
