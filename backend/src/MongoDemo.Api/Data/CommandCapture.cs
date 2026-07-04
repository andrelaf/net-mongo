using MongoDB.Bson;

namespace MongoDemo.Api.Data;

/// <summary>
/// Captura os comandos que o driver realmente envia ao MongoDB durante o
/// processamento de uma requisição HTTP. É um serviço com escopo (scoped):
/// o middleware limpa a lista no início de cada request e cada endpoint pode
/// devolver o que foi capturado para fins didáticos.
///
/// O driver .NET expõe eventos de monitoramento de comandos (CommandStartedEvent).
/// Interceptamos esses eventos para mostrar ao aluno o comando `find`,
/// `aggregate`, `insert`, etc. exatamente como chega ao servidor — a melhor
/// forma de "provar" o que uma FilterDefinition ou um pipeline LINQ gera.
/// </summary>
public class CommandCapture
{
    private readonly List<CapturedCommand> _commands = new();
    private readonly Lock _gate = new();

    // Comandos administrativos/handshake que não interessam ao aluno.
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "hello", "isMaster", "ismaster", "buildInfo", "ping", "saslStart",
        "saslContinue", "getLastError", "endSessions", "listIndexes", "listCollections"
    };

    public void Record(string commandName, BsonDocument command)
    {
        if (Ignored.Contains(commandName))
            return;

        // Serializa em JSON "shell-like" (relaxado), fácil de ler no front.
        var json = command.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
        {
            Indent = true,
            OutputMode = MongoDB.Bson.IO.JsonOutputMode.Shell
        });

        lock (_gate)
        {
            _commands.Add(new CapturedCommand(commandName, json));
        }
    }

    public void Reset()
    {
        lock (_gate) _commands.Clear();
    }

    public IReadOnlyList<CapturedCommand> Commands
    {
        get { lock (_gate) return _commands.ToList(); }
    }
}

public record CapturedCommand(string Name, string Json);
