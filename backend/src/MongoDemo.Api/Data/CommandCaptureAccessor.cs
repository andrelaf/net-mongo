namespace MongoDemo.Api.Data;

/// <summary>
/// Ponte entre o MongoClient (singleton, onde os eventos de comando são
/// disparados) e o <see cref="CommandCapture"/> (scoped, um por requisição).
/// Usa AsyncLocal para que cada requisição HTTP enxergue apenas os seus
/// próprios comandos, mesmo com várias requisições concorrentes.
/// </summary>
public class CommandCaptureAccessor
{
    private static readonly AsyncLocal<CommandCapture?> Current = new();

    public CommandCapture? Capture
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
