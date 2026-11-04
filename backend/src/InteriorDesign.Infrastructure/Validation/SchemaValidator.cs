using System.Text.Json;
using Json.Schema;

namespace InteriorDesign.Infrastructure.Validation;

/// <summary>
/// Valida JSON contra los esquemas de /schemas (design-state, catalog, room).
/// Es la primera barrera: lo que manda la IA o el frontend debe cumplir el esquema
/// antes de pasar al Design Engine.
/// </summary>
public class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _schemas = [];

    public SchemaValidator(string schemasDir)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        foreach (var dependency in new[] { "common.schema.json", "room.schema.json" })
            options.SchemaRegistry.Register(JsonSchema.FromFile(Path.Combine(schemasDir, dependency), options));
        foreach (var name in new[] { "design-state", "catalog" })
            _schemas[name] = JsonSchema.FromFile(Path.Combine(schemasDir, $"{name}.schema.json"), options);
        // Un producto suelto se valida con la definicion interna del catalogo
        _schemas["product"] = JsonSchema.FromText(
            """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "$ref": "https://iaproyect.local/schemas/catalog.schema.json#/$defs/product" }""",
            options);
        _schemas["room"] = JsonSchema.FromText(
            """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "$ref": "https://iaproyect.local/schemas/room.schema.json" }""",
            options);
        _schemas["requirements"] = JsonSchema.FromText(
            """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "array", "items": { "$ref": "https://iaproyect.local/schemas/design-state.schema.json#/$defs/requirement" } }""",
            options);
    }

    /// <summary>Busca la carpeta schemas/ subiendo desde <paramref name="start"/>.</summary>
    public static string? FindSchemasDir(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "schemas");
            if (File.Exists(Path.Combine(candidate, "design-state.schema.json")))
                return candidate;
        }
        return null;
    }

    /// <summary>Errores de validacion (vacio = valido). schema: design-state | catalog | product | room | requirements.</summary>
    public List<string> Validate(string schema, JsonElement instance)
    {
        var results = _schemas[schema].Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        });
        if (results.IsValid) return [];
        return (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Distinct()
            .ToList();
    }

    public List<string> Validate<T>(string schema, T value, JsonSerializerOptions options) =>
        Validate(schema, JsonSerializer.SerializeToElement(value, options));
}
