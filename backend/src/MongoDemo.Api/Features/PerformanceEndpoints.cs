using MongoDB.Bson;
using MongoDB.Driver;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// PERFORMANCE. O maior ganho no MongoDB vem de índices e de trafegar menos
/// dados. Usamos o comando explain() para PROVAR se uma query usa índice
/// (IXSCAN) ou varre a coleção inteira (COLLSCAN), e demonstramos covered query
/// e paginação por keyset.
/// </summary>
public static class PerformanceEndpoints
{
    private static readonly MongoDB.Bson.IO.JsonWriterSettings Shell = new()
    {
        Indent = true,
        OutputMode = MongoDB.Bson.IO.JsonOutputMode.Shell
    };

    // Roda o comando explain e devolve estatísticas resumidas + o plano vencedor.
    private static async Task<BsonDocument> ExplainFind(MongoContext ctx, BsonDocument filter, BsonDocument? projection = null)
    {
        var find = new BsonDocument { { "find", "products" }, { "filter", filter } };
        if (projection is not null) find.Add("projection", projection);

        var cmd = new BsonDocument { { "explain", find }, { "verbosity", "executionStats" } };
        return await ctx.Database.RunCommandAsync<BsonDocument>(cmd);
    }

    private static object Summarize(BsonDocument explain)
    {
        var stats = explain["executionStats"].AsBsonDocument;
        var winning = explain["queryPlanner"]["winningPlan"].AsBsonDocument;
        // Desce a árvore de estágios até achar o nome do estágio "de baixo".
        string Stage(BsonDocument plan)
        {
            var s = plan.GetValue("stage", "").AsString;
            var inner = plan.Contains("inputStage") ? plan["inputStage"].AsBsonDocument : null;
            return inner is null ? s : $"{s} -> {Stage(inner)}";
        }
        return new
        {
            plan = Stage(winning),
            nReturned = stats["nReturned"].ToInt32(),
            totalKeysExamined = stats["totalKeysExamined"].ToInt32(),
            totalDocsExamined = stats["totalDocsExamined"].ToInt32(),
            executionTimeMillis = stats["executionTimeMillis"].ToInt32()
        };
    }

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/performance");

        // ---- COLLSCAN x IXSCAN ----
        g.MapGet("/explain", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Performance", "driver",
                "Comparação lado a lado via explain(executionStats). A query por categoryId " +
                "usa o índice ix_category_price (IXSCAN: poucos docs examinados). A query por " +
                "regex em 'description' NÃO tem índice e faz COLLSCAN (examina TODOS os " +
                "documentos). Repare em totalDocsExamined: esse é o número que você quer baixo.",
                """
                // Query indexada (categoryId)
                var indexed = await ExplainFind(ctx, new BsonDocument("categoryId", someCategoryId));

                // Query sem índice (regex em campo não indexado) => COLLSCAN
                var scan = await ExplainFind(ctx,
                    new BsonDocument("description", new BsonDocument("$regex", "qualidade")));
                """,
                async () =>
                {
                    var catId = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                        .Project(p => p.CategoryId).Limit(1).FirstAsync();

                    var indexed = await ExplainFind(ctx, new BsonDocument("categoryId", catId));
                    var scan = await ExplainFind(ctx,
                        new BsonDocument("description", new BsonDocument("$regex", "qualidade")));

                    var result = new
                    {
                        indexed = Summarize(indexed),
                        collectionScan = Summarize(scan)
                    };
                    return (result, 2);
                }));

        // ---- Covered query ----
        g.MapGet("/covered", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Performance", "driver",
                "Covered query: quando o índice contém TODOS os campos do filtro e da " +
                "projeção, o Mongo responde direto do índice sem tocar nos documentos " +
                "(totalDocsExamined = 0). Filtramos por categoryId e projetamos apenas " +
                "price, excluindo _id — tudo coberto por ix_category_price {categoryId, price}.",
                """
                var explain = await ExplainFind(ctx,
                    filter:     new BsonDocument("categoryId", someCategoryId),
                    projection: new BsonDocument { { "price", 1 }, { "_id", 0 } });
                // Espera-se: stage IXSCAN (sem FETCH) e totalDocsExamined = 0.
                """,
                async () =>
                {
                    var catId = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                        .Project(p => p.CategoryId).Limit(1).FirstAsync();

                    var explain = await ExplainFind(ctx,
                        new BsonDocument("categoryId", catId),
                        new BsonDocument { { "price", 1 }, { "_id", 0 } });

                    var summary = Summarize(explain);
                    var result = new
                    {
                        covered = summary,
                        note = "totalDocsExamined = 0 confirma que a query foi coberta pelo índice."
                    };
                    return (result, 1);
                }));

        // ---- Paginação por keyset (range) ----
        g.MapGet("/pagination", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Performance", "driver",
                "Paginação eficiente por KEYSET (range) em vez de Skip/Limit. Skip(N) " +
                "força o servidor a percorrer e descartar N documentos — fica mais lento " +
                "quanto mais fundo você pagina. No keyset guardamos o _id do último item e " +
                "pedimos 'os próximos após esse _id', usando o índice para pular direto.",
                """
                int pageSize = 5;
                // Página 1
                var page1 = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                    .SortBy(p => p.Id).Limit(pageSize).ToListAsync();

                // Página 2: continua a partir do último _id (sem Skip!)
                var lastId = page1[^1].Id;
                var page2 = await ctx.Products.Find(p => p.Id > lastId)
                    .SortBy(p => p.Id).Limit(pageSize).ToListAsync();
                """,
                async () =>
                {
                    const int pageSize = 5;
                    var page1 = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                        .SortBy(p => p.Id).Limit(pageSize).ToListAsync();

                    var lastId = page1[^1].Id;
                    var page2 = await ctx.Products.Find(p => p.Id > lastId)
                        .SortBy(p => p.Id).Limit(pageSize).ToListAsync();

                    var result = new
                    {
                        page1 = page1.Select(Dto.Product),
                        cursor = lastId.ToString(),
                        page2 = page2.Select(Dto.Product)
                    };
                    return (result, page1.Count + page2.Count);
                }));
    }
}
