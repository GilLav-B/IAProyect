using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InteriorDesign.Engine;

/// <summary>
/// Termino estilo Prolog (atomo, numero, compuesto o lista). El motor reporta conflictos,
/// advertencias y motivos con estos terminos para que se puedan comparar 1:1 con diseno.pl.
/// ToString() imprime igual que writeq/1 de SWI-Prolog.
/// </summary>
public abstract partial record Term
{
    public static Term A(string atom) => new Atom(atom);
    public static Term N(decimal number) => new Number(number);
    public static Term F(double number) => new Float(number);
    public static Term C(string functor, params Term[] args) => new Compound(functor, args);
    public static Term L(IEnumerable<Term> items) => new ListTerm(items.ToArray());

    /// <summary>Nombre del functor (o el atomo mismo).</summary>
    public abstract string Name { get; }

    [GeneratedRegex("^[a-z][a-zA-Z0-9_]*$")]
    private static partial Regex PlainAtom();

    internal static string QuoteAtom(string atom) =>
        PlainAtom().IsMatch(atom) || atom == "[]" ? atom : "'" + atom.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}

public sealed record Atom(string Value) : Term
{
    public override string Name => Value;
    public override string ToString() => QuoteAtom(Value);
}

public sealed record Number(decimal Value) : Term
{
    public override string Name => ToString();
    public override string ToString() => Value == decimal.Truncate(Value)
        ? decimal.Truncate(Value).ToString(CultureInfo.InvariantCulture)
        : Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record Float(double Value) : Term
{
    public override string Name => ToString();
    public override string ToString()
    {
        var s = Value.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s : s + ".0";
    }
}

public sealed record Compound(string Functor, Term[] Args) : Term
{
    public override string Name => Functor;
    public bool Equals(Compound? other) => other is not null && Functor == other.Functor && Args.SequenceEqual(other.Args);
    public override int GetHashCode() => HashCode.Combine(Functor, Args.Length);
    public override string ToString() =>
        new StringBuilder(QuoteAtom(Functor)).Append('(').Append(string.Join(",", Args.Select(a => a.ToString()))).Append(')').ToString();
}

public sealed record ListTerm(Term[] Items) : Term
{
    public override string Name => "[]";
    public bool Equals(ListTerm? other) => other is not null && Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Length;
    public override string ToString() => "[" + string.Join(",", Items.Select(i => i.ToString())) + "]";
}
