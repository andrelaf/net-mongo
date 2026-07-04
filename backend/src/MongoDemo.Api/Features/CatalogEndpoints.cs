using MongoDB.Driver;
using MongoDemo.Api.Data;

namespace MongoDemo.Api.Features;

/// <summary>
/// Endpoints utilitários: catálogo dos exemplos disponíveis (usado pelo front
/// para montar o menu) e reseed dos dados.
/// </summary>
public static class CatalogEndpoints
{
    public record ExampleInfo(string Id, string Concept, string Title, string Approach, string Route);

    public static readonly IReadOnlyList<ExampleInfo> Examples = new List<ExampleInfo>
    {
        new("crud-driver", "CRUD", "CRUD básico com o Driver", "driver", "/api/crud/driver"),
        new("crud-ef", "CRUD", "CRUD básico com EF Core", "ef-core", "/api/crud/ef"),

        new("filter-builder", "Filtros", "FilterDefinition (Builders) · Driver", "driver", "/api/filters/builder"),
        new("filter-ef", "Filtros", "Mesmo filtro · EF Core", "ef-core", "/api/ef/filter"),
        new("filter-linq", "Filtros", "Filtros com LINQ · Driver", "driver", "/api/filters/linq"),
        new("filter-array", "Filtros", "Filtro em array (multikey) · Driver", "driver", "/api/filters/array"),
        new("filter-text", "Filtros", "Busca full-text ($text) · Driver", "driver", "/api/filters/text"),

        new("projection-include", "Projeção", "Incluir/excluir campos · Driver", "driver", "/api/projection/include"),
        new("projection-ef", "Projeção", "Projeção com Select · EF Core", "ef-core", "/api/ef/projection"),
        new("projection-computed", "Projeção", "Campos calculados ($project) · Driver", "driver", "/api/projection/computed"),
        new("projection-slice", "Projeção", "Fatiar array ($slice) · Driver", "driver", "/api/projection/slice"),

        new("agg-group", "Agregação", "Faturamento por categoria ($group) · Driver", "driver", "/api/aggregation/revenue-by-category"),
        new("agg-ef", "Agregação", "GroupBy por categoria · EF Core", "ef-core", "/api/ef/aggregation"),
        new("agg-unwind", "Agregação", "Top produtos ($unwind + $group) · Driver", "driver", "/api/aggregation/top-products"),
        new("agg-bucket", "Agregação", "Faixas de preço ($bucket) · Driver", "driver", "/api/aggregation/price-buckets"),
        new("agg-lookup", "Agregação", "Join entre coleções ($lookup) · Driver", "driver", "/api/aggregation/orders-with-customer"),
        new("agg-facet", "Agregação", "Dashboard multi-métrica ($facet) · Driver", "driver", "/api/aggregation/dashboard"),

        new("perf-index", "Performance", "COLLSCAN x IXSCAN (explain)", "driver", "/api/performance/explain"),
        new("perf-covered", "Performance", "Covered query", "driver", "/api/performance/covered"),
        new("perf-pagination", "Performance", "Paginação por range (keyset)", "driver", "/api/performance/pagination"),

        new("model-embed", "Modelagem", "Embedding vs Referencing", "driver", "/api/modeling/embedding"),
        new("model-bulk", "Modelagem", "Update atômico ($inc/$push)", "driver", "/api/modeling/atomic-update"),

        new("ef-linq", "EF Core", "Consultas LINQ + AsNoTracking", "ef-core", "/api/ef/linq"),
        new("ef-owned", "EF Core", "Owned types (documentos embutidos)", "ef-core", "/api/ef/owned"),
    };

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/examples", () => Results.Ok(Examples))
           .WithName("ListExamples");

        api.MapPost("/reseed", async (DataSeeder seeder) =>
        {
            await seeder.SeedAsync(force: true);
            return Results.Ok(new { message = "Base recriada com dados de exemplo." });
        }).WithName("Reseed");

        api.MapGet("/stats", async (MongoContext ctx) =>
        {
            var products = await ctx.Products.CountDocumentsAsync(FilterDefinition<Domain.Product>.Empty);
            var orders = await ctx.Orders.CountDocumentsAsync(FilterDefinition<Domain.Order>.Empty);
            var customers = await ctx.Customers.CountDocumentsAsync(FilterDefinition<Domain.Customer>.Empty);
            var categories = await ctx.Categories.CountDocumentsAsync(FilterDefinition<Domain.Category>.Empty);
            return Results.Ok(new { products, orders, customers, categories });
        }).WithName("Stats");
    }
}
