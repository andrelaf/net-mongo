using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// Mapeadores para DTOs leves. Motivo: ObjectId não serializa bem com
/// System.Text.Json (viraria um objeto com Timestamp/Machine/etc). Expomos o
/// _id como string e apenas os campos relevantes para o front.
/// </summary>
public static class Dto
{
    public static object Product(Product p) => new
    {
        id = p.Id.ToString(),
        p.Sku,
        p.Name,
        category = p.CategoryName,
        price = p.Price,
        stock = p.Stock,
        tags = p.Tags,
        ratingAvg = p.RatingAvg,
        ratingCount = p.RatingCount
    };

    public static object Order(Order o) => new
    {
        id = o.Id.ToString(),
        o.OrderNumber,
        customer = o.CustomerName,
        status = o.Status.ToString(),
        total = o.Total,
        lines = o.Lines.Count,
        createdAt = o.CreatedAt
    };

    public static object Customer(Customer c) => new
    {
        id = c.Id.ToString(),
        c.Name,
        c.Email,
        c.Country,
        tier = c.LoyaltyTier.ToString()
    };
}
