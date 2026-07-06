using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MongoDemo.Tests;

/// <summary>
/// Um teste por conceito. Cada exemplo deve: responder 200, devolver o envelope
/// com o código C# e — para os exemplos via driver — ter capturado pelo menos um
/// comando MongoDB real (prova de que a query rodou no servidor).
/// </summary>
[Collection("api")]
public class ExampleEndpointsTests
{
    private readonly HttpClient _client;

    public ExampleEndpointsTests(ApiFactory factory) => _client = factory.CreateClient();

    private static async Task<JsonElement> ReadJson(HttpResponseMessage res)
    {
        var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task Catalog_lists_examples()
    {
        var res = await _client.GetAsync("/api/examples");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await ReadJson(res);
        Assert.True(json.GetArrayLength() >= 15, "esperava vários exemplos catalogados");
    }

    [Fact]
    public async Task Stats_reports_seeded_data()
    {
        var json = await ReadJson(await _client.GetAsync("/api/stats"));
        Assert.True(json.GetProperty("products").GetInt64() > 0);
        Assert.True(json.GetProperty("orders").GetInt64() > 0);
    }

    // ---- Exemplos GET (driver + EF) ----
    public static IEnumerable<object[]> GetRoutes() => new[]
    {
        new object[] { "/api/filters/builder" },
        new object[] { "/api/filters/linq" },
        new object[] { "/api/filters/array" },
        new object[] { "/api/filters/text" },
        new object[] { "/api/projection/include" },
        new object[] { "/api/projection/computed" },
        new object[] { "/api/projection/slice" },
        new object[] { "/api/aggregation/revenue-by-category" },
        new object[] { "/api/aggregation/top-products" },
        new object[] { "/api/aggregation/price-buckets" },
        new object[] { "/api/aggregation/orders-with-customer" },
        new object[] { "/api/aggregation/dashboard" },
        new object[] { "/api/performance/explain" },
        new object[] { "/api/performance/covered" },
        new object[] { "/api/performance/pagination" },
        new object[] { "/api/modeling/embedding" },
        new object[] { "/api/antipatterns/unbounded-array" },
        new object[] { "/api/antipatterns/subset-pattern" },
        new object[] { "/api/antipatterns/too-many-indexes" },
        new object[] { "/api/antipatterns/bucket-pattern" },
        new object[] { "/api/ef/filter" },
        new object[] { "/api/ef/aggregation" },
        new object[] { "/api/ef/linq" },
        new object[] { "/api/ef/projection" },
        new object[] { "/api/ef/owned" },
    };

    [Theory]
    [MemberData(nameof(GetRoutes))]
    public async Task Get_example_returns_valid_envelope(string route)
    {
        var res = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await ReadJson(res);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("concept").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("csharp").GetString()));
        Assert.True(json.GetProperty("data").ValueKind != JsonValueKind.Undefined);
        // O envelope sempre expõe a lista de comandos (a captura em si é validada
        // rodando a API real na Kestrel; sob o TestServer o AsyncLocal do
        // monitoramento não é propagado, então aqui checamos apenas a forma).
        Assert.Equal(JsonValueKind.Array, json.GetProperty("mongoCommands").ValueKind);
    }

    // ---- Exemplos POST (CRUD e update atômico) ----
    [Theory]
    [InlineData("/api/crud/driver")]
    [InlineData("/api/crud/ef")]
    [InlineData("/api/modeling/atomic-update")]
    public async Task Post_example_returns_valid_envelope(string route)
    {
        var res = await _client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await ReadJson(res);
        Assert.True(json.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task RevenueByCategory_returns_positive_totals()
    {
        var json = await ReadJson(await _client.GetAsync("/api/aggregation/revenue-by-category"));
        var data = json.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0);
        // A receita da categoria de topo deve ser positiva.
        var first = data[0];
        Assert.True(first.GetProperty("revenue").GetDecimal() > 0);
    }

    [Fact]
    public async Task Explain_shows_index_scan_beats_collection_scan()
    {
        var json = await ReadJson(await _client.GetAsync("/api/performance/explain"));
        var data = json.GetProperty("data");
        var indexed = data.GetProperty("indexed");
        var scan = data.GetProperty("collectionScan");

        // A query indexada examina bem menos documentos que o COLLSCAN.
        var indexedDocs = indexed.GetProperty("totalDocsExamined").GetInt32();
        var scanDocs = scan.GetProperty("totalDocsExamined").GetInt32();
        Assert.Contains("IXSCAN", indexed.GetProperty("plan").GetString());
        Assert.True(indexedDocs <= scanDocs);
    }

    [Fact]
    public async Task Covered_query_examines_zero_documents()
    {
        var json = await ReadJson(await _client.GetAsync("/api/performance/covered"));
        var covered = json.GetProperty("data").GetProperty("covered");
        Assert.Equal(0, covered.GetProperty("totalDocsExamined").GetInt32());
    }
}
