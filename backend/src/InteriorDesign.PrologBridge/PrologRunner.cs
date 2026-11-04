using System.Diagnostics;
using System.Text;
using InteriorDesign.Domain.State;

namespace InteriorDesign.PrologBridge;

/// <summary>
/// Ejecuta consultas sobre diseno.pl con SWI-Prolog usando los hechos de uno o varios DesignStates.
/// Cada consulta es una meta con la variable R, ej. "conflictos(d1,R)"; la respuesta es R impreso con writeq.
/// </summary>
public class PrologRunner(string swiplPath, string repoRoot, TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(30);

    public string DisenoPath => Path.Combine(repoRoot, "diseno.pl");
    public string BridgePath => Path.Combine(repoRoot, "prolog", "bridge.pl");

    /// <summary>
    /// Busca swipl: variable SWIPL_PATH, la ruta tipica de Windows o el PATH.
    /// Devuelve null si no esta instalado.
    /// </summary>
    public static string? FindSwipl()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SWIPL_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv)) return fromEnv;
        const string windows = @"C:\Program Files\swipl\bin\swipl.exe";
        if (File.Exists(windows)) return windows;
        var exe = OperatingSystem.IsWindows() ? "swipl.exe" : "swipl";
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, exe))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Carpeta del repo (la que contiene diseno.pl), subiendo desde <paramref name="start"/>.</summary>
    public static string? FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "diseno.pl")) && File.Exists(Path.Combine(dir.FullName, "prolog", "bridge.pl")))
                return dir.FullName;
        return null;
    }

    public async Task<Dictionary<string, string>> QueryAsync(
        IReadOnlyCollection<DesignState> states, CatalogState catalog,
        IReadOnlyDictionary<string, string> queries, CancellationToken ct = default)
    {
        var facts = new StringBuilder(PrologFacts.Build(states, catalog));
        foreach (var (key, goal) in queries)
            facts.AppendLine($"consulta({Engine.Term.A(key)}, R, ({goal})).");

        var file = Path.Combine(Path.GetTempPath(), $"iaproyect_hechos_{Guid.NewGuid():N}.pl");
        await File.WriteAllTextAsync(file, facts.ToString(), new UTF8Encoding(false), ct);
        try
        {
            var psi = new ProcessStartInfo(swiplPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = repoRoot
            };
            static string Quoted(string path) => "'" + path.Replace('\\', '/').Replace("'", "\\'") + "'";
            // Las opciones van antes del archivo: lo que sigue al primer .pl se toma como argumento del script.
            foreach (var arg in new[]
                     {
                         "-q", "-g", $"consult({Quoted(BridgePath)})", "-g", $"bridge_main({Quoted(file)})",
                         "-t", "halt", DisenoPath
                     })
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar swipl.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeout);
            var stdout = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"swipl tardo mas de {_timeout.TotalSeconds} s.");
            }

            var output = await stdout;
            var errors = await stderr;
            var result = new Dictionary<string, string>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = line.IndexOf('\t');
                if (tab > 0) result[line[..tab]] = line[(tab + 1)..].TrimEnd('\r');
            }
            var missing = queries.Keys.Where(k => !result.ContainsKey(k)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Prolog no respondio {string.Join(", ", missing)}. stderr: {errors}");
            return result;
        }
        finally
        {
            File.Delete(file);
        }
    }
}
