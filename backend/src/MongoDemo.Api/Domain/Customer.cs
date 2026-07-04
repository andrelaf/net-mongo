using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDemo.Api.Domain;

/// <summary>
/// Cliente. O endereço é embutido (1:1 / 1:poucos, lido junto com o cliente).
/// Já os pedidos NÃO são embutidos aqui — são uma relação 1:muitos ilimitada,
/// então vivem em sua própria coleção referenciando o customerId.
/// </summary>
public class Customer
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("address")]
    public Address? Address { get; set; }

    [BsonElement("loyaltyTier")]
    [BsonRepresentation(BsonType.String)]
    public LoyaltyTier LoyaltyTier { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public enum LoyaltyTier
{
    Bronze,
    Silver,
    Gold,
    Platinum
}

public class Address
{
    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("zip")]
    public string Zip { get; set; } = string.Empty;
}
