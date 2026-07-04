using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDemo.Api.Domain;

/// <summary>
/// Categoria de produto. Coleção pequena e "de referência" (lookup table).
/// Boa prática: coleções pequenas e estáveis são ótimas candidatas a serem
/// referenciadas por Id a partir de outras coleções (normalização parcial).
/// </summary>
public class Category
{
    // [BsonId] mapeia esta propriedade para o campo "_id" do documento.
    // Usamos ObjectId (12 bytes) por ser o default do Mongo: contém timestamp,
    // é gerado no cliente (evita round-trip) e é sequencial o suficiente para
    // não fragmentar índices como um GUID aleatório faria.
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("slug")]
    public string Slug { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}
