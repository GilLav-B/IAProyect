namespace InteriorDesign.Infrastructure.Persistence;

/// <summary>Arma el SQL de los CHECK constraints a partir de DomainValues.</summary>
internal static class Check
{
    private static string List(IEnumerable<string> values) =>
        string.Join(", ", values.Select(v => $"'{v.Replace("'", "''")}'"));

    /// <summary>columna IN ('a', 'b', ...)</summary>
    public static string In(string column, IEnumerable<string> values) =>
        $"{column} IN ({List(values)})";

    /// <summary>columna IS NULL OR columna IN (...)</summary>
    public static string NullOrIn(string column, IEnumerable<string> values) =>
        $"{column} IS NULL OR {In(column, values)}";

    /// <summary>Todos los elementos del arreglo estan en la lista.</summary>
    public static string ArrayIn(string column, IEnumerable<string> values) =>
        $"{column} <@ ARRAY[{List(values)}]::text[]";
}
