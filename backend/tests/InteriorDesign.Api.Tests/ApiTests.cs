using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InteriorDesign.Api.Tests;

/// <summary>
/// Solo corre si existe IAPROYECT_TEST_DB. Usa una BD propia (iaproyect_api_test) que se borra y recrea.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IAPROYECT_TEST_DB")))
            Skip = "Sin PostgreSQL: define IAPROYECT_TEST_DB para correr esta prueba.";
    }
}

public sealed class ApiFixture : IDisposable
{
    public WebApplicationFactory<Program>? Factory { get; }
    public HttpClient? Client { get; }

    public ApiFixture()
    {
        var baseConnection = Environment.GetEnvironmentVariable("IAPROYECT_TEST_DB");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new NpgsqlConnectionStringBuilder(baseConnection) { Database = "iaproyect_api_test" }.ConnectionString;

        var options = new DbContextOptionsBuilder<InteriorDesignDbContext>();
        InteriorDesignDbContext.Configure(options, connection);
        using (var db = new InteriorDesignDbContext(options.Options))
            db.Database.EnsureDeleted();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Default", connection);
            b.UseSetting("SeedExamples", "true");
        });
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}

/// <summary>Fase 2: la API completa contra PostgreSQL, con los datos de ejemplo d1-d4.</summary>
public class ApiTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private HttpClient Http => fx.Client!;

    private async Task<JsonNode> Get(string url)
    {
        var r = await Http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
    }

    private Task<HttpResponseMessage> Post(string url, object body) => Http.PostAsJsonAsync(url, body);

    private static List<string> Terms(JsonNode? list) => list!.AsArray().Select(x => x!["term"]!.GetValue<string>()).ToList();

    private async Task<string> Duplicate(string source)
    {
        var id = $"t_{Guid.NewGuid():N}"[..10];
        var r = await Post($"/api/designs/{source}/duplicate", new { newId = id });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return id;
    }

    // ---------- tests obligatorios del CLAUDE.md, ahora por HTTP ----------

    [PostgresFact]
    public async Task D1_a_D4_dan_el_resultado_de_Prolog()
    {
        var d1 = await Get("/api/designs/d1/validation");
        Assert.True(d1["valid"]!.GetValue<bool>());
        Assert.Equal(["presupuesto_puede_excederse"], Terms(d1["warnings"]));

        Assert.Equal(["presupuesto_excedido"], Terms((await Get("/api/designs/d2/validation"))["conflicts"]));
        Assert.Equal(["bloquea_puerta(sofa_d3,p1)"], Terms((await Get("/api/designs/d3/validation"))["conflicts"]));
        Assert.True((await Get("/api/designs/d4/validation"))["valid"]!.GetValue<bool>());
    }

    [PostgresFact]
    public async Task Propuestas_obligatorias_son_rechazadas()
    {
        async Task<string> Verdict(object body)
        {
            var r = await Post("/api/designs/d1/proposals/validate", body);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return JsonNode.Parse(await r.Content.ReadAsStringAsync())!["verdict"]!.GetValue<string>();
        }

        Assert.Equal("rechazada([elemento_bloqueado(tv_d1)])",
            await Verdict(new { action = "mover", instanceId = "tv_d1", x = 300, y = 100, rotation = 0 }));
        Assert.Equal("rechazada([elemento_bloqueado(tv_d1),rompe_requisito(tiene(televisor,pulgadas,75))])",
            await Verdict(new { action = "reemplazar", instanceId = "tv_d1", catalogId = "tv_65" }));
        Assert.Equal("rechazada([estructural_protegido(muro)])",
            await Verdict(new { action = "modificar_estructura", element = "muro" }));
    }

    [PostgresFact]
    public async Task Propuesta_rechazada_no_se_aplica()
    {
        var r = await Post("/api/designs/d1/proposals/apply", new { action = "reemplazar", instanceId = "tv_d1", catalogId = "tv_65" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var d1 = await Get("/api/designs/d1");
        Assert.Contains(d1["objects"]!.AsArray(), o => o!["instanceId"]!.GetValue<string>() == "tv_d1" && o["catalogId"]!.GetValue<string>() == "tv_75");
    }

    // ---------- versiones ----------

    [PostgresFact]
    public async Task Duplicar_mover_historial_comparar_y_revertir()
    {
        var copy = await Duplicate("d1");

        var r = await Post($"/api/designs/{copy}/proposals/apply", new { action = "mover", instanceId = "sofa_d1", x = 200, y = 110, rotation = 90 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var applied = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.True(applied["verdict"]!["accepted"]!.GetValue<bool>());
        Assert.Equal(1, applied["state"]!["design"]!["revision"]!.GetValue<int>());

        // la original no cambia (versiones independientes)
        var d1 = await Get("/api/designs/d1");
        Assert.Equal(180, d1["objects"]!.AsArray().Single(o => o!["instanceId"]!.GetValue<string>() == "sofa_d1")!["position"]!["x"]!.GetValue<int>());

        var history = (await Get($"/api/designs/{copy}/history")).AsArray();
        Assert.Equal(2, history.Count);
        Assert.Equal("mover(sofa_d1,200,110,90)", history[1]!["description"]!.GetValue<string>());

        Assert.Equal(["d1"], (await Get($"/api/designs/{copy}/ancestors")).AsArray().Select(x => x!.GetValue<string>()));

        var revert = await Http.PostAsync($"/api/designs/{copy}/history/{history[0]!["id"]}/revert", null);
        Assert.Equal(HttpStatusCode.OK, revert.StatusCode);
        var reverted = JsonNode.Parse(await revert.Content.ReadAsStringAsync())!;
        Assert.Equal(180, reverted["objects"]!.AsArray().Single(o => o!["instanceId"]!.GetValue<string>() == "sofa_d1")!["position"]!["x"]!.GetValue<int>());
        Assert.Equal(2, reverted["design"]!["revision"]!.GetValue<int>());

        var compare = await Get("/api/designs/d1/compare/d2");
        Assert.Equal(["dif(sofa,sofa_003,sofa_001)"], Terms(compare["differences"]));
    }

    [PostgresFact]
    public async Task Lote_de_movimientos_revisa_y_solo_guarda_si_todo_es_valido()
    {
        var copy = await Duplicate("d1");
        var swap = new { moves = new object[]
        {
            new { instanceId = "sofa_d1", x = 320, y = 110, rotation = 90 },
            new { instanceId = "mesa_d1", x = 180, y = 135, rotation = 90 },
        } };

        // dryRun: revisa sin guardar
        var check = JsonNode.Parse(await (await Post($"/api/designs/{copy}/proposals/moves?dryRun=true", swap)).Content.ReadAsStringAsync())!;
        Assert.True(check["accepted"]!.GetValue<bool>());
        Assert.Null(check["state"]);
        Assert.Equal(0, (await Get($"/api/designs/{copy}"))["design"]!["revision"]!.GetValue<int>());

        // Guardar: aplica los dos en un solo cambio del historial
        var saved = await Post($"/api/designs/{copy}/proposals/moves", swap);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var state = JsonNode.Parse(await saved.Content.ReadAsStringAsync())!["state"]!;
        Assert.Equal(320, state["objects"]!.AsArray().Single(o => o!["instanceId"]!.GetValue<string>() == "sofa_d1")!["position"]!["x"]!.GetValue<int>());
        Assert.Equal(2, (await Get($"/api/designs/{copy}/history")).AsArray().Count);

        // Si algo interfiere no se guarda nada
        var bad = await Post($"/api/designs/{copy}/proposals/moves",
            new { moves = new object[] { new { instanceId = "lampara_d1", x = 120, y = 345, rotation = 0 } } });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        Assert.Equal(2, (await Get($"/api/designs/{copy}/history")).AsArray().Count);
    }

    [PostgresFact]
    public async Task Crear_desde_cero_proyecto_habitacion_diseno_y_agregar_muebles()
    {
        var pid = $"proy_{Guid.NewGuid():N}"[..13];
        Assert.Equal(HttpStatusCode.Created, (await Post("/api/projects", new { id = pid, name = "Depa prueba", type = "domestico" })).StatusCode);

        var roomId = $"h_{Guid.NewGuid():N}"[..10];
        object Room(int doorPos) => new
        {
            room = new
            {
                id = roomId, spaceType = "recamara", length = 400, width = 350, height = 260,
                doors = new[] { new { id = "p1", wall = "sur", position = doorPos, width = 80 } },
                windows = new[] { new { id = "v1", wall = "este", position = 100, width = 120, sillHeight = 90 } },
                surfaces = new { floor = new { materialId = "piso_madera_001" }, walls = new { color = "beige" } }
            },
            requirements = new[] { new { id = "r1", priority = "obligatorio", type = "presupuesto_max", amount = 25000 } }
        };

        // Puerta que se sale de la pared (350 + 80 > 400): la API la rechaza
        Assert.Equal(HttpStatusCode.BadRequest, (await Post($"/api/projects/{pid}/rooms", Room(350))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post($"/api/projects/{pid}/rooms", Room(20))).StatusCode);

        var designId = $"d_{Guid.NewGuid():N}"[..10];
        var room = (await Get($"/api/rooms/{roomId}"))!;
        var created = await Post("/api/designs", new
        {
            schemaVersion = "1.0",
            project = new { id = pid, name = "Depa prueba", type = "domestico" },
            design = new { id = designId, name = "Diseno 1", style = "moderno", budgetMode = "ajustar", revision = 0 },
            room = room["room"],
            requirements = room["requirements"],
            objects = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Agregar del catalogo: el motor elige el lugar
        var cama = await Post($"/api/designs/{designId}/objects", new { catalogId = "cama_001" });
        Assert.Equal(HttpStatusCode.OK, cama.StatusCode);
        var state = JsonNode.Parse(await cama.Content.ReadAsStringAsync())!["state"]!;
        Assert.Single(state["objects"]!.AsArray());
        Assert.Equal("cama_1", state["objects"]![0]!["instanceId"]!.GetValue<string>());

        // Una isla no va en una recamara
        Assert.Equal(HttpStatusCode.Conflict, (await Post($"/api/designs/{designId}/objects", new { catalogId = "isla_001" })).StatusCode);

        // Y se puede quitar
        var quitar = await Post($"/api/designs/{designId}/proposals/apply", new { action = "quitar", instanceId = "cama_1" });
        Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        Assert.Empty((await Get($"/api/designs/{designId}"))["objects"]!.AsArray());
    }

    [PostgresFact]
    public async Task Bloquear_desbloquear_y_configurar_color()
    {
        var copy = await Duplicate("d1");

        var unlock = await Http.PatchAsJsonAsync($"/api/designs/{copy}/objects/tv_d1/lock", new { locked = false });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);
        var v = JsonNode.Parse(await (await Post($"/api/designs/{copy}/proposals/validate",
            new { action = "mover", instanceId = "tv_d1", x = 490, y = 106, rotation = 90 })).Content.ReadAsStringAsync())!;
        Assert.Equal("aceptada", v["verdict"]!.GetValue<string>());

        var badColor = await Http.PatchAsJsonAsync($"/api/designs/{copy}/objects/sofa_d1/config", new { color = "rojo" });
        Assert.Equal(HttpStatusCode.Conflict, badColor.StatusCode);
        var goodColor = await Http.PatchAsJsonAsync($"/api/designs/{copy}/objects/sofa_d1/config", new { color = "blanco" });
        Assert.Equal(HttpStatusCode.OK, goodColor.StatusCode);
    }

    [PostgresFact]
    public async Task Confirmar_estructura_permite_modificarla()
    {
        var copy = await Duplicate("d1");
        Assert.Equal(HttpStatusCode.OK, (await Post($"/api/designs/{copy}/structural-confirmations", new { element = "muro" })).StatusCode);
        var v = JsonNode.Parse(await (await Post($"/api/designs/{copy}/proposals/validate",
            new { action = "modificar_estructura", element = "muro" })).Content.ReadAsStringAsync())!;
        Assert.Equal("aceptada", v["verdict"]!.GetValue<string>());
    }

    // ---------- presupuesto, similares, catalogo ----------

    [PostgresFact]
    public async Task Presupuesto_por_rubro_y_estado()
    {
        var d1 = await Get("/api/designs/d1/budget");
        Assert.Equal("puede_excederse", d1["status"]!.GetValue<string>());
        Assert.Equal(29299m, d1["total"]!["min"]!.GetValue<decimal>());
        Assert.Equal(6500m + 2200m + 3500m, d1["byCategory"]!["muebles"]!["min"]!.GetValue<decimal>());

        var d2 = await Get("/api/designs/d2/budget");
        Assert.Equal("excedido", d2["status"]!.GetValue<string>());
        Assert.Equal(33000m, d2["effectiveCap"]!.GetValue<decimal>());
    }

    [PostgresFact]
    public async Task Similares_alternativas_y_ahorros()
    {
        // La BD tiene el catalogo de diseno.pl + el ampliado [EJEMPLO]: se revisa contenido y orden por precio
        var similar = (await Get("/api/catalog/products/sofa_001/similar")).AsArray();
        var ids = similar.Select(p => p!["id"]!.GetValue<string>()).ToList();
        Assert.Contains("sofa_003", ids);
        Assert.Contains("sofa_002", ids);
        Assert.DoesNotContain("sofa_001", ids);
        var prices = similar.Select(p => p!["price"]!["min"]!.GetValue<decimal>()).ToList();
        Assert.Equal(prices.Order(), prices);

        var savings = (await Get("/api/designs/d2/savings")).AsArray();
        Assert.Equal("sofa_002", savings[0]!["suggestedCatalogId"]!.GetValue<string>());
        Assert.Equal(4000m, savings[0]!["saving"]!.GetValue<decimal>());

        Assert.NotNull(await Get("/api/designs/d1/objects/sofa_d1/alternatives"));
    }

    // ---------- validacion de entrada ----------

    [PostgresFact]
    public async Task DesignState_invalido_o_con_productos_inexistentes_es_rechazado()
    {
        var state = await Get("/api/designs/d4");
        state["design"]!["id"] = "t_invalido";
        state["objects"]![0]!["rotation"] = 45;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/designs", state)).StatusCode);

        state["objects"]![0]!["rotation"] = 0;
        state["objects"]![0]!["catalogId"] = "no_existe";
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/designs", state)).StatusCode);
    }

    [PostgresFact]
    public async Task Verificacion_con_Prolog_coincide()
    {
        var health = await Get("/api/health");
        if (!health["prolog"]!.GetValue<bool>()) return;
        foreach (var d in new[] { "d1", "d2", "d3", "d4" })
        {
            var c = await Get($"/api/designs/{d}/verify-prolog");
            Assert.True(c["matches"]!.GetValue<bool>(), c.ToJsonString());
        }
    }
}
