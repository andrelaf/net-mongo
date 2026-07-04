using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// AGREGAÇÃO — o recurso analítico mais poderoso do MongoDB. Um pipeline é uma
/// sequência de estágios ($match, $group, $unwind, $bucket, $lookup, $facet...)
/// executada no servidor, perto dos dados. Cobrimos os padrões mais comuns.
/// </summary>
public static class AggregationEndpoints
{
    private static readonly MongoDB.Bson.IO.JsonWriterSettings Shell = new()
    {
        Indent = true,
        OutputMode = MongoDB.Bson.IO.JsonOutputMode.Shell
    };

    private static List<string> ToJson(IEnumerable<BsonDocument> docs) =>
        docs.Select(d => d.ToJson(Shell)).ToList();

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/aggregation");

        // ---- $unwind + $group: faturamento por categoria ----
        g.MapGet("/revenue-by-category", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Agregação", "driver",
                "Faturamento por categoria. Como cada pedido tem um array de linhas, " +
                "primeiro 'achatamos' com um unwind (from line in o.Lines) e então " +
                "agrupamos por categoria somando o total das linhas. Escrito em LINQ, o " +
                "driver gera o pipeline $unwind -> $group -> $sort.",
                """
                var query =
                    from o in ctx.Orders.AsQueryable()
                    from line in o.Lines            // $unwind
                    group line by line.CategoryName into g
                    select new {
                        Category = g.Key,
                        Revenue  = g.Sum(x => x.LineTotal),
                        Units    = g.Sum(x => x.Quantity),
                        Lines    = g.Count()
                    };

                var result = await query.OrderByDescending(x => x.Revenue).ToListAsync();
                """,
                async () =>
                {
                    var query =
                        from o in ctx.Orders.AsQueryable()
                        from line in o.Lines
                        group line by line.CategoryName into grp
                        select new
                        {
                            Category = grp.Key,
                            Revenue = grp.Sum(x => x.LineTotal),
                            Units = grp.Sum(x => x.Quantity),
                            Lines = grp.Count()
                        };
                    var result = await query.OrderByDescending(x => x.Revenue).ToListAsync();
                    return (result, result.Count);
                }));

        // ---- Top produtos por unidades vendidas ----
        g.MapGet("/top-products", (MongoContext ctx, CommandCapture cap, int? take) =>
            EndpointHelpers.RunExample(cap, "Agregação", "driver",
                "Ranking dos produtos mais vendidos. Unwind das linhas, group por produto " +
                "somando quantidade e receita, ordena desc e limita ao top N. Padrão " +
                "clássico de 'leaderboard' feito inteiramente no servidor.",
                """
                var query =
                    from o in ctx.Orders.AsQueryable()
                    from line in o.Lines
                    group line by line.ProductName into g
                    select new {
                        Product = g.Key,
                        Units   = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.LineTotal)
                    };

                var top = await query.OrderByDescending(x => x.Units).Take(10).ToListAsync();
                """,
                async () =>
                {
                    var n = take is > 0 and <= 50 ? take.Value : 10;
                    var query =
                        from o in ctx.Orders.AsQueryable()
                        from line in o.Lines
                        group line by line.ProductName into grp
                        select new
                        {
                            Product = grp.Key,
                            Units = grp.Sum(x => x.Quantity),
                            Revenue = grp.Sum(x => x.LineTotal)
                        };
                    var top = await query.OrderByDescending(x => x.Units).Take(n).ToListAsync();
                    return (top, top.Count);
                }));

        // ---- $bucket: histograma de faixas de preço ----
        g.MapGet("/price-buckets", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Agregação", "driver",
                "$bucket agrupa documentos em faixas (histograma). Definimos as fronteiras " +
                "de preço e, para cada faixa, contamos os produtos e calculamos a nota " +
                "média. Como $bucket não tem açúcar em LINQ, montamos o estágio como " +
                "BsonDocument — mostrando que você tem acesso a QUALQUER operador do Mongo.",
                """
                var bucket = new BsonDocument("$bucket", new BsonDocument
                {
                    { "groupBy", "$price" },
                    { "boundaries", new BsonArray { 0, 50, 100, 250, 500, 1000 } },
                    { "default", "1000+" },
                    { "output", new BsonDocument
                        {
                            { "count", new BsonDocument("$sum", 1) },
                            { "avgRating", new BsonDocument("$avg", "$ratingAvg") }
                        }
                    }
                });

                var docs = await ctx.Products.Aggregate<BsonDocument>(
                    new BsonDocument[] { bucket }).ToListAsync();
                """,
                async () =>
                {
                    var bucket = new BsonDocument("$bucket", new BsonDocument
                    {
                        { "groupBy", "$price" },
                        { "boundaries", new BsonArray { 0, 50, 100, 250, 500, 1000 } },
                        { "default", "1000+" },
                        { "output", new BsonDocument
                            {
                                { "count", new BsonDocument("$sum", 1) },
                                { "avgRating", new BsonDocument("$avg", "$ratingAvg") }
                            }
                        }
                    });
                    var docs = await ctx.Products
                        .Aggregate<BsonDocument>(new BsonDocument[] { bucket }).ToListAsync();
                    return (ToJson(docs), docs.Count);
                }));

        // ---- $lookup: join entre coleções ----
        g.MapGet("/orders-with-customer", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Agregação", "driver",
                "$lookup faz o 'join' entre pedidos e clientes (orders.customerId = " +
                "customers._id), trazendo o tier de fidelidade atual do cliente. " +
                "IMPORTANTE: $lookup é como um LEFT JOIN e pode ser caro. Em caminhos " +
                "quentes, prefira denormalizar (como já fazemos com customerName). Use " +
                "$lookup para relatórios/consultas menos frequentes.",
                """
                var pipeline = new BsonDocument[]
                {
                    new("$sort", new BsonDocument("createdAt", -1)),
                    new("$limit", 10),
                    new("$lookup", new BsonDocument
                    {
                        { "from", "customers" },
                        { "localField", "customerId" },
                        { "foreignField", "_id" },
                        { "as", "customer" }
                    }),
                    new("$project", new BsonDocument
                    {
                        { "orderNumber", 1 }, { "total", 1 }, { "status", 1 },
                        { "customerTier", new BsonDocument("$first", "$customer.loyaltyTier") }
                    })
                };
                var docs = await ctx.Orders.Aggregate<BsonDocument>(pipeline).ToListAsync();
                """,
                async () =>
                {
                    var pipeline = new BsonDocument[]
                    {
                        new("$sort", new BsonDocument("createdAt", -1)),
                        new("$limit", 10),
                        new("$lookup", new BsonDocument
                        {
                            { "from", "customers" },
                            { "localField", "customerId" },
                            { "foreignField", "_id" },
                            { "as", "customer" }
                        }),
                        new("$project", new BsonDocument
                        {
                            { "orderNumber", 1 }, { "total", 1 }, { "status", 1 },
                            { "customerTier", new BsonDocument("$first", "$customer.loyaltyTier") }
                        })
                    };
                    var docs = await ctx.Orders.Aggregate<BsonDocument>(pipeline).ToListAsync();
                    return (ToJson(docs), docs.Count);
                }));

        // ---- $facet: várias métricas em uma passada ----
        g.MapGet("/dashboard", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Agregação", "driver",
                "$facet roda vários sub-pipelines na MESMA passada pelos dados, retornando " +
                "um documento com todas as métricas — perfeito para um dashboard. Aqui: " +
                "receita/total de pedidos, pedidos por status e o ticket médio, tudo de " +
                "uma vez só.",
                """
                var facet = new BsonDocument("$facet", new BsonDocument
                {
                    { "totals", new BsonArray {
                        new BsonDocument("$group", new BsonDocument {
                            { "_id", BsonNull.Value },
                            { "revenue", new BsonDocument("$sum", "$total") },
                            { "orders", new BsonDocument("$sum", 1) },
                            { "avgTicket", new BsonDocument("$avg", "$total") } }) } },
                    { "byStatus", new BsonArray {
                        new BsonDocument("$group", new BsonDocument {
                            { "_id", "$status" },
                            { "count", new BsonDocument("$sum", 1) } }),
                        new BsonDocument("$sort", new BsonDocument("count", -1)) } }
                });
                var docs = await ctx.Orders.Aggregate<BsonDocument>(
                    new BsonDocument[] { facet }).ToListAsync();
                """,
                async () =>
                {
                    var facet = new BsonDocument("$facet", new BsonDocument
                    {
                        { "totals", new BsonArray {
                            new BsonDocument("$group", new BsonDocument {
                                { "_id", BsonNull.Value },
                                { "revenue", new BsonDocument("$sum", "$total") },
                                { "orders", new BsonDocument("$sum", 1) },
                                { "avgTicket", new BsonDocument("$avg", "$total") } }) } },
                        { "byStatus", new BsonArray {
                            new BsonDocument("$group", new BsonDocument {
                                { "_id", "$status" },
                                { "count", new BsonDocument("$sum", 1) } }),
                            new BsonDocument("$sort", new BsonDocument("count", -1)) } }
                    });
                    var docs = await ctx.Orders
                        .Aggregate<BsonDocument>(new BsonDocument[] { facet }).ToListAsync();
                    return (ToJson(docs), docs.Count);
                }));
    }
}
