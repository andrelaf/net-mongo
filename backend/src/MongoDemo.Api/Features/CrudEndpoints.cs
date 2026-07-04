using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.EntityFrameworkCore;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// CRUD básico comparando o Driver oficial e o EF Core sobre a mesma coleção.
/// </summary>
public static class CrudEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/crud");

        // ---- Driver: insere, lê, atualiza e apaga um produto efêmero ----
        g.MapPost("/driver", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "CRUD", "driver",
                "Ciclo completo com o Driver: InsertOne, Find, UpdateOne com operador " +
                "$set e DeleteOne. O Driver expõe operadores atômicos do MongoDB " +
                "diretamente (aqui, $set). Note que _id é um ObjectId gerado no cliente.",
                """
                var product = new Product { Sku = "SKU-DEMO", Name = "Produto Demo", Price = 99.9m, ... };
                await ctx.Products.InsertOneAsync(product);                       // CREATE

                var read = await ctx.Products.Find(p => p.Id == product.Id).FirstAsync(); // READ

                await ctx.Products.UpdateOneAsync(                                 // UPDATE ($set)
                    p => p.Id == product.Id,
                    Builders<Product>.Update.Set(p => p.Price, 79.9m).Inc(p => p.Stock, 5));

                await ctx.Products.DeleteOneAsync(p => p.Id == product.Id);        // DELETE
                """,
                async () =>
                {
                    var product = new Product
                    {
                        Id = ObjectId.GenerateNewId(),
                        Sku = $"SKU-DEMO-{Guid.NewGuid():N}",
                        Name = "Produto Demo (efêmero)",
                        Description = "Criado pelo exemplo de CRUD",
                        Price = 99.9m,
                        Stock = 10,
                        CategoryName = "Demo",
                        CreatedAt = DateTime.UtcNow
                    };
                    await ctx.Products.InsertOneAsync(product);

                    var created = await ctx.Products.Find(p => p.Id == product.Id).FirstAsync();

                    await ctx.Products.UpdateOneAsync(
                        p => p.Id == product.Id,
                        Builders<Product>.Update.Set(p => p.Price, 79.9m).Inc(p => p.Stock, 5));

                    var updated = await ctx.Products.Find(p => p.Id == product.Id).FirstAsync();

                    await ctx.Products.DeleteOneAsync(p => p.Id == product.Id);
                    var afterDelete = await ctx.Products.CountDocumentsAsync(p => p.Id == product.Id);

                    var steps = new object[]
                    {
                        new { step = "created", price = created.Price, stock = created.Stock },
                        new { step = "updated", price = updated.Price, stock = updated.Stock },
                        new { step = "deletedRemaining", count = afterDelete }
                    };
                    return (steps, steps.Length);
                }));

        // ---- EF Core: mesmo ciclo com change tracking + SaveChanges ----
        g.MapPost("/ef", (AppDbContext db, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "CRUD", "ef-core",
                "Ciclo completo com EF Core: Add + SaveChanges, consulta LINQ, mutação " +
                "da entidade rastreada + SaveChanges (o EF gera o update), Remove + " +
                "SaveChanges. O change tracking detecta o que mudou automaticamente.",
                """
                var product = new Product { Sku = "SKU-EF", Name = "Produto EF", Price = 50m };
                db.Products.Add(product);
                await db.SaveChangesAsync();                                     // CREATE

                var read = await db.Products.FirstAsync(p => p.Id == product.Id);// READ
                read.Price = 45m;                                                // muda a entidade rastreada
                await db.SaveChangesAsync();                                     // UPDATE (gerado pelo EF)

                db.Products.Remove(read);
                await db.SaveChangesAsync();                                     // DELETE
                """,
                async () =>
                {
                    var product = new Product
                    {
                        Id = ObjectId.GenerateNewId(),
                        Sku = $"SKU-EF-{Guid.NewGuid():N}",
                        Name = "Produto EF (efêmero)",
                        Price = 50m,
                        Stock = 3,
                        CategoryName = "Demo",
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Products.Add(product);
                    await db.SaveChangesAsync();

                    var read = await db.Products.FirstAsync(p => p.Id == product.Id);
                    read.Price = 45m;
                    await db.SaveChangesAsync();

                    db.Products.Remove(read);
                    await db.SaveChangesAsync();

                    var remaining = await db.Products.CountAsync(p => p.Id == product.Id);

                    var steps = new object[]
                    {
                        new { step = "created", price = 50m },
                        new { step = "updated", price = 45m },
                        new { step = "deletedRemaining", count = remaining }
                    };
                    return (steps, steps.Length);
                }));
    }
}
