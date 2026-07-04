using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDemo.Api.Domain;

/// <summary>
/// Produto do catálogo. Demonstra várias decisões de modelagem:
///
///  - <see cref="CategoryId"/> + <see cref="CategoryName"/>: REFERÊNCIA com um
///    campo denormalizado. Guardamos o Id (fonte da verdade) e também o nome,
///    que é lido com muita frequência e muda pouco. Isso evita um $lookup/join
///    na maioria das telas de listagem. (Padrão "Extended Reference").
///
///  - <see cref="Reviews"/>: documentos EMBUTIDOS. Avaliações pertencem ao
///    produto, são lidas junto com ele e têm cardinalidade limitada. Embutir
///    evita joins e mantém a leitura em um único documento. Regra prática:
///    embuta quando a relação é "contém" e o array não cresce sem limite.
///
///  - <see cref="Tags"/>: array de strings, ótimo para índice multikey e para
///    filtros do tipo "contém a tag X".
/// </summary>
public class Product
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("sku")]
    public string Sku { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    // ---- Referência para Category (normalizado) + campo denormalizado ----
    [BsonElement("categoryId")]
    public ObjectId CategoryId { get; set; }

    [BsonElement("categoryName")]
    public string CategoryName { get; set; } = string.Empty;

    [BsonElement("price")]
    // Decimal128 preserva precisão monetária (nunca use double para dinheiro).
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("dimensions")]
    public Dimensions? Dimensions { get; set; }

    // ---- Sub-documentos embutidos ----
    [BsonElement("reviews")]
    public List<Review> Reviews { get; set; } = new();

    // Campo pré-calculado (denormalização de agregado): média das reviews.
    // Atualizado quando uma review é adicionada. Evita recalcular a média
    // toda vez que o produto é lido. Clássico trade-off write-vs-read.
    [BsonElement("ratingAvg")]
    public double RatingAvg { get; set; }

    [BsonElement("ratingCount")]
    public int RatingCount { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>Objeto de valor embutido (não tem _id próprio).</summary>
public class Dimensions
{
    [BsonElement("weightKg")]
    public double WeightKg { get; set; }

    [BsonElement("widthCm")]
    public double WidthCm { get; set; }

    [BsonElement("heightCm")]
    public double HeightCm { get; set; }
}

/// <summary>Avaliação embutida dentro de Product.</summary>
public class Review
{
    [BsonElement("author")]
    public string Author { get; set; } = string.Empty;

    [BsonElement("rating")]
    public int Rating { get; set; }

    [BsonElement("comment")]
    public string Comment { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
