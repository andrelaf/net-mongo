using MongoDB.Bson;
using MongoDB.Driver;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Data;

/// <summary>
/// Acesso via DRIVER OFICIAL (MongoDB.Driver) — o caminho de mais baixo nível e
/// mais poderoso. Expõe as coleções fortemente tipadas e centraliza a criação
/// de índices.
///
/// O MongoClient é caro de criar e gerencia o pool de conexões internamente:
/// deve ser SINGLETON na aplicação. Nunca crie um MongoClient por requisição.
/// </summary>
public class MongoContext
{
    public IMongoDatabase Database { get; }

    public MongoContext(IMongoClient client, MongoSettings settings)
    {
        Database = client.GetDatabase(settings.Database);
    }

    public IMongoCollection<Product> Products => Database.GetCollection<Product>("products");
    public IMongoCollection<Category> Categories => Database.GetCollection<Category>("categories");
    public IMongoCollection<Order> Orders => Database.GetCollection<Order>("orders");
    public IMongoCollection<Customer> Customers => Database.GetCollection<Customer>("customers");

    /// <summary>
    /// Cria os índices que sustentam os exemplos de filtro/ordenação/performance.
    /// Índices são a decisão de performance nº 1 no MongoDB: uma query sem índice
    /// vira COLLSCAN (varre a coleção inteira). CreateOne é idempotente por nome.
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        // SKU único — garante unicidade no servidor, não só na aplicação.
        await Products.Indexes.CreateOneAsync(new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(p => p.Sku),
            new CreateIndexOptions { Unique = true, Name = "ux_sku" }));

        // Índice composto: filtra por categoria e ordena por preço.
        // Regra ESR (Equality, Sort, Range): igualdade primeiro (categoryId),
        // depois o campo de ordenação (price).
        await Products.Indexes.CreateOneAsync(new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(p => p.CategoryId).Ascending(p => p.Price),
            new CreateIndexOptions { Name = "ix_category_price" }));

        // Índice multikey em array de tags — acelera "tags contém X".
        await Products.Indexes.CreateOneAsync(new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(p => p.Tags),
            new CreateIndexOptions { Name = "ix_tags" }));

        // Índice de texto para busca full-text em nome/descrição.
        await Products.Indexes.CreateOneAsync(new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Text(p => p.Name).Text(p => p.Description),
            new CreateIndexOptions { Name = "tx_name_desc" }));

        // Pedidos: consultas por cliente e por data são as mais comuns.
        await Orders.Indexes.CreateOneAsync(new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(o => o.CustomerId).Descending(o => o.CreatedAt),
            new CreateIndexOptions { Name = "ix_customer_date" }));

        await Orders.Indexes.CreateOneAsync(new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Descending(o => o.CreatedAt),
            new CreateIndexOptions { Name = "ix_created" }));

        await Customers.Indexes.CreateOneAsync(new CreateIndexModel<Customer>(
            Builders<Customer>.IndexKeys.Ascending(c => c.Email),
            new CreateIndexOptions { Unique = true, Name = "ux_email" }));
    }
}
