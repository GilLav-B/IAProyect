using Npgsql;

namespace InteriorDesign.Infrastructure.Persistence;

public static class ConnectionStrings
{
    /// <summary>
    /// Arma la cadena de conexion con POSTGRES_HOST, POSTGRES_PORT, POSTGRES_DB,
    /// POSTGRES_USER y POSTGRES_PASSWORD. Nunca lleva claves escritas en codigo.
    /// </summary>
    public static string FromEnvironment()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Env("POSTGRES_HOST") ?? "localhost",
            Port = int.TryParse(Env("POSTGRES_PORT"), out var port) ? port : 5432,
            Database = Env("POSTGRES_DB") ?? "iaproyect",
            Username = Env("POSTGRES_USER"),
            Password = Env("POSTGRES_PASSWORD")
        };
        return builder.ConnectionString;
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
