using System.Threading.Channels;
using NetWatch.Core;
using NetWatch.Core.Logging;

namespace NetWatch.App.Logging;

/// <summary>
/// Führt das <see cref="Journal"/> auf einem eigenen Task. Andere Programmteile legen Aufträge nur in eine
/// Warteschlange (<see cref="Post"/>) und warten nie auf Dateizugriffe. Alle 10 s werden gepufferte Zeilen
/// nachgeschrieben und das Lebenszeichen erneuert.
/// </summary>
internal sealed class JournalWriter : IAsyncDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    private readonly Channel<Action<Journal>> _queue = Channel.CreateUnbounded<Action<Journal>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Journal _journal;
    private readonly Task _loop;
    private readonly System.Threading.Timer _tick;
    private int _disposed;

    public JournalWriter(string dataDirectory, IClock clock, TimeZoneInfo timeZone, out RunState? previousState)
    {
        var stateFile = new StateFile(Path.Combine(dataDirectory, RunState.FileName));
        previousState = stateFile.Load();

        _journal = new Journal(
            new FileLineSink(Path.Combine(dataDirectory, EventCsv.FileName), EventCsv.Header),
            new FileLineSink(Path.Combine(dataDirectory, SessionCsv.FileName), SessionCsv.Header),
            stateFile.Save,
            clock,
            timeZone);

        _loop = Task.Run(RunAsync);
        _tick = new System.Threading.Timer(_ => Post(j => j.Tick()), null, TickInterval, TickInterval);
    }

    public void Post(Action<Journal> action) => _queue.Writer.TryWrite(action);

    private async Task RunAsync()
    {
        await foreach (Action<Journal> action in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                action(_journal);
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Protokoll: {ex}");
            }
        }
    }

    /// <summary>Schreibt alles Ausstehende, schließt das Journal und wartet, bis der Schreib-Task fertig ist.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _tick.DisposeAsync().ConfigureAwait(false);
        Post(j => j.Close());
        _queue.Writer.Complete();
        await _loop.ConfigureAwait(false);
    }
}
