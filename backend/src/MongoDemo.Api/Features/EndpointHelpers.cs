using System.Diagnostics;
using MongoDemo.Api.Data;

namespace MongoDemo.Api.Features;

public static class EndpointHelpers
{
    /// <summary>
    /// Executa a operação, mede o tempo e monta o <see cref="ExampleResult{T}"/>
    /// já anexando os comandos MongoDB capturados durante a execução.
    /// </summary>
    public static async Task<IResult> RunExample<T>(
        CommandCapture capture,
        string concept,
        string approach,
        string explanation,
        string csharp,
        Func<Task<(T data, int count)>> operation)
    {
        // Zera o que possa ter sido capturado por seeding/health nesta scope.
        capture.Reset();

        var sw = Stopwatch.StartNew();
        var (data, count) = await operation();
        sw.Stop();

        var result = ExampleResult.Of(concept, approach, explanation, csharp,
            sw.ElapsedMilliseconds, data, count);
        result.MongoCommands = capture.Commands.ToList();

        return Results.Ok(result);
    }
}
