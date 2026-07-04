using MongoDB.Bson;
using MongoDB.Driver;
using MongoDemo.Api.Data;
using MongoDemo.Api.Domain;

namespace MongoDemo.Api.Features;

/// <summary>
/// ANTI-PADRÕES de modelagem (schema design anti-patterns) e como corrigi-los.
/// Baseado nos anti-padrões oficiais do MongoDB: arrays ilimitados, documentos
/// inchados (limite de 16MB), $lookup excessivo, índices/coleções demais.
/// Cada exemplo demonstra o PROBLEMA de forma mensurável e mostra a CORREÇÃO.
/// </summary>
public static class AntiPatternEndpoints
{
    private const long MaxBsonBytes = 16 * 1024 * 1024; // limite rígido de 16MB por documento

    private static string Human(long bytes) =>
        bytes < 1024 ? $"{bytes} B" :
        bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB" :
        $"{bytes / (1024.0 * 1024.0):F2} MB";

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/antipatterns");

        // ---- Anti-padrão nº 1: ARRAY EMBUTIDO ILIMITADO (massive array) ----
        g.MapGet("/unbounded-array", (CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Anti-padrões", "driver",
                "ANTI-PADRÃO: array embutido que cresce SEM LIMITE. Imagine embutir TODOS " +
                "os pedidos dentro do cliente. Medimos o tamanho BSON real do documento à " +
                "medida que o array cresce e projetamos quando ele estouraria o limite rígido " +
                "de 16MB do MongoDB. Além do teto de 16MB, arrays enormes degradam índices e " +
                "forçam carregar o array inteiro em memória a cada leitura. " +
                "CORREÇÃO: referenciar (é exatamente o que nosso schema faz — pedidos vivem " +
                "em sua própria coleção com customerId) e/ou aplicar o Subset Pattern.",
                """
                // Documento com array embutido que cresce sem limite (ERRADO):
                //   class BloatedCustomer { ...; List<OrderLine> Orders; }
                // Medimos o tamanho BSON real conforme o array cresce:
                foreach (var n in new[] { 10, 100, 1000, 5000 })
                {
                    var doc = new BloatedCustomer { Orders = BuildLines(n) };
                    long bytes = doc.ToBson().Length;   // serialização BSON real
                }
                // Extrapolamos: quantos elementos até bater os 16MB?
                """,
                () =>
                {
                    var samples = new[] { 10, 100, 1000, 5000 };
                    var measurements = new List<object>();
                    long lastBytes = 0;
                    int lastN = 0;

                    foreach (var n in samples)
                    {
                        var doc = new BloatedCustomer
                        {
                            Id = ObjectId.GenerateNewId(),
                            Name = "Cliente Inchado",
                            Orders = BuildLines(n)
                        };
                        long bytes = doc.ToBson().Length;
                        measurements.Add(new { embeddedItems = n, bytes, size = Human(bytes) });
                        lastBytes = bytes;
                        lastN = n;
                    }

                    // Bytes por elemento (aprox.) e projeção do estouro de 16MB.
                    double bytesPerItem = (double)lastBytes / lastN;
                    long itemsUntil16Mb = (long)(MaxBsonBytes / bytesPerItem);

                    var result = new
                    {
                        limit = Human(MaxBsonBytes),
                        measurements,
                        bytesPerItemApprox = Math.Round(bytesPerItem, 1),
                        estimatedItemsUntil16Mb = itemsUntil16Mb,
                        verdict = $"~{itemsUntil16Mb:N0} pedidos embutidos estourariam o limite de 16MB.",
                        fix = "Referencie (coleção 'orders' com customerId) e/ou use o Subset Pattern."
                    };
                    return Task.FromResult<(object, int)>((result, samples.Length));
                }));

        // ---- Correção: SUBSET PATTERN ----
        g.MapGet("/subset-pattern", (MongoContext ctx, CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Anti-padrões", "driver",
                "CORREÇÃO do array ilimitado: SUBSET PATTERN. Em vez de embutir as N reviews " +
                "(que crescem sem limite), o documento do produto guarda só um SUBCONJUNTO " +
                "quente — as poucas reviews mais recentes/úteis para a tela — enquanto o " +
                "histórico completo fica numa coleção separada. A listagem lê 1 documento " +
                "pequeno; a página de 'todas as reviews' busca o resto sob demanda. " +
                "Aqui projetamos apenas as 3 reviews mais recentes com $slice (o 'subset').",
                """
                // Subset: só as 3 reviews mais recentes viajam para o card do produto.
                var projection = Builders<Product>.Projection
                    .Include(p => p.Name)
                    .Include(p => p.RatingAvg)
                    .Include(p => p.RatingCount)          // total mora no doc (contador)
                    .Slice(p => p.Reviews, -3);           // -3 = as 3 ÚLTIMAS

                var cards = await ctx.Products
                    .Find(Builders<Product>.Filter.SizeGt(p => p.Reviews, 2))
                    .Project<BsonDocument>(projection)
                    .Limit(8)
                    .ToListAsync();
                """,
                async () =>
                {
                    var projection = Builders<Product>.Projection
                        .Include(p => p.Name)
                        .Include(p => p.RatingAvg)
                        .Include(p => p.RatingCount)
                        .Slice(p => p.Reviews, -3);

                    var cards = await ctx.Products
                        .Find(Builders<Product>.Filter.SizeGt(p => p.Reviews, 2))
                        .Project<BsonDocument>(projection)
                        .Limit(8)
                        .ToListAsync();

                    var json = cards.Select(d => d.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
                    {
                        Indent = true,
                        OutputMode = MongoDB.Bson.IO.JsonOutputMode.Shell
                    })).ToList();
                    return (json, json.Count);
                }));
    }

    // Documento propositalmente mal modelado para o exemplo de array ilimitado.
    private class BloatedCustomer
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderLine> Orders { get; set; } = new();
    }

    private static List<OrderLine> BuildLines(int n)
    {
        var list = new List<OrderLine>(n);
        for (int i = 0; i < n; i++)
        {
            list.Add(new OrderLine
            {
                ProductId = ObjectId.GenerateNewId(),
                ProductName = $"Produto exemplo número {i}",
                CategoryName = "Categoria",
                UnitPrice = 99.90m,
                Quantity = 2,
                LineTotal = 199.80m
            });
        }
        return list;
    }
}
