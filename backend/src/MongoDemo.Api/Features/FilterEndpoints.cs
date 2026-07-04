using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// Exemplos de FILTROS (o estágio $match / o comando find). Mostra as três
/// formas de expressar filtros no Driver: Builders, LINQ e BsonDocument, além
/// de filtros em array e busca de texto.
/// </summary>
public static class FilterEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/filters");

        // ---- FilterDefinition com Builders (fortemente tipado) ----
        g.MapGet("/builder", (MongoContext ctx, CommandCapture cap, decimal min = 0, decimal max = 0) =>
            EndpointHelpers.RunExample(cap, "Filtros", "driver",
                "Builders<Product>.Filter compõe condições de forma tipada e segura. " +
                "Aqui combinamos faixa de preço (Gte/Lte) com estoque disponível usando " +
                "o operador lógico And. É a forma canônica e refatorável de montar filtros.",
                """
                var f = Builders<Product>.Filter;
                var filter = f.And(
                    f.Gte(p => p.Price, min),
                    f.Lte(p => p.Price, max),
                    f.Gt(p => p.Stock, 0));

                var list = await ctx.Products.Find(filter)
                    .SortBy(p => p.Price)
                    .Limit(20)
                    .ToListAsync();
                """,
                async () =>
                {
                    var lo = min <= 0 ? 0 : min;
                    var hi = max <= 0 ? 500 : max;
                    var f = Builders<Product>.Filter;
                    var filter = f.And(f.Gte(p => p.Price, lo), f.Lte(p => p.Price, hi), f.Gt(p => p.Stock, 0));

                    var list = await ctx.Products.Find(filter)
                        .SortBy(p => p.Price).Limit(20).ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));

        // ---- Filtro com LINQ (AsQueryable) ----
        g.MapGet("/linq", (MongoContext ctx, CommandCapture cap, string? tag) =>
            EndpointHelpers.RunExample(cap, "Filtros", "driver",
                "O mesmo filtro escrito em LINQ via AsQueryable(). O provider LINQ do " +
                "driver traduz a expressão para um pipeline de agregação ($match). " +
                "Sintaxe familiar para quem vem de EF; mesma performance do find.",
                """
                var query =
                    from p in ctx.Products.AsQueryable()
                    where p.RatingAvg >= 4.0 && p.Tags.Contains(tag)
                    orderby p.RatingAvg descending
                    select p;

                var list = await query.Take(20).ToListAsync();
                """,
                async () =>
                {
                    var t = string.IsNullOrWhiteSpace(tag) ? "premium" : tag;
                    var query = ctx.Products.AsQueryable()
                        .Where(p => p.RatingAvg >= 4.0 && p.Tags.Contains(t))
                        .OrderByDescending(p => p.RatingAvg)
                        .Take(20);
                    var list = await query.ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));

        // ---- Filtro em array (multikey) ----
        g.MapGet("/array", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Filtros", "driver",
                "Filtro sobre arrays. AnyIn casa documentos cujo array 'tags' contenha " +
                "QUALQUER uma das tags pedidas; All exige TODAS. Esses filtros usam o " +
                "índice multikey ix_tags que criamos sobre o campo de array.",
                """
                var f = Builders<Product>.Filter;
                // documentos com pelo menos uma dessas tags:
                var anyFilter = f.AnyIn(p => p.Tags, new[] { "promo", "black-friday" });
                // documentos que tenham TODAS estas tags:
                var allFilter = f.All(p => p.Tags, new[] { "premium" });

                var anyList = await ctx.Products.Find(anyFilter).Limit(15).ToListAsync();
                """,
                async () =>
                {
                    var f = Builders<Product>.Filter;
                    var anyFilter = f.AnyIn(p => p.Tags, new[] { "promo", "black-friday" });
                    var list = await ctx.Products.Find(anyFilter).Limit(15).ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));

        // ---- Busca full-text ($text) ----
        g.MapGet("/text", (MongoContext ctx, CommandCapture cap, string? q) =>
            EndpointHelpers.RunExample(cap, "Filtros", "driver",
                "Busca full-text usando o índice de texto tx_name_desc. O operador " +
                "$text tokeniza e busca por termos; ordenamos pelo textScore (relevância). " +
                "Para busca mais rica (fuzzy, sinônimos) use o Atlas Search.",
                """
                var filter = Builders<Product>.Filter.Text("fone premium");
                var scoreProj = Builders<Product>.Projection.MetaTextScore("score");

                var list = await ctx.Products.Find(filter)
                    .Project<Product>(scoreProj)
                    .Sort(Builders<Product>.Sort.MetaTextScore("score"))
                    .Limit(10)
                    .ToListAsync();
                """,
                async () =>
                {
                    var term = string.IsNullOrWhiteSpace(q) ? "Fone" : q;
                    var filter = Builders<Product>.Filter.Text(term);
                    var list = await ctx.Products.Find(filter)
                        .Sort(Builders<Product>.Sort.MetaTextScore("textScore"))
                        .Limit(10).ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));
    }
}
