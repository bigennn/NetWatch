using System.Net;
using NetWatch.App.Logging;
using NetWatch.App.Network;
using NetWatch.Core;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.App.Measurement;

/// <summary>
/// Der Messbetrieb: pingt im Prüftakt, gibt jede Runde an den <see cref="OutageDetector"/> und leitet dessen
/// Meldungen an das Protokoll weiter. Läuft auf einem eigenen Hintergrund-Task; im Takt-Pfad gibt es keinen
/// Datei- oder UI-Zugriff, nur das Einreihen in Warteschlangen.
/// </summary>
internal sealed class MeasurementService : IAsyncDisposable
{
    public const int IntervalMs = 200;
    public const int TimeoutMs = 180;

    private static readonly IPAddress[] InternetTargets = [IPAddress.Parse("1.1.1.1"), IPAddress.Parse("8.8.8.8")];

    private readonly IClock _clock;
    private readonly NetworkMonitor _network;
    private readonly JournalWriter _journal;
    private readonly IcmpProber _prober;
    private readonly OutageDetector _detector = new();
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loop;
    private bool _paused;
    private bool _gatewayNoteWritten;

    public MeasurementService(IClock clock, NetworkMonitor network, JournalWriter journal)
    {
        _clock = clock;
        _network = network;
        _journal = journal;
        _prober = new IcmpProber(clock, TimeoutMs);
        _detector.OutageStarted += (_, start) =>
        {
            _journal.Post(j => j.OutageStarted(start));
            OutageStarted?.Invoke(this, start);
        };
        _detector.OutageEnded += (_, outage) =>
        {
            _journal.Post(j => j.OutageEnded(outage));
            OutageEnded?.Invoke(this, outage);
        };
    }

    // Die folgenden Ereignisse kommen vom Mess-Task. Empfänger dürfen nicht blockieren,
    // die Oberfläche reiht sie deshalb nur in ihre eigene Warteschlange ein.

    /// <summary>Wechsel grau/grün/rot.</summary>
    public event EventHandler<ConnectionState>? StateChanged;

    public event EventHandler<OutageStart>? OutageStarted;

    public event EventHandler<Outage>? OutageEnded;

    public ConnectionState State { get; private set; } = ConnectionState.Paused;

    /// <summary>Der Router beantwortet keine Pings (wird im Hover-Text angezeigt).</summary>
    public bool GatewayIgnoresPings => Volatile.Read(ref _gatewayNoteWritten);

    public void Start() => _loop = Task.Run(() => RunAsync(_cancellation.Token));

    /// <summary>Standby beginnt: laufender Aussetzer endet mit <c>suspended</c>, bis <see cref="Resume"/> wird nicht ausgewertet.</summary>
    public void Suspend()
    {
        lock (_sync)
        {
            _paused = true;
            _detector.Suspend(_clock.MonotonicMs);
            PublishState();
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            _paused = false;
        }
    }

    private async Task RunAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMs));
        long roundNumber = 0;
        try
        {
            do
            {
                if (Volatile.Read(ref _paused))
                {
                    continue;
                }

                GatewayInfo? gateway = _network.Gateway;
                bool adapterDown = _network.AdapterDown;
                var targets = new List<IPAddress>(3);
                if (gateway is not null)
                {
                    targets.Add(gateway.Gateway);
                }

                targets.AddRange(InternetTargets);

                long number = roundNumber++;
                DateTimeOffset sentAtUtc = _clock.UtcNow;
                long sentAtMs = _clock.MonotonicMs;
                ProbeResult[] results = await _prober.ProbeRoundAsync(number, targets).ConfigureAwait(false);

                ProbeResult? gatewayResult = gateway is null ? null : results[0];
                ProbeResult[] internetResults = gateway is null ? results : results[1..];
                var round = new ProbeRound(number, sentAtUtc, sentAtMs, gatewayResult, internetResults, adapterDown);

                lock (_sync)
                {
                    // Eine Runde, die noch vor dem Standby gestartet wurde, nicht mehr auswerten.
                    if (_paused)
                    {
                        continue;
                    }

                    _detector.Process(round);
                    PublishState();
                    if (_detector.GatewayIgnoresPings && !_gatewayNoteWritten)
                    {
                        _gatewayNoteWritten = true;
                        _journal.Post(j => j.AddSessionNote(SessionNotes.GatewayIgnoresPing));
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Beenden.
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Messbetrieb abgebrochen: {ex}");
        }
    }

    private void PublishState()
    {
        if (_detector.State == State)
        {
            return;
        }

        State = _detector.State;
        StateChanged?.Invoke(this, State);
    }

    /// <summary>Beendet den Messbetrieb; ein laufender Aussetzer endet mit <c>app_exit</c>.</summary>
    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        lock (_sync)
        {
            _detector.Stop(_clock.MonotonicMs);
            PublishState();
        }

        _cancellation.Dispose();
    }
}
