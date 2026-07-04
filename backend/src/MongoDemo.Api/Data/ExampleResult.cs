using System.Text.Json.Serialization;

namespace MongoDemo.Api.Data;

/// <summary>
/// Envelope padrão de resposta dos exemplos. Além do dado em si, devolve o
/// código C# executado, os comandos reais enviados ao MongoDB e o tempo gasto —
/// tudo que o front precisa para "ensinar" o conceito.
/// </summary>
public class ExampleResult<T>
{
    public required string Concept { get; init; }
    public required string Approach { get; init; } // "driver" | "ef-core"
    public required string Explanation { get; init; }

    // Sem o atributo, o camelCase geraria "cSharp" (S maiúsculo). Fixamos "csharp".
    [JsonPropertyName("csharp")]
    public required string CSharp { get; init; }
    public long ElapsedMs { get; init; }
    public int Count { get; init; }
    public T? Data { get; init; }
    public List<CapturedCommand> MongoCommands { get; set; } = new();
}

public static class ExampleResult
{
    public static ExampleResult<T> Of<T>(
        string concept, string approach, string explanation, string csharp,
        long elapsedMs, T data, int count) => new()
        {
            Concept = concept,
            Approach = approach,
            Explanation = explanation,
            CSharp = csharp,
            ElapsedMs = elapsedMs,
            Data = data,
            Count = count
        };
}
