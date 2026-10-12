using System.Text;
using System.Threading.Channels;

namespace NetWatch.App.Diagnostics;

/// <summary>
/// Schreibt Zeilen in Konsole und Datei, ohne den Aufrufer zu blockieren:
/// <see cref="Write"/> legt die Zeile nur in eine Warteschlange, ein eigener Task schreibt sie weg.
/// So bleibt der Takt-Pfad des Prüfers frei von Datei- und Konsolenzugriffen.
/// </summary>
internal sealed class DiagnosticLog : IAsyncDisposable
{
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly StreamWriter _file;
    private readonly Task _writer;

    public DiagnosticLog(string filePath)
    {
        FilePath = filePath;
        _file = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _writer = Task.Run(WriteLoopAsync);
    }

    public string FilePath { get; }

    public void Write(string line) => _lines.Writer.TryWrite(line);

    private async Task WriteLoopAsync()
    {
        await foreach (string line in _lines.Reader.ReadAllAsync())
        {
            Console.WriteLine(line);
            await _file.WriteLineAsync(line);
            await _file.FlushAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lines.Writer.Complete();
        await _writer;
        await _file.DisposeAsync();
    }
}
