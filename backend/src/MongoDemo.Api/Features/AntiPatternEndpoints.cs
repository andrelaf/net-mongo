using System.Diagnostics;
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

        // ---- Anti-padrão nº 2: ÍNDICES DEMAIS / DESNECESSÁRIOS ----
        g.MapGet("/too-many-indexes", async (MongoContext ctx, CommandCapture cap) =>
        {
            const string collName = "_ap_demo_indexes";
            var coll = ctx.Database.GetCollection<BsonDocument>(collName);

            return await EndpointHelpers.RunExample(cap, "Anti-padrões", "driver",
                "ANTI-PADRÃO: criar índices 'por via das dúvidas'. Todo índice acelera a " +
                "LEITURA, mas é atualizado em TODA escrita e ocupa disco/RAM. Aqui inserimos " +
                "o mesmo lote de documentos duas vezes: primeiro com só o índice padrão (_id) " +
                "e depois com 5 índices extras. Compare o tempo de inserção — os índices a mais " +
                "tornam a escrita mais lenta sem nenhum benefício se você não consulta aqueles " +
                "campos. CORREÇÃO: só crie índices para os filtros/ordenções que você realmente usa.",
                """
                // Aquece (cria a coleção; não cronometramos essa parte):
                await coll.InsertManyAsync(BuildDocs(1000));

                // 1) Insere 8.000 docs com apenas o índice padrão (_id):
                var sw1 = Stopwatch.StartNew();
                await coll.InsertManyAsync(BuildDocs(8000));
                sw1.Stop();

                // 2) Cria 15 índices extras e insere outros 8.000 docs:
                for (int f = 0; f < 15; f++)
                    await coll.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending($"f{f}")));

                var sw2 = Stopwatch.StartNew();
                await coll.InsertManyAsync(BuildDocs(8000));
                sw2.Stop();
                // sw2 > sw1: manter 15 índices custa em CADA escrita.
                """,
                async () =>
                {
                    const int batch = 8000;
                    await ctx.Database.DropCollectionAsync(collName); // idempotente
                    await coll.InsertManyAsync(BuildDocs(1000));      // warmup (cria coleção; não cronometrado)

                    var sw1 = Stopwatch.StartNew();
                    await coll.InsertManyAsync(BuildDocs(batch));
                    sw1.Stop();

                    for (int f = 0; f < 15; f++)
                        await coll.Indexes.CreateOneAsync(
                            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending($"f{f}")));

                    var sw2 = Stopwatch.StartNew();
                    await coll.InsertManyAsync(BuildDocs(batch));
                    sw2.Stop();

                    var stats = await ctx.Database.RunCommandAsync<BsonDocument>(
                        new BsonDocument { { "collStats", collName } });
                    int nindexes = stats.GetValue("nindexes", 0).ToInt32();
                    long indexSize = stats.GetValue("totalIndexSize", 0).ToInt64();

                    await ctx.Database.DropCollectionAsync(collName); // limpeza

                    var slowdown = sw1.ElapsedMilliseconds > 0
                        ? Math.Round((double)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds, 2)
                        : 0;

                    var result = new
                    {
                        withDefaultIndexOnly = new { docs = batch, insertMs = sw1.ElapsedMilliseconds, indexes = 1 },
                        withFifteenExtraIndexes = new { docs = batch, insertMs = sw2.ElapsedMilliseconds, indexes = nindexes, indexSize = Human(indexSize) },
                        writeSlowdownFactor = slowdown,
                        verdict = slowdown > 1
                            ? $"Com 15 índices a escrita ficou ~{slowdown}x mais lenta (mesmos {batch} docs)."
                            : $"Escrita comparável nesta amostra, mas os índices ocuparam {Human(indexSize)} extras e são mantidos em toda gravação.",
                        fix = "Indexe só o que você consulta. Remova índices não usados ($indexStats ajuda a achá-los)."
                    };
                    return (result, 2);
                });
        });

        // ---- Anti-padrão nº 3: muitos documentos minúsculos -> BUCKET PATTERN ----
        g.MapGet("/bucket-pattern", (CommandCapture cap) =>
            EndpointHelpers.RunExample(cap, "Anti-padrões", "driver",
                "ANTI-PADRÃO em séries temporais/IoT: um documento por leitura gera MILHÕES " +
                "de docs minúsculos — muito overhead de índice e de _id. CORREÇÃO: BUCKET " +
                "PATTERN — agrupar as leituras de uma janela (ex.: 1 hora) em UM documento com " +
                "um array de medições + agregados (min/máx/média) já calculados. Aqui pegamos " +
                "240 leituras de um sensor (uma a cada 6 min por 24h) e agrupamos por hora: " +
                "de 240 documentos para 24 buckets — 10x menos documentos.",
                """
                // ANTES (anti-padrão): 240 leituras = 240 documentos minúsculos.
                var readings = GenerateReadings(240);

                // DEPOIS (Bucket Pattern): agrupa por hora em 1 doc por janela.
                var buckets = readings
                    .GroupBy(r => new DateTime(r.ts.Year, r.ts.Month, r.ts.Day, r.ts.Hour, 0, 0))
                    .Select(h => new {
                        sensor = "sensor-1",
                        hour = h.Key,
                        count = h.Count(),
                        min = h.Min(x => x.value),
                        max = h.Max(x => x.value),
                        avg = Math.Round(h.Average(x => x.value), 2),
                        measurements = h.Select(x => new { x.ts, x.value })   // array embutido (LIMITADO: 1h)
                    });
                """,
                () =>
                {
                    var readings = GenerateReadings(240);
                    var buckets = readings
                        .GroupBy(r => new DateTime(r.ts.Year, r.ts.Month, r.ts.Day, r.ts.Hour, 0, 0, DateTimeKind.Utc))
                        .Select(h => new
                        {
                            sensor = "sensor-1",
                            hour = h.Key,
                            count = h.Count(),
                            min = h.Min(x => x.value),
                            max = h.Max(x => x.value),
                            avg = Math.Round(h.Average(x => x.value), 2),
                            measurements = h.Select(x => new { x.ts, x.value }).ToList()
                        })
                        .OrderBy(b => b.hour)
                        .ToList();

                    var result = new
                    {
                        antiPattern = new { style = "1 documento por leitura", documents = readings.Count },
                        bucketPattern = new { style = "1 documento por hora (bucket)", documents = buckets.Count },
                        reduction = $"{readings.Count} -> {buckets.Count} documentos ({readings.Count / buckets.Count}x menos)",
                        sampleBucket = buckets[0],
                        note = "O array do bucket é LIMITADO (no máx. as leituras de 1 hora) — não vira array ilimitado."
                    };
                    return Task.FromResult<(object, int)>((result, buckets.Count));
                }));
    }

    private static List<BsonDocument> BuildDocs(int n)
    {
        var rng = new Random(42);
        var list = new List<BsonDocument>(n);
        for (int i = 0; i < n; i++)
        {
            var doc = new BsonDocument();
            for (int f = 0; f < 15; f++)
                doc.Add($"f{f}", rng.Next(0, 1_000_000));
            list.Add(doc);
        }
        return list;
    }

    private static List<(DateTime ts, double value)> GenerateReadings(int n)
    {
        var rng = new Random(7);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var list = new List<(DateTime, double)>(n);
        for (int i = 0; i < n; i++)
            list.Add((start.AddMinutes(i * 6), Math.Round(20 + rng.NextDouble() * 10, 2)));
        return list;
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
