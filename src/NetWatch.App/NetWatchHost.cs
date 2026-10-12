using Microsoft.Win32;
using NetWatch.App.Logging;
using NetWatch.App.Measurement;
using NetWatch.App.Network;
using NetWatch.Core;
using NetWatch.Core.Logging;

namespace NetWatch.App;

/// <summary>
/// Verbindet Messbetrieb, Netzwerküberwachung und Protokoll und reagiert auf Standby, Aufwachen und Herunterfahren.
/// </summary>
internal sealed class NetWatchHost
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(3);

    private readonly IClock _clock = new SystemClock();
    private readonly NetworkMonitor _network;
    private readonly JournalWriter _journal;
    private readonly MeasurementService _measurement;
    private int _stopped;

    public NetWatchHost()
    {
        _journal = new JournalWriter(AppPaths.DataDirectory, _clock, TimeZoneInfo.Local, out RunState? previous);
        _journal.Post(j => j.Recover(previous));
        _network = new NetworkMonitor();
        _measurement = new MeasurementService(_clock, _network, _journal);
    }

    public MeasurementService Measurement => _measurement;

    public void Start()
    {
        DateTimeOffset now = _clock.UtcNow;
        _journal.Post(j => j.SessionStarted(now, SessionReasons.Start));
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnded += OnSessionEnded;
        _measurement.Start();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        DateTimeOffset now = _clock.UtcNow;
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _measurement.Suspend();
                _journal.Post(j => j.SessionEnded(now, SessionReasons.Suspend));
                break;

            case PowerModes.Resume:
                _journal.Post(j => j.SessionStarted(now, SessionReasons.Resume));
                _measurement.Resume();
                break;
        }
    }

    /// <summary>
    /// Windows wird heruntergefahren oder der Nutzer meldet sich ab. Danach beendet Windows das Programm
    /// womöglich ohne normalen Ablauf, deshalb wird hier sofort alles geschrieben.
    /// </summary>
    private void OnSessionEnded(object sender, SessionEndedEventArgs e) =>
        Task.Run(() => StopAsync(SessionReasons.Shutdown)).Wait(ShutdownTimeout);

    /// <summary>Beendet den Messbetrieb und schreibt Messphase und Zustandsdatei. Mehrfacher Aufruf ist harmlos.</summary>
    public async Task StopAsync(string reason)
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
        {
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionEnded -= OnSessionEnded;

        await _measurement.DisposeAsync().ConfigureAwait(false);
        _network.Dispose();
        DateTimeOffset now = _clock.UtcNow;
        _journal.Post(j => j.SessionEnded(now, reason));
        await _journal.DisposeAsync().ConfigureAwait(false);
    }
}
