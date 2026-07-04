using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;

namespace MongoDemo.Tests;

/// <summary>
/// Sobe um MongoDB real e efêmero via Testcontainers (Docker) e injeta a sua
/// connection string na API pela configuração. Assim os testes exercitam o
/// caminho completo — driver + provider EF — contra um Mongo de verdade, sem
/// depender de nada instalado na máquina além do Docker.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:8")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting tem precedência sobre appsettings.json.
        builder.UseSetting("Mongo:ConnectionString", _mongo.GetConnectionString());
        builder.UseSetting("Mongo:Database", "shoptest");
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync() => await _mongo.StartAsync();

    public new async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
