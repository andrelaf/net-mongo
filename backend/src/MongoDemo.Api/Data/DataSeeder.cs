using MongoDB.Bson;
using MongoDB.Driver;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Data;

/// <summary>
/// Popula o banco com dados de exemplo determinísticos (mesma seed => mesmos
/// dados) para que os exemplos sejam reproduzíveis. Idempotente: só semeia se a
/// coleção de produtos estiver vazia.
/// </summary>
public class DataSeeder
{
    private readonly MongoContext _ctx;
    private readonly Random _rng = new(20260630); // seed fixa => reproduzível

    public DataSeeder(MongoContext ctx) => _ctx = ctx;

    public async Task SeedAsync(bool force = false)
    {
        if (force)
        {
            await _ctx.Products.DeleteManyAsync(FilterDefinition<Product>.Empty);
            await _ctx.Categories.DeleteManyAsync(FilterDefinition<Category>.Empty);
            await _ctx.Customers.DeleteManyAsync(FilterDefinition<Customer>.Empty);
            await _ctx.Orders.DeleteManyAsync(FilterDefinition<Order>.Empty);
        }

        if (await _ctx.Products.CountDocumentsAsync(FilterDefinition<Product>.Empty) > 0)
            return;

        var categories = SeedCategories();
        await _ctx.Categories.InsertManyAsync(categories);

        var products = SeedProducts(categories);
        await _ctx.Products.InsertManyAsync(products);

        var customers = SeedCustomers();
        await _ctx.Customers.InsertManyAsync(customers);

        var orders = SeedOrders(customers, products);
        await _ctx.Orders.InsertManyAsync(orders);
    }

    private List<Category> SeedCategories()
    {
        string[] names = { "Eletrônicos", "Livros", "Casa & Cozinha", "Esportes", "Brinquedos" };
        return names.Select(n => new Category
        {
            Id = ObjectId.GenerateNewId(),
            Name = n,
            Slug = n.ToLowerInvariant().Replace(" & ", "-").Replace(" ", "-"),
            Description = $"Categoria {n}"
        }).ToList();
    }

    private List<Product> SeedProducts(List<Category> categories)
    {
        string[] adjectives = { "Pro", "Max", "Lite", "Ultra", "Plus", "Eco", "Prime", "Neo" };
        string[] nouns = { "Fone", "Cadeira", "Livro", "Bola", "Robô", "Panela", "Teclado", "Mouse", "Câmera", "Lâmpada" };
        string[] tagPool = { "promo", "novo", "premium", "importado", "bestseller", "eco", "black-friday" };

        var products = new List<Product>();
        for (int i = 0; i < 60; i++)
        {
            var cat = categories[_rng.Next(categories.Count)];
            var name = $"{nouns[_rng.Next(nouns.Length)]} {adjectives[_rng.Next(adjectives.Length)]} {i:D2}";
            var price = Math.Round((decimal)(_rng.NextDouble() * 900 + 10), 2);

            var reviews = new List<Review>();
            int reviewCount = _rng.Next(0, 6);
            for (int r = 0; r < reviewCount; r++)
            {
                reviews.Add(new Review
                {
                    Author = $"user{_rng.Next(1, 200)}",
                    Rating = _rng.Next(1, 6),
                    Comment = "Comentário de exemplo",
                    CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(1, 120))
                });
            }

            var tags = tagPool.OrderBy(_ => _rng.Next()).Take(_rng.Next(1, 4)).ToList();

            products.Add(new Product
            {
                Id = ObjectId.GenerateNewId(),
                Sku = $"SKU-{i:D4}",
                Name = name,
                Description = $"Descrição do produto {name}. Excelente qualidade.",
                CategoryId = cat.Id,
                CategoryName = cat.Name,
                Price = price,
                Stock = _rng.Next(0, 200),
                Tags = tags,
                Dimensions = new Dimensions
                {
                    WeightKg = Math.Round(_rng.NextDouble() * 5 + 0.1, 2),
                    WidthCm = _rng.Next(5, 100),
                    HeightCm = _rng.Next(5, 100)
                },
                Reviews = reviews,
                RatingCount = reviews.Count,
                RatingAvg = reviews.Count > 0 ? Math.Round(reviews.Average(x => x.Rating), 2) : 0,
                CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(1, 365))
            });
        }
        return products;
    }

    private List<Customer> SeedCustomers()
    {
        string[] firstNames = { "Ana", "Bruno", "Carla", "Diego", "Elisa", "Felipe", "Gabi", "Hugo" };
        string[] countries = { "Brasil", "Portugal", "Argentina", "Chile" };
        var tiers = Enum.GetValues<LoyaltyTier>();

        var list = new List<Customer>();
        for (int i = 0; i < 20; i++)
        {
            var name = $"{firstNames[_rng.Next(firstNames.Length)]} {(char)('A' + i)}";
            list.Add(new Customer
            {
                Id = ObjectId.GenerateNewId(),
                Name = name,
                Email = $"cliente{i:D2}@exemplo.com",
                Country = countries[_rng.Next(countries.Length)],
                Address = new Address { Street = $"Rua {i}", City = "Cidade", Zip = $"0000{i:D2}" },
                LoyaltyTier = tiers[_rng.Next(tiers.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(30, 700))
            });
        }
        return list;
    }

    private List<Order> SeedOrders(List<Customer> customers, List<Product> products)
    {
        var orders = new List<Order>();
        var statuses = Enum.GetValues<OrderStatus>();

        for (int i = 0; i < 200; i++)
        {
            var customer = customers[_rng.Next(customers.Count)];
            int lineCount = _rng.Next(1, 5);
            var lines = new List<OrderLine>();

            for (int l = 0; l < lineCount; l++)
            {
                var p = products[_rng.Next(products.Count)];
                int qty = _rng.Next(1, 4);
                lines.Add(new OrderLine
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    CategoryName = p.CategoryName,
                    UnitPrice = p.Price,
                    Quantity = qty,
                    LineTotal = p.Price * qty
                });
            }

            orders.Add(new Order
            {
                Id = ObjectId.GenerateNewId(),
                OrderNumber = $"ORD-{i:D5}",
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                Status = statuses[_rng.Next(statuses.Length)],
                Lines = lines,
                Total = lines.Sum(x => x.LineTotal),
                CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(0, 365))
            });
        }
        return orders;
    }
}
