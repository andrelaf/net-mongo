using MongoDB.Bson;
using MongoDB.Driver;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// MODELAGEM na prática. Dois pontos centrais do design de dados no MongoDB:
///  1) Embedding vs Referencing — "dados acessados juntos ficam juntos".
///  2) Atomicidade em nível de documento — updates de um único documento são
///     atômicos, o que torna operadores como $inc/$push muito poderosos.
/// </summary>
public static class ModelingEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/modeling");

        // ---- Embedding vs Referencing ----
        g.MapGet("/embedding", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Modelagem", "driver",
                "Embedding vs Referencing na prática. As REVIEWS estão EMBUTIDAS no " +
                "produto: um único Find traz o produto e todas as suas avaliações — zero " +
                "joins, uma leitura. Já o CLIENTE de um pedido é REFERENCIADO: precisaríamos " +
                "de uma segunda consulta (ou $lookup). Regra: embuta relações 'contém' e " +
                "de cardinalidade limitada; referencie relações grandes/ilimitadas ou " +
                "compartilhadas. Note que o pedido guarda customerName denormalizado " +
                "justamente para evitar esse segundo acesso no caminho comum.",
                """
                // 1 leitura traz produto + reviews embutidas:
                var product = await ctx.Products.Find(p => p.Reviews.Any())
                    .SortByDescending(p => p.RatingCount).FirstAsync();

                // Referência: o pedido só tem customerId; buscar o cliente é outra query:
                var order = await ctx.Orders.Find(FilterDefinition<Order>.Empty).FirstAsync();
                var customer = await ctx.Customers.Find(c => c.Id == order.CustomerId).FirstAsync();
                """,
                async () =>
                {
                    var product = await ctx.Products
                        .Find(Builders<Product>.Filter.SizeGt(p => p.Reviews, 0))
                        .SortByDescending(p => p.RatingCount).FirstAsync();

                    var order = await ctx.Orders.Find(FilterDefinition<Order>.Empty).FirstAsync();
                    var customer = await ctx.Customers.Find(c => c.Id == order.CustomerId).FirstAsync();

                    var result = new
                    {
                        embeddedExample = new
                        {
                            product.Name,
                            reviewsReadInSameDocument = product.Reviews.Count,
                            reviews = product.Reviews.Select(r => new { r.Author, r.Rating, r.Comment })
                        },
                        referencedExample = new
                        {
                            order.OrderNumber,
                            denormalizedCustomerName = order.CustomerName,
                            resolvedCustomerTier = customer.LoyaltyTier.ToString(),
                            note = "customerName veio denormalizado no pedido; o tier exigiu uma 2ª query."
                        }
                    };
                    return (result, 1);
                }));

        // ---- Update atômico: $inc + $push (single-document transaction) ----
        g.MapPost("/atomic-update", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Modelagem", "driver",
                "Update atômico em um único documento. Adicionamos uma review com $push, " +
                "incrementamos o contador com $inc e recalculamos a média — tudo numa " +
                "operação FindOneAndUpdate atômica no servidor (sem ler-modificar-gravar no " +
                "cliente, sem race condition). Usamos um produto efêmero e o removemos ao fim.",
                """
                var update = Builders<Product>.Update
                    .Push(p => p.Reviews, novaReview)   // $push no array embutido
                    .Inc(p => p.RatingCount, 1)          // $inc atômico
                    .Set(p => p.RatingAvg, novaMedia);   // $set

                var updated = await ctx.Products.FindOneAndUpdateAsync(
                    p => p.Id == id, update,
                    new FindOneAndUpdateOptions<Product> { ReturnDocument = ReturnDocument.After });
                """,
                async () =>
                {
                    // cria produto efêmero
                    var p = new Product
                    {
                        Id = ObjectId.GenerateNewId(),
                        Sku = $"SKU-ATOMIC-{Guid.NewGuid():N}",
                        Name = "Produto Atômico (efêmero)",
                        CategoryName = "Demo",
                        Price = 10m,
                        Stock = 1,
                        RatingCount = 2,
                        RatingAvg = 4.0,
                        Reviews = new List<Review>
                        {
                            new() { Author = "a", Rating = 4, CreatedAt = DateTime.UtcNow },
                            new() { Author = "b", Rating = 4, CreatedAt = DateTime.UtcNow }
                        },
                        CreatedAt = DateTime.UtcNow
                    };
                    await ctx.Products.InsertOneAsync(p);

                    var newReview = new Review { Author = "novo", Rating = 5, Comment = "Excelente!", CreatedAt = DateTime.UtcNow };
                    var newAvg = Math.Round((p.RatingAvg * p.RatingCount + newReview.Rating) / (p.RatingCount + 1), 2);

                    var update = Builders<Product>.Update
                        .Push(x => x.Reviews, newReview)
                        .Inc(x => x.RatingCount, 1)
                        .Set(x => x.RatingAvg, newAvg);

                    var updated = await ctx.Products.FindOneAndUpdateAsync<Product>(
                        x => x.Id == p.Id, update,
                        new FindOneAndUpdateOptions<Product> { ReturnDocument = ReturnDocument.After });

                    await ctx.Products.DeleteOneAsync(x => x.Id == p.Id); // limpeza

                    var result = new
                    {
                        before = new { ratingCount = 2, ratingAvg = 4.0 },
                        after = new { ratingCount = updated.RatingCount, ratingAvg = updated.RatingAvg, reviews = updated.Reviews.Count }
                    };
                    return (result, 1);
                }));
    }
}
