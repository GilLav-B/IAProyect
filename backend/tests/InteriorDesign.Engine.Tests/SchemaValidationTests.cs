using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Fase 1: los esquemas de /schemas deben aceptar los datos de ejemplo
/// d1-d4 y el catalogo de diseno.pl, y rechazar datos mal formados.
/// </summary>
public class SchemaValidationTests
{
    private static readonly string SchemasDir = FindSchemasDir();
    private static readonly BuildOptions Options = CreateOptions();
    private static readonly JsonSchema DesignState = Load("design-state.schema.json");
    private static readonly JsonSchema Catalog = Load("catalog.schema.json");

    private static string FindSchemasDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "schemas");
            if (File.Exists(Path.Combine(candidate, "design-state.schema.json")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("No se encontro la carpeta schemas/ del repositorio.");
    }

    private static BuildOptions CreateOptions()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        // Los esquemas referenciados se registran antes de construir los que los usan.
        options.SchemaRegistry.Register(JsonSchema.FromFile(Path.Combine(SchemasDir, "common.schema.json"), options));
        options.SchemaRegistry.Register(JsonSchema.FromFile(Path.Combine(SchemasDir, "room.schema.json"), options));
        return options;
    }

    private static JsonSchema Load(string file) =>
        JsonSchema.FromFile(Path.Combine(SchemasDir, file), Options);

    private static JsonNode Example(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(SchemasDir, "examples", file)))!;

    private static EvaluationResults Evaluate(JsonSchema schema, JsonNode instance)
    {
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };
        using var doc = JsonDocument.Parse(instance.ToJsonString());
        return schema.Evaluate(doc.RootElement, options);
    }

    private static string Errors(EvaluationResults results) =>
        string.Join(Environment.NewLine,
            (results.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation} [{e.Key}] {e.Value}")));

    // ---------- Datos validos de diseno.pl ----------

    [Theory]
    [InlineData("d1.json")]
    [InlineData("d2.json")]
    [InlineData("d3.json")]
    [InlineData("d4.json")]
    public void Ejemplos_de_diseno_pl_son_DesignState_validos(string file)
    {
        var results = Evaluate(DesignState, Example(file));
        Assert.True(results.IsValid, $"{file} no valida:{Environment.NewLine}{Errors(results)}");
    }

    [Fact]
    public void Catalogo_de_diseno_pl_es_valido()
    {
        var results = Evaluate(Catalog, Example("catalog.json"));
        Assert.True(results.IsValid, Errors(results));
    }

    // ---------- Datos invalidos: el esquema debe rechazarlos ----------

    private static JsonNode D1With(Action<JsonNode> change)
    {
        var node = Example("d1.json");
        change(node);
        return node;
    }

    public static TheoryData<string, Action<JsonNode>> DesignStateInvalido => new()
    {
        { "rotacion no multiplo de 90", n => n["objects"]![0]!["rotation"] = 45 },
        { "posicion en decimales (no cm enteros)", n => n["objects"]![0]!["position"]!["x"] = 180.5 },
        { "objeto sin instanceId", n => n["objects"]![0]!.AsObject().Remove("instanceId") },
        { "categoria inexistente", n => n["objects"]![0]!["type"] = "tele" },
        { "estilo inexistente", n => n["design"]!["style"] = "barroco" },
        { "prioridad inexistente", n => n["requirements"]![0]!["priority"] = "urgente" },
        { "presupuesto_max sin amount", n => n["requirements"]![0]!.AsObject().Remove("amount") },
        { "requisito con campo de otro tipo", n => n["requirements"]![3]!["amount"] = 100 },
        { "habitacion con largo cero", n => n["room"]!["length"] = 0 },
        { "ventana sin antepecho", n => n["room"]!["windows"]![0]!.AsObject().Remove("sillHeight") },
        { "pared inexistente", n => n["room"]!["doors"]![0]!["wall"] = "arriba" },
        { "modo de presupuesto inexistente", n => n["design"]!["budgetMode"] = "gratis" },
        { "propiedad desconocida", n => n["objects"]![0]!["inventado"] = true },
        { "estructural no permitido", n => n["structuralConfirmations"] = new JsonArray("sofa") },
        { "fecha de historial invalida", n => n["history"] = new JsonArray(new JsonObject { ["date"] = "ayer", ["description"] = "x" }) },
    };

    [Theory]
    [MemberData(nameof(DesignStateInvalido))]
    public void DesignState_invalido_es_rechazado(string caso, Action<JsonNode> change)
    {
        var results = Evaluate(DesignState, D1With(change));
        Assert.False(results.IsValid, $"Debio rechazarse: {caso}");
    }

    public static TheoryData<string, Action<JsonNode>> CatalogoInvalido => new()
    {
        { "producto conceptual con sku", n => n["products"]![19]!["sku"] = "ABC-1" },
        { "producto sin kind", n => n["products"]![0]!.AsObject().Remove("kind") },
        { "dimension negativa", n => n["products"]![0]!["dimensions"]!["width"] = -10 },
        { "precio negativo", n => n["products"]![0]!["price"]!["min"] = -1 },
        { "material inexistente", n => n["products"]![0]!["materials"] = new JsonArray("plastico") },
        { "producto sin colores", n => n["products"]![0]!["colors"] = new JsonArray() },
        { "adaptador inexistente", n => n["providers"]![0]!["adapter"] = "xml" },
    };

    [Theory]
    [MemberData(nameof(CatalogoInvalido))]
    public void Catalogo_invalido_es_rechazado(string caso, Action<JsonNode> change)
    {
        var node = Example("catalog.json");
        change(node);
        var results = Evaluate(Catalog, node);
        Assert.False(results.IsValid, $"Debio rechazarse: {caso}");
    }
}
