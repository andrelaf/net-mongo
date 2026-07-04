using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDemo.Api.Data;
using MongoDemo.Api.Features;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuração do MongoDB
// ---------------------------------------------------------------------------
var mongoSettings = builder.Configuration.GetSection("Mongo").Get<MongoSettings>() ?? new MongoSettings();
builder.Services.AddSingleton(mongoSettings);

// Acessor de captura (singleton) — ponte para o monitoramento de comandos.
builder.Services.AddSingleton<CommandCaptureAccessor>();
// Captura por requisição (scoped).
builder.Services.AddScoped<CommandCapture>();

// MongoClient é SINGLETON (gerencia o pool de conexões).
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var accessor = sp.GetRequiredService<CommandCaptureAccessor>();
    var clientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);

    // Monitoramento de comandos: encaminha cada comando enviado ao capture da
    // requisição atual (via AsyncLocal), para exibirmos a query real no front.
    clientSettings.ClusterConfigurator = cb =>
    {
        cb.Subscribe<CommandStartedEvent>(e =>
        {
            accessor.Capture?.Record(e.CommandName, e.Command);
        });
    };

    return new MongoClient(clientSettings);
});

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<DataSeeder>();

// EF Core com o provider MongoDB. Reutiliza o MESMO IMongoClient singleton
// (monitorado) — assim o EF compartilha o pool de conexões e seus comandos
// também aparecem na captura didática.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseMongoDB(sp.GetRequiredService<IMongoClient>(), mongoSettings.Database));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors();
app.MapOpenApi();

// Middleware: liga o CommandCapture scoped ao accessor (AsyncLocal) no início
// de cada requisição e o desliga ao final.
app.Use(async (context, next) =>
{
    var accessor = context.RequestServices.GetRequiredService<CommandCaptureAccessor>();
    var capture = context.RequestServices.GetRequiredService<CommandCapture>();
    accessor.Capture = capture;
    try { await next(); }
    finally { accessor.Capture = null; }
});

app.MapHealthChecks("/health");

// Agrupa os exemplos por conceito.
var api = app.MapGroup("/api");
CatalogEndpoints.Map(api);
CrudEndpoints.Map(api);
FilterEndpoints.Map(api);
ProjectionEndpoints.Map(api);
AggregationEndpoints.Map(api);
PerformanceEndpoints.Map(api);
ModelingEndpoints.Map(api);
EfCoreEndpoints.Map(api);

// Cria índices e semeia dados no startup (best-effort: não derruba a app se o
// Mongo ainda não estiver de pé).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var ctx = scope.ServiceProvider.GetRequiredService<MongoContext>();
        await ctx.EnsureIndexesAsync();
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Falha ao inicializar o MongoDB no startup. Suba o container e reinicie.");
    }
}

app.Run();

// Necessário para o WebApplicationFactory dos testes de integração.
public partial class Program { }
