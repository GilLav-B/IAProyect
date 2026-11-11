using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using InteriorDesign.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InteriorDesign.Api.Tests;

/// <summary>Fase 5: ciclo de un trabajo de IA. n8n se simula llamando a /progress y /result.</summary>
[Collection("api")]
public class AiJobTests(ApiFixture fx)
{
    private HttpClient Http => fx.Client!;

    private async Task<string> Duplicate(string source)
    {
        var id = $"t_{Guid.NewGuid():N}"[..10];
        var r = await Http.PostAsJsonAsync($"/api/designs/{source}/duplicate", new { newId = id });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return id;
    }

    private async Task<JsonNode> Json(HttpResponseMessage r) => JsonNode.Parse(await r.Content.ReadAsStringAsync())!;

    private Task<HttpResponseMessage> Callback(string url, object body, string? secret = ApiFixture.CallbackSecret)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (secret is not null) msg.Headers.Add("X-Callback-Secret", secret);
        return Http.SendAsync(msg);
    }

    private static object AiAnswer(params object[] proposals) => new
    {
        ok = true,
        data = new { summary = "prueba", style = "japandi", proposals },
    };

    [PostgresFact]
    public async Task Chat_simula_y_solo_aplica_lo_que_acepta_el_motor()
    {
        var id = await Duplicate("d1");
        var start = await Http.PostAsJsonAsync($"/api/designs/{id}/ai/chat", new { message = "quita la lampara y la tv" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await Json(start))["id"]!.GetValue<string>();
        Assert.Equal("pendiente", (await Json(start))["status"]!.GetValue<string>());

        // Sin secreto o con secreto incorrecto: rechazado
        Assert.Equal(HttpStatusCode.Unauthorized, (await Callback($"/api/ai/jobs/{jobId}/progress", new { progress = 10 }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Callback($"/api/ai/jobs/{jobId}/progress", new { progress = 10 }, "otro")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Callback($"/api/ai/jobs/{jobId}/progress", new { progress = 40, message = "Pensando" })).StatusCode);
        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("en_proceso", job["status"]!.GetValue<string>());
        Assert.Equal(40, job["progress"]!.GetValue<int>());

        var result = await Callback($"/api/ai/jobs/{jobId}/result", AiAnswer(
            new { action = "quitar", instanceId = "lampara_d1", reason = "pidio quitarla" },
            new { action = "quitar", instanceId = "tv_d1", reason = "pidio quitarla" }));
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);

        job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("completado", job["status"]!.GetValue<string>());
        var outcomes = job["result"]!["outcomes"]!.AsArray();
        Assert.True(outcomes[0]!["applied"]!.GetValue<bool>());
        Assert.False(outcomes[1]!["applied"]!.GetValue<bool>()); // tv bloqueada
        Assert.Equal(1, job["result"]!["applicable"]!.GetValue<int>());

        // Nada cambia hasta que el usuario aplica
        var before = await Json(await Http.GetAsync($"/api/designs/{id}"));
        Assert.Contains(before["objects"]!.AsArray(), o => o!["instanceId"]!.GetValue<string>() == "lampara_d1");

        var apply = await Http.PostAsync($"/api/ai/jobs/{jobId}/apply", null);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var applied = await Json(apply);
        Assert.Equal(1, applied["applied"]!.GetValue<int>());
        var objects = applied["state"]!["objects"]!.AsArray().Select(o => o!["instanceId"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("lampara_d1", objects);
        Assert.Contains("tv_d1", objects);
    }

    [PostgresFact]
    public async Task Referencia_crea_version_nueva_y_no_toca_la_original()
    {
        var id = await Duplicate("d4");
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        image.Headers.ContentType = new("image/png");
        form.Add(image, "image", "ref.png");
        form.Add(new StringContent("estilo japandi"), "note");
        var start = await Http.PostAsync($"/api/designs/{id}/ai/reference", form);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await Json(start))["id"]!.GetValue<string>();

        await Callback($"/api/ai/jobs/{jobId}/result", AiAnswer(
            new { action = "agregar", catalogId = "planta_001", reason = "como en la foto" },
            new { action = "agregar", catalogId = "sofa_001", reason = "no va en recamara" }));

        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("completado", job["status"]!.GetValue<string>());
        var newId = job["resultDesignId"]!.GetValue<string>();
        Assert.Equal(1, job["result"]!["applied"]!.GetValue<int>());

        var created = await Json(await Http.GetAsync($"/api/designs/{newId}"));
        Assert.Contains(created["objects"]!.AsArray(), o => o!["catalogId"]!.GetValue<string>() == "planta_001");
        Assert.Equal("japandi", created["design"]!["style"]!.GetValue<string>());
        var original = await Json(await Http.GetAsync($"/api/designs/{id}"));
        Assert.DoesNotContain(original["objects"]!.AsArray(), o => o!["catalogId"]!.GetValue<string>() == "planta_001");
    }

    [PostgresFact]
    public async Task Render_desde_captura_guarda_la_imagen_con_su_leyenda()
    {
        var id = await Duplicate("d1");
        using var form = new MultipartFormDataContent();
        var capture = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        capture.Headers.ContentType = new("image/png");
        form.Add(capture, "image", "vista.png");
        form.Add(new StringContent("de noche"), "note");
        form.Add(new StringContent("lote1"), "batch");
        form.Add(new StringContent("2"), "view");
        var start = await Http.PostAsync($"/api/designs/{id}/ai/render", form);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await Json(start))["id"]!.GetValue<string>();

        // El prompt pide conservar el acomodo y lleva los muebles del diseno
        var started = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("imagen", started["type"]!.GetValue<string>());
        Assert.Equal("render", started["mode"]!.GetValue<string>());
        Assert.Equal("lote1", started["batch"]!.GetValue<string>());
        Assert.Equal(2, started["view"]!.GetValue<int>());

        var png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Assert.Equal(HttpStatusCode.NoContent, (await Callback($"/api/ai/jobs/{jobId}/result", new { ok = true, data = new { b64 = png } })).StatusCode);
        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("completado", job["status"]!.GetValue<string>());
        var url = job["result"]!["imageUrl"]!.GetValue<string>();
        Assert.Equal("Imagen de referencia generada por IA. No representa medidas ni productos exactos.", job["result"]!["caption"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await Http.GetAsync(url)).StatusCode);

        // La lista de lo visible se guarda con el trabajo (el prompt lo arma el servicio de IA en Python)
        using var form2 = new MultipartFormDataContent();
        var capture2 = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        capture2.Headers.ContentType = new("image/png");
        form2.Add(capture2, "image", "vista.png");
        form2.Add(new StringContent("""[{"instanceId":"sofa_d1","where":"al centro, a media distancia"}]"""), "visible");
        var start2 = await Http.PostAsync($"/api/designs/{id}/ai/render", form2);
        Assert.Equal(HttpStatusCode.Accepted, start2.StatusCode);
        var job2Id = (await Json(start2))["id"]!.GetValue<string>();
        await using (var scope = fx.Factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InteriorDesignDbContext>();
            var input = JsonNode.Parse((await db.AiJobs.SingleAsync(j => j.Id == job2Id)).InputJson!)!;
            Assert.Equal("render", input["mode"]!.GetValue<string>());
            var visible = input["visible"]!.AsArray();
            Assert.Equal("sofa_d1", Assert.Single(visible)!["instanceId"]!.GetValue<string>());
            Assert.Equal("al centro, a media distancia", visible[0]!["where"]!.GetValue<string>());
            Assert.Null(input["prompt"]);
        }

        // Sin muebles no hay render
        var empty = await Http.PostAsJsonAsync("/api/designs/x/ai/render", new { });
        Assert.NotEqual(HttpStatusCode.Accepted, empty.StatusCode);
    }

    [PostgresFact]
    public async Task Disenar_con_IA_vacia_lo_no_obligatorio_y_crea_version_nueva()
    {
        var id = await Duplicate("d1");
        var start = await Http.PostAsJsonAsync($"/api/designs/{id}/ai/design", new { style = "japandi", note = "calido" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await Json(start))["id"]!.GetValue<string>();
        Assert.Equal("diseno", (await Json(start))["type"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.PostAsJsonAsync($"/api/designs/{id}/ai/design", new { style = "gotico" })).StatusCode);

        await Callback($"/api/ai/jobs/{jobId}/result", new
        {
            ok = true,
            data = new
            {
                summary = "sala japandi",
                style = "moderno", // la IA no decide el estilo: se usa el que pidio el usuario
                proposals = new object[]
                {
                    new { action = "agregar", catalogId = "planta_001", reason = "verde" },
                    new { action = "agregar", catalogId = "cama_001", reason = "no va en sala" },
                },
            },
        });
        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("completado", job["status"]!.GetValue<string>());
        Assert.Equal(1, job["result"]!["applied"]!.GetValue<int>());
        var created = await Json(await Http.GetAsync($"/api/designs/{job["resultDesignId"]!.GetValue<string>()}"));
        Assert.Equal("japandi", created["design"]!["style"]!.GetValue<string>());
        var ids = created["objects"]!.AsArray().Select(o => o!["instanceId"]!.GetValue<string>()).ToList();
        Assert.Contains("tv_d1", ids);         // bloqueada y obligatoria: se queda
        Assert.DoesNotContain("sofa_d1", ids); // se quito para disenar desde cero
        Assert.Contains(created["objects"]!.AsArray(), o => o!["catalogId"]!.GetValue<string>() == "planta_001");
        // La original no se toca
        var original = await Json(await Http.GetAsync($"/api/designs/{id}"));
        Assert.Contains(original["objects"]!.AsArray(), o => o!["instanceId"]!.GetValue<string>() == "sofa_d1");
    }

    [PostgresFact]
    public async Task Blender_guarda_las_rutas_como_urls_de_archivos()
    {
        var id = await Duplicate("d1");
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.PostAsJsonAsync($"/api/designs/{id}/blender", new { views = "arriba" })).StatusCode);
        var start = await Http.PostAsJsonAsync($"/api/designs/{id}/blender", new { views = "all", quality = "alta" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await Json(start))["id"]!.GetValue<string>();
        await Callback($"/api/ai/jobs/{jobId}/result", new
        {
            ok = true,
            data = new
            {
                blend = $"blender/{jobId}/{id}.blend",
                images = new[] { new { view = "se", path = $"blender/{jobId}/{id}_se.png", seconds = 12.5 } },
                seconds = 20.1,
            },
        });
        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("completado", job["status"]!.GetValue<string>());
        Assert.Equal($"/files/blender/{jobId}/{id}.blend", job["result"]!["blendUrl"]!.GetValue<string>());
        Assert.Equal($"/files/blender/{jobId}/{id}_se.png", job["result"]!["images"]![0]!["url"]!.GetValue<string>());

        // Una ruta con ".." no se acepta
        var start2 = await Http.PostAsJsonAsync($"/api/designs/{id}/blender", new { });
        var job2Id = (await Json(start2))["id"]!.GetValue<string>();
        await Callback($"/api/ai/jobs/{job2Id}/result", new { ok = true, data = new { blend = "../../secreto", images = Array.Empty<object>(), seconds = 1 } });
        Assert.Equal("fallido", (await Json(await Http.GetAsync($"/api/ai/jobs/{job2Id}")))["status"]!.GetValue<string>());
    }

    [PostgresFact]
    public async Task Listas_de_compras_y_materiales()
    {
        var lists = await Json(await Http.GetAsync("/api/designs/d1/lists"));
        Assert.Equal(0.10m, lists["wasteFactor"]!.GetValue<decimal>());
        var material = Assert.Single(lists["materials"]!.AsArray())!;
        Assert.Equal(44, material["m2WithWaste"]!.GetValue<decimal>());
        Assert.Contains(lists["shopping"]!.AsArray(), i => i!["catalogId"]!.GetValue<string>() == "tv_75");
    }

    [PostgresFact]
    public async Task Exportar_sin_servicio_de_IA_da_503_y_formato_invalido_400()
    {
        // En las pruebas AI_SERVICE_URL apunta a un puerto sin servicio
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/api/designs/d1/export/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.GetAsync("/api/designs/d1/export/docx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Http.GetAsync("/api/designs/no_existe/export/pdf")).StatusCode);
    }

    [PostgresFact]
    public async Task Error_de_la_IA_deja_el_trabajo_fallido()
    {
        var id = await Duplicate("d1");
        var start = await Http.PostAsJsonAsync($"/api/designs/{id}/ai/chat", new { message = "algo" });
        var jobId = (await Json(start))["id"]!.GetValue<string>();
        await Callback($"/api/ai/jobs/{jobId}/result", new { ok = false, error = "OpenAI 429" });
        var job = await Json(await Http.GetAsync($"/api/ai/jobs/{jobId}"));
        Assert.Equal("fallido", job["status"]!.GetValue<string>());
        Assert.Equal("OpenAI 429", job["error"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.PostAsync($"/api/ai/jobs/{jobId}/apply", null)).StatusCode);
    }
}
