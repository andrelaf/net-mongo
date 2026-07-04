using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// PROJEÇÃO: escolher quais campos voltam (e transformá-los). Reduzir o payload
/// é uma das otimizações mais baratas — menos bytes na rede e menos RAM no
/// servidor. Também é o que habilita "covered queries".
/// </summary>
public static class ProjectionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/projection");

        // ---- Incluir apenas alguns campos ----
        g.MapGet("/include", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Projeção", "driver",
                "Projeção tipada com .Project(p => new { ... }). Só os campos citados " +
                "trafegam. O driver gera um documento 'projection' no comando find. " +
                "Traga sempre apenas o que a tela precisa.",
                """
                var list = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                    .Project(p => new { p.Name, p.Price, p.RatingAvg })
                    .Limit(15)
                    .ToListAsync();
                """,
                async () =>
                {
                    var list = await ctx.Products.Find(FilterDefinition<Product>.Empty)
                        .Project(p => new { p.Name, p.Price, p.RatingAvg })
                        .Limit(15).ToListAsync();
                    return (list, list.Count);
                }));

        // ---- Campos calculados via pipeline ----
        g.MapGet("/computed", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Projeção", "driver",
                "Projeção com campos DERIVADOS. Via LINQ Select criamos campos que não " +
                "existem no documento: um flag de estoque e o valor total em estoque " +
                "(preço x quantidade). Isso vira um estágio $project com expressões.",
                """
                var list = await ctx.Products.AsQueryable()
                    .Select(p => new {
                        p.Name,
                        p.Price,
                        inStock = p.Stock > 0,
                        stockValue = p.Price * p.Stock
                    })
                    .Take(15)
                    .ToListAsync();
                """,
                async () =>
                {
                    var list = await ctx.Products.AsQueryable()
                        .Select(p => new
                        {
                            p.Name,
                            p.Price,
                            inStock = p.Stock > 0,
                            stockValue = p.Price * p.Stock
                        })
                        .Take(15).ToListAsync();
                    return (list, list.Count);
                }));

        // ---- $slice em array embutido ----
        g.MapGet("/slice", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Projeção", "driver",
                "Projeção com $slice: de produtos que têm reviews, retornamos o nome e " +
                "apenas as 2 primeiras avaliações — útil para 'preview' sem trazer um " +
                "array potencialmente grande. Aqui os documentos vêm como JSON cru para " +
                "você ver a forma exata projetada.",
                """
                var projection = Builders<Product>.Projection
                    .Include(p => p.Name)
                    .Slice(p => p.Reviews, 2);   // apenas as 2 primeiras reviews

                var docs = await ctx.Products
                    .Find(p => p.Reviews.Any())
                    .Project<BsonDocument>(projection)
                    .Limit(10)
                    .ToListAsync();
                """,
                async () =>
                {
                    var projection = Builders<Product>.Projection
                        .Include(p => p.Name)
                        .Slice(p => p.Reviews, 2);

                    var docs = await ctx.Products
                        .Find(Builders<Product>.Filter.SizeGt(p => p.Reviews, 0))
                        .Project<BsonDocument>(projection)
                        .Limit(10).ToListAsync();

                    var json = docs.Select(d => d.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
                    {
                        Indent = true,
                        OutputMode = MongoDB.Bson.IO.JsonOutputMode.Shell
                    })).ToList();
                    return (json, json.Count);
                }));
    }
}
