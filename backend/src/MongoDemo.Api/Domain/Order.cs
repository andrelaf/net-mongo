using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDemo.Api.Domain;

/// <summary>
/// Pedido. Demonstra o padrão de SNAPSHOT / denormalização histórica:
/// as linhas do pedido (<see cref="Lines"/>) copiam o nome e o preço do produto
/// no momento da compra. Um pedido é um fato histórico — se o preço do produto
/// mudar amanhã, o pedido de ontem NÃO pode mudar. Por isso não referenciamos
/// o preço "ao vivo"; congelamos o valor no documento do pedido.
/// </summary>
public class Order
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    // Referência ao cliente (relação 1:N com muitos pedidos por cliente ->
    // referenciar, nunca embutir pedidos dentro do cliente pois cresce sem limite).
    [BsonElement("customerId")]
    public ObjectId CustomerId { get; set; }

    [BsonElement("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)] // salva o enum como string legível
    public OrderStatus Status { get; set; }

    [BsonElement("lines")]
    public List<OrderLine> Lines { get; set; } = new();

    [BsonElement("total")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Total { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>Linha de pedido embutida — snapshot do produto no ato da compra.</summary>
public class OrderLine
{
    [BsonElement("productId")]
    public ObjectId ProductId { get; set; }

    // Snapshot: nome e preço congelados no momento da compra.
    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("categoryName")]
    public string CategoryName { get; set; } = string.Empty;

    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("lineTotal")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }
}
