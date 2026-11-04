using InteriorDesign.Domain.State;
using InteriorDesign.Engine;
using InteriorDesign.PrologBridge;

namespace InteriorDesign.Api.Services;

/// <summary>
/// Verificador en ejecucion: corre lo mismo en diseno.pl (SWI-Prolog) y lo compara con el motor en C#.
/// El motor en C# siempre es el que decide; si Prolog difiere solo se registra en el log.
/// </summary>
public class PrologVerifier(PrologRunner? runner, ILogger<PrologVerifier> logger)
{
    public bool Available => runner is not null;

    private static string Str(IEnumerable<Term> t) => Term.L(t).ToString();

    // Prolog imprime 45998.0 cuando suma flotantes; vale lo mismo que 45998
    private static string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"(\d+)\.0\b", "$1");

    public async Task<PrologComparison> CompareAsync(DesignState s, DesignEngine engine, CancellationToken ct = default)
    {
        if (runner is null)
            return new PrologComparison(false, false, null, null, null, null, null, null, "SWI-Prolog no esta instalado (SWIPL_PATH).");
        var d = Term.A(s.Design.Id).ToString();
        var total = engine.BudgetTotal(s);
        var mine = new Dictionary<string, string>
        {
            ["c"] = Str(engine.Conflicts(s)),
            ["w"] = Str(engine.Warnings(s)),
            ["t"] = $"{Term.N(total.Min)}/{Term.N(total.Max)}"
        };
        try
        {
            var theirs = await runner.QueryAsync([s], engine.Catalog, new Dictionary<string, string>
            {
                ["c"] = $"conflictos({d},R)",
                ["w"] = $"advertencias({d},R)",
                ["t"] = $"presupuesto_total({d},Mi,Ma), R = Mi/Ma"
            }, ct);
            var matches = mine.All(kv => Normalize(kv.Value) == Normalize(theirs[kv.Key]));
            if (!matches)
                logger.LogWarning("Prolog difiere del motor en {Design}: C# {Mine} / Prolog {Theirs}", s.Design.Id, mine, theirs);
            return new PrologComparison(true, matches, mine["c"], theirs["c"], mine["w"], theirs["w"], mine["t"], theirs["t"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo la verificacion con Prolog para {Design}", s.Design.Id);
            return new PrologComparison(true, false, mine["c"], null, mine["w"], null, mine["t"], null, ex.Message);
        }
    }

    public async Task<PrologCheck?> VerdictAsync(DesignState s, DesignEngine engine, Proposal proposal, Verdict mine, CancellationToken ct = default)
    {
        if (runner is null) return null;
        try
        {
            var r = await runner.QueryAsync([s], engine.Catalog, new Dictionary<string, string>
            {
                ["v"] = $"veredicto({Term.A(s.Design.Id)},{proposal.ToTerm()},R)"
            }, ct);
            var matches = r["v"] == mine.ToString();
            if (!matches)
                logger.LogWarning("Prolog difiere en {Proposal}: C# {Mine} / Prolog {Theirs}", proposal.ToTerm(), mine, r["v"]);
            return new PrologCheck(r["v"], matches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo la verificacion con Prolog de {Proposal}", proposal.ToTerm());
            return new PrologCheck($"error: {ex.Message}", false);
        }
    }
}
