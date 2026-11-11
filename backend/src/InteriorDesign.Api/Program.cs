using System.Text.Json.Serialization;
using InteriorDesign.Api;
using InteriorDesign.Api.Endpoints;
using InteriorDesign.Api.Services;
using InteriorDesign.Engine;
using InteriorDesign.Infrastructure.Persistence;
using InteriorDesign.Infrastructure.Seed;
using InteriorDesign.Infrastructure.Validation;
using InteriorDesign.PrologBridge;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

DotEnv.Load(AppContext.BaseDirectory);
DotEnv.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

var connection = builder.Configuration.GetConnectionString("Default") ?? ConnectionStrings.FromEnvironment();
var schemasDir = builder.Configuration["SchemasPath"]
    ?? SchemaValidator.FindSchemasDir(AppContext.BaseDirectory)
    ?? SchemaValidator.FindSchemasDir(Directory.GetCurrentDirectory())
    ?? throw new DirectoryNotFoundException("No se encontro la carpeta schemas/. Define SchemasPath.");

builder.Services.AddDbContext<InteriorDesignDbContext>(o => InteriorDesignDbContext.Configure(o, connection));
builder.Services.AddScoped<DesignRepository>();
builder.Services.AddScoped<DesignService>();
builder.Services.AddSingleton(new SchemaValidator(schemasDir));
builder.Services.AddSingleton(Rules.Default);
builder.Services.AddSingleton(sp =>
{
    var swipl = PrologRunner.FindSwipl();
    var root = PrologRunner.FindRepoRoot(AppContext.BaseDirectory) ?? PrologRunner.FindRepoRoot(Directory.GetCurrentDirectory());
    var runner = swipl is not null && root is not null ? new PrologRunner(swipl, root) : null;
    return new PrologVerifier(runner, sp.GetRequiredService<ILogger<PrologVerifier>>());
});

// IA: n8n orquesta los trabajos, el servicio de IA en Python (ai-service) hace la IA y la API valida con el motor
var repoRoot = Directory.GetParent(schemasDir)!.FullName;
var storageSetting = builder.Configuration["STORAGE_PATH"] ?? "./storage";
var storageDir = Path.GetFullPath(Path.IsPathRooted(storageSetting) ? storageSetting : Path.Combine(repoRoot, storageSetting));
Directory.CreateDirectory(storageDir);
var aiOptions = new AiOptions(
    N8nUrl: builder.Configuration["N8N_URL"] ?? "http://localhost:5678",
    CallbackBaseUrl: builder.Configuration["API_URL"] ?? "http://localhost:5000",
    CallbackSecret: builder.Configuration["N8N_CALLBACK_SECRET"],
    StorageDir: storageDir,
    DispatchEnabled: builder.Configuration.GetValue("AiDispatchEnabled", true),
    AiServiceUrl: builder.Configuration["AI_SERVICE_URL"] ?? "http://127.0.0.1:8100");
builder.Services.AddSingleton(aiOptions);
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<AiJobService>();
builder.Services.AddHttpClient();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddProblemDetails();

var app = builder.Build();

// Migraciones al arrancar y datos de ejemplo (d1-d4 de diseno.pl) solo si la BD esta vacia
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InteriorDesignDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("SeedExamples", app.Environment.IsDevelopment()))
    {
        if (await ExampleSeeder.SeedIfEmptyAsync(db, schemasDir))
            app.Logger.LogInformation("Se cargaron los datos de ejemplo (catalogo y d1-d4 de diseno.pl).");
        var (added, assets) = await ExampleSeeder.SyncExampleCatalogAsync(db, schemasDir);
        if (added + assets > 0)
            app.Logger.LogInformation("Catalogo de ejemplo: {Added} productos agregados, {Assets} modelos 3D asignados.", added, assets);
    }
    var prolog = scope.ServiceProvider.GetRequiredService<PrologVerifier>();
    app.Logger.LogInformation("Verificador Prolog: {Estado}", prolog.Available ? "disponible" : "NO disponible (instala SWI-Prolog)");
}

app.UseExceptionHandler();
app.UseCors();
// Imagenes generadas y referencias subidas (storage/ esta fuera de git)
// .blend no es un tipo conocido: se agrega para poder descargar el archivo de Blender
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".blend"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(storageDir), RequestPath = "/files", ContentTypeProvider = contentTypes });
app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "IAProyect API");
    o.DocumentTitle = "IAProyect - API de diseno de interiores";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/api/health", (PrologVerifier prolog) => new { status = "ok", prolog = prolog.Available }).WithTags("Sistema");
app.MapCatalog();
app.MapProjects();
app.MapDesigns();
app.MapAi();
app.MapExports();

app.Run();

/// <summary>Visible para las pruebas de integracion (WebApplicationFactory).</summary>
public partial class Program;
