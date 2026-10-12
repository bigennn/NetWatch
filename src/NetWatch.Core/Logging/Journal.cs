using NetWatch.Core.Detection;

namespace NetWatch.Core.Logging;

/// <summary>
/// Führt Ereignis- und Laufzeitprotokoll sowie die Zustandsdatei. Alle Methoden werden nacheinander
/// von einem einzigen Schreib-Task aufgerufen; die Klasse selbst ist deshalb nicht threadsicher.
/// </summary>
public sealed class Journal
{
    /// <summary>Spätestens so oft wird die Zustandsdatei als Lebenszeichen neu geschrieben.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);

    private readonly BufferedLog _events;
    private readonly BufferedLog _sessions;
    private readonly Action<RunState?> _saveState;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _timeZone;
    private RunningSession? _session;
    private OutageStart? _openOutage;
    private DateTimeOffset _lastSaveUtc = DateTimeOffset.MinValue;

    /// <param name="saveState">Speichert die Zustandsdatei; <c>null</c> heißt: Datei löschen.</param>
    public Journal(ILineSink events, ILineSink sessions, Action<RunState?> saveState, IClock clock, TimeZoneInfo timeZone)
    {
        _events = new BufferedLog(events);
        _sessions = new BufferedLog(sessions);
        _saveState = saveState;
        _clock = clock;
        _timeZone = timeZone;
    }

    public IReadOnlyList<string> PendingEventLines => _events.Pending;

    public IReadOnlyList<string> PendingSessionLines => _sessions.Pending;

    /// <summary>
    /// Wertet die Zustandsdatei des letzten Laufs aus: holt ungeschriebene Zeilen nach und schließt nach einem
    /// Absturz die Messphase (<c>crash_detected</c>) und einen offenen Aussetzer (<c>end_unknown</c>) ab.
    /// Das Ende ist das letzte Lebenszeichen; die Dauer stammt hier ausnahmsweise aus der Systemuhr,
    /// weil die monotone Uhr den Programmneustart nicht überlebt.
    /// </summary>
    public void Recover(RunState? previous)
    {
        if (previous is null)
        {
            return;
        }

        foreach (string line in previous.PendingEventLines)
        {
            _events.Add(line);
        }

        foreach (string line in previous.PendingSessionLines)
        {
            _sessions.Add(line);
        }

        DateTimeOffset heartbeat = previous.LastHeartbeatUtc;
        if (previous.OpenOutage is { } open)
        {
            long durationMs = (long)Math.Max(0, (heartbeat - open.StartUtc).TotalMilliseconds);
            var outage = new Outage(
                open.StartUtc,
                open.StartUtc.AddMilliseconds(durationMs),
                durationMs,
                open.Cause,
                open.FailedTargets,
                open.Gateway,
                OutageNotes.EndUnknown);
            _events.Add(EventCsv.FormatLine(outage, _timeZone));
        }

        if (previous.Session is { } session)
        {
            var crashed = new Session(
                session.StartUtc,
                session.StartReason,
                heartbeat > session.StartUtc ? heartbeat : session.StartUtc,
                SessionReasons.CrashDetected,
                [.. session.Notes, SessionNotes.EndEstimated]);
            _sessions.Add(SessionCsv.FormatLine(crashed, _timeZone));
        }

        SaveState();
    }

    public void SessionStarted(DateTimeOffset startUtc, string reason)
    {
        _session = new RunningSession(startUtc, reason, []);
        SaveState();
    }

    public void SessionEnded(DateTimeOffset endUtc, string reason)
    {
        if (_session is null)
        {
            return;
        }

        _sessions.Add(SessionCsv.FormatLine(new Session(_session.StartUtc, _session.StartReason, endUtc, reason, _session.Notes), _timeZone));
        _session = null;
        SaveState();
    }

    /// <summary>Vermerkt einen Hinweis zur laufenden Messphase (jeder Hinweis nur einmal).</summary>
    public void AddSessionNote(string note)
    {
        if (_session is null || _session.Notes.Contains(note))
        {
            return;
        }

        _session = _session with { Notes = [.. _session.Notes, note] };
        SaveState();
    }

    public void OutageStarted(OutageStart start)
    {
        _openOutage = start;
        SaveState();
    }

    public void OutageEnded(Outage outage)
    {
        _openOutage = null;
        _events.Add(EventCsv.FormatLine(outage, _timeZone));
        SaveState();
    }

    /// <summary>
    /// Regelmäßig aufrufen (alle paar Sekunden): schreibt gepufferte Zeilen nach und erneuert das Lebenszeichen.
    /// </summary>
    public void Tick()
    {
        int pendingBefore = _events.Pending.Count + _sessions.Pending.Count;
        _events.Flush();
        _sessions.Flush();
        int pendingAfter = _events.Pending.Count + _sessions.Pending.Count;

        if (pendingAfter != pendingBefore || _clock.UtcNow - _lastSaveUtc >= HeartbeatInterval)
        {
            SaveState();
        }
    }

    /// <summary>
    /// Beim Beenden: letzter Schreibversuch. Ist alles geschrieben, wird die Zustandsdatei gelöscht;
    /// sonst bleibt sie mit den ungeschriebenen Zeilen (ohne laufende Messphase) für den nächsten Start.
    /// </summary>
    public void Close()
    {
        _events.Flush();
        _sessions.Flush();
        _session = null;
        _openOutage = null;

        if (_events.Pending.Count == 0 && _sessions.Pending.Count == 0)
        {
            _saveState(null);
        }
        else
        {
            SaveState();
        }
    }

    private void SaveState()
    {
        DateTimeOffset now = _clock.UtcNow;
        _saveState(new RunState
        {
            Session = _session,
            LastHeartbeatUtc = now,
            OpenOutage = _openOutage,
            PendingEventLines = [.. _events.Pending],
            PendingSessionLines = [.. _sessions.Pending],
        });
        _lastSaveUtc = now;
    }
}
