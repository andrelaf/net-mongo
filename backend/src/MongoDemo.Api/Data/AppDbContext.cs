using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Data;

/// <summary>
/// Acesso via EF CORE usando o provider oficial MongoDB.EntityFrameworkCore.
///
/// Quando usar EF Core x Driver direto:
///  - EF Core: produtividade, LINQ familiar, change tracking, uma API única
///    para times que já usam EF em SQL. Ótimo para CRUD e queries LINQ.
///  - Driver: controle total sobre o pipeline de agregação, operadores de
///    update atômicos ($inc, $push, arrayFilters), bulk write, índices, e todo
///    recurso novo do MongoDB. Necessário para agregações mais avançadas.
///
/// Documentos embutidos são mapeados como OWNED ENTITIES do EF.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ToCollection define a coleção física de cada entidade.
        modelBuilder.Entity<Category>().ToCollection("categories");

        modelBuilder.Entity<Product>(b =>
        {
            b.ToCollection("products");
            // Embutidos: Dimensions (1:1) e Reviews (1:N) como owned types.
            b.OwnsOne(p => p.Dimensions);
            b.OwnsMany(p => p.Reviews);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToCollection("customers");
            b.OwnsOne(c => c.Address);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToCollection("orders");
            b.OwnsMany(o => o.Lines);
        });
    }
}
