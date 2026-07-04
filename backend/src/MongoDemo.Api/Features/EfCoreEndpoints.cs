using Microsoft.EntityFrameworkCore;
using MongoDemo.Api.Data;

namespace MongoDemo.Api.Features;

/// <summary>
/// EF CORE com o provider oficial do MongoDB. Mostra que boa parte do dia a dia
/// (consultas LINQ, projeções, navegação em owned types) funciona igual ao EF de
/// sempre. O provider traduz o LINQ para pipelines de agregação do MongoDB.
///
/// Limites atuais que valem lembrar: nem todo operador do Mongo tem tradução em
/// LINQ (ex.: $bucket, $facet, arrayFilters) — nesses casos, use o Driver.
/// </summary>
public static class EfCoreEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/ef");

        // ---- Filtro (contraparte EF do filters/builder do Driver) ----
        g.MapGet("/filter", (AppDbContext db, CommandCapture cap, decimal min = 0, decimal max = 0) =>
            EndpointHelpers.RunExample(cap, "Filtros", "ef-core",
                "MESMO filtro do exemplo 'Builders' do Driver, agora em EF Core. Faixa de " +
                "preço + estoque disponível, ordenado por preço. O provider traduz o Where " +
                "para um estágio $match. Compare o comando gerado com o do Driver: o find/" +
                "aggregate é equivalente — muda a ergonomia (LINQ com change tracker), não a query.",
                """
                var lo = min <= 0 ? 0 : min;
                var hi = max <= 0 ? 500 : max;

                var list = await db.Products
                    .AsNoTracking()
                    .Where(p => p.Price >= lo && p.Price <= hi && p.Stock > 0)
                    .OrderBy(p => p.Price)
                    .Take(20)
                    .ToListAsync();
                """,
                async () =>
                {
                    var lo = min <= 0 ? 0 : min;
                    var hi = max <= 0 ? 500 : max;
                    var list = await db.Products
                        .AsNoTracking()
                        .Where(p => p.Price >= lo && p.Price <= hi && p.Stock > 0)
                        .OrderBy(p => p.Price)
                        .Take(20)
                        .ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));

        // ---- Agregação com EF: o LIMITE do provider (contraparte do Driver) ----
        g.MapGet("/aggregation", (AppDbContext db, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Agregação", "ef-core",
                "LIÇÃO IMPORTANTE de EF x Driver. O provider EF do MongoDB (10.x) AINDA NÃO " +
                "traduz GroupBy para $group — chamar db.Products.GroupBy(...) lança " +
                "'could not be translated'. Estratégia correta aqui: projetar no SERVIDOR só " +
                "os campos necessários (Select vira $project, que reduz o tráfego) e então " +
                "agrupar no CLIENTE com LINQ-to-Objects. Para agregação de verdade no " +
                "servidor, use o Driver — compare com 'Faturamento por categoria ($group)'.",
                """
                // GroupBy no servidor NÃO é suportado pelo provider EF hoje:
                //   db.Products.GroupBy(p => p.CategoryName)...  => InvalidOperationException

                // Projeta no servidor ($project) só o que a agregação precisa:
                var rows = await db.Products
                    .AsNoTracking()
                    .Select(p => new { p.CategoryName, p.Price, p.Stock })
                    .ToListAsync();

                // Agrega no cliente (LINQ-to-Objects):
                var byCategory = rows
                    .GroupBy(r => r.CategoryName)
                    .Select(g => new {
                        Category = g.Key,
                        Products = g.Count(),
                        AvgPrice = Math.Round(g.Average(x => x.Price), 2),
                        Stock    = g.Sum(x => x.Stock)
                    })
                    .OrderByDescending(x => x.Products)
                    .ToList();
                """,
                async () =>
                {
                    var rows = await db.Products
                        .AsNoTracking()
                        .Select(p => new { p.CategoryName, p.Price, p.Stock })
                        .ToListAsync();

                    var byCategory = rows
                        .GroupBy(r => r.CategoryName)
                        .Select(grp => new
                        {
                            Category = grp.Key,
                            Products = grp.Count(),
                            AvgPrice = Math.Round(grp.Average(x => x.Price), 2),
                            Stock = grp.Sum(x => x.Stock),
                            ranBy = "cliente (LINQ-to-Objects) — GroupBy não traduzido pelo provider"
                        })
                        .OrderByDescending(x => x.Products)
                        .ToList();
                    return (byCategory, byCategory.Count);
                }));

        // ---- Consulta LINQ pura ----
        g.MapGet("/linq", (AppDbContext db, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "EF Core", "ef-core",
                "Consulta LINQ padrão do EF Core traduzida pelo provider MongoDB. " +
                "Where + OrderByDescending + Take, com AsNoTracking porque é leitura " +
                "(dispensa o change tracker e é mais rápido).",
                """
                var list = await db.Products
                    .AsNoTracking()
                    .Where(p => p.Price >= 100 && p.Stock > 0)
                    .OrderByDescending(p => p.Price)
                    .Take(15)
                    .ToListAsync();
                """,
                async () =>
                {
                    var list = await db.Products
                        .AsNoTracking()
                        .Where(p => p.Price >= 100 && p.Stock > 0)
                        .OrderByDescending(p => p.Price)
                        .Take(15)
                        .ToListAsync();
                    return (list.Select(Dto.Product), list.Count);
                }));

        // ---- Projeção com Select ----
        g.MapGet("/projection", (AppDbContext db, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "EF Core", "ef-core",
                "Projeção com Select para um tipo anônimo — o EF materializa só os campos " +
                "pedidos. Boa prática de performance também no EF: nunca traga a entidade " +
                "inteira quando a tela precisa de 3 campos.",
                """
                var list = await db.Products
                    .AsNoTracking()
                    .Where(p => p.RatingAvg >= 4)
                    .Select(p => new { p.Name, p.Price, p.RatingAvg })
                    .Take(15)
                    .ToListAsync();
                """,
                async () =>
                {
                    var list = await db.Products
                        .AsNoTracking()
                        .Where(p => p.RatingAvg >= 4)
                        .Select(p => new { p.Name, p.Price, p.RatingAvg })
                        .Take(15)
                        .ToListAsync();
                    return (list, list.Count);
                }));

        // ---- Owned types (documentos embutidos) ----
        g.MapGet("/owned", (AppDbContext db, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "EF Core", "ef-core",
                "Owned types: Reviews e Dimensions são documentos EMBUTIDOS mapeados como " +
                "owned entities. O filtro Where(p => p.Reviews.Any(r => r.Rating == 5)) é " +
                "traduzido para o servidor. Já a CONTAGEM por documento é feita no cliente: " +
                "nem todo operador sobre coleções owned tem tradução no provider — quando " +
                "faltar, materialize com ToListAsync e finalize em LINQ-to-Objects (ou use " +
                "o Driver/agregação). Repare que as reviews já vieram carregadas no produto.",
                """
                // Filtro traduzido para o servidor ($elemMatch em reviews):
                var products = await db.Products
                    .AsNoTracking()
                    .Where(p => p.Reviews.Any(r => r.Rating == 5))
                    .Take(15)
                    .ToListAsync();

                // Projeção/contagem no cliente (owned types já carregados):
                var data = products.Select(p => new {
                    p.Name,
                    FiveStar = p.Reviews.Count(r => r.Rating == 5),
                    Weight = p.Dimensions?.WeightKg ?? 0
                });
                """,
                async () =>
                {
                    var products = await db.Products
                        .AsNoTracking()
                        .Where(p => p.Reviews.Any(r => r.Rating == 5))
                        .Take(15)
                        .ToListAsync();

                    var data = products.Select(p => new
                    {
                        p.Name,
                        FiveStar = p.Reviews.Count(r => r.Rating == 5),
                        Weight = p.Dimensions?.WeightKg ?? 0
                    }).ToList();
                    return (data, data.Count);
                }));
    }
}
