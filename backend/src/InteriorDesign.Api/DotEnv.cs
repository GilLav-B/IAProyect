namespace InteriorDesign.Api;

/// <summary>
/// Lee el archivo .env de la raiz del repo (si existe) y pone sus valores como variables de entorno.
/// No pisa variables que ya esten definidas. El .env nunca se sube a git.
/// </summary>
public static class DotEnv
{
    public static string? Load(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(file)) continue;
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(key) is null && value.Length > 0)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return file;
        }
        return null;
    }
}
