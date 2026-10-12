namespace NetWatch.Core.Detection;

/// <summary>
/// Erkennt Aussetzer aus Prüfrunden (Zustandsautomat ohne Windows-Abhängigkeit).
/// </summary>
/// <remarks>
/// Regeln (siehe docs/decisions.md, F4, F5, F7):
/// <list type="bullet">
/// <item>Eine Runde ist verloren, wenn kein Internetziel antwortet.</item>
/// <item>Ein Aussetzer beginnt erst ab <c>minLostRounds</c> verlorenen Runden in Folge; der Beginn wird
/// auf die erste dieser Runden rückdatiert.</item>
/// <item>Er endet mit der ersten Runde, in der ein Internetziel antwortet.</item>
/// <item>Die Ursache ist die am weitesten vorne liegende, die während des Aussetzers auftrat:
/// <c>PcLan</c> vor <c>Router</c> vor <c>Unknown</c> vor <c>Provider</c>.</item>
/// </list>
/// </remarks>
public sealed class OutageDetector
{
    public const int DefaultMinLostRounds = 2;

    /// <summary>
    /// So viele Runden in Folge mit Internet, aber ohne Antwort des Routers, und der Router gilt
    /// als "beantwortet keine Pings". Dann lässt sich Router von Provider nicht unterscheiden.
    /// </summary>
    public const int SilentGatewayRounds = 5;

    /// <summary>
    /// Anlaufphase nach dem Start und nach dem Aufwachen: Verlorene Runden zählen erst ab der ersten
    /// beantworteten Runde, spätestens nach dieser Zeit. Sonst erzeugte jedes Aufwachen einen falschen
    /// Aussetzer, weil der Netzwerkadapter einige Sekunden zum Verbinden braucht.
    /// </summary>
    public const int DefaultWarmupMs = 30_000;

    private readonly int _minLostRounds;
    private readonly int _warmupMs;
    private bool _warmingUp = true;
    private long? _warmupStartMs;
    private readonly List<(ProbeRound Round, OutageCause Cause)> _pendingLost = [];
    private RunningOutage? _current;
    private string? _gatewayAddress;
    private int _silentGatewayRounds;

    public OutageDetector(int minLostRounds = DefaultMinLostRounds, int warmupMs = DefaultWarmupMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLostRounds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupMs);
        _minLostRounds = minLostRounds;
        _warmupMs = warmupMs;
    }

    /// <summary>Wird gemeldet, sobald ein Aussetzer feststeht (nach der letzten nötigen verlorenen Runde).</summary>
    public event EventHandler<OutageStart>? OutageStarted;

    /// <summary>Wird gemeldet, wenn ein Aussetzer abgeschlossen ist.</summary>
    public event EventHandler<Outage>? OutageEnded;

    public ConnectionState State { get; private set; } = ConnectionState.Paused;

    /// <summary><c>true</c>, wenn der Router zuletzt mehrere Runden lang nicht antwortete, obwohl das Internet erreichbar war.</summary>
    public bool GatewayIgnoresPings => _silentGatewayRounds >= SilentGatewayRounds;

    public void Process(ProbeRound round)
    {
        TrackGateway(round);

        if (_warmingUp)
        {
            _warmupStartMs ??= round.SentAtMonotonicMs;
            if (round.InternetLost && round.SentAtMonotonicMs - _warmupStartMs < _warmupMs)
            {
                return;
            }

            _warmingUp = false;
        }

        if (!round.InternetLost)
        {
            if (_current is not null)
            {
                End(round.SentAtMonotonicMs, note: null);
            }

            _pendingLost.Clear();
            State = ConnectionState.Connected;
            return;
        }

        OutageCause cause = Classify(round);
        if (_current is not null)
        {
            _current.Add(round, cause);
            return;
        }

        _pendingLost.Add((round, cause));
        if (_pendingLost.Count < _minLostRounds)
        {
            return;
        }

        (ProbeRound first, _) = _pendingLost[0];
        _current = new RunningOutage(first.SentAtUtc, first.SentAtMonotonicMs, first.Gateway?.Target);
        foreach ((ProbeRound lostRound, OutageCause lostCause) in _pendingLost)
        {
            _current.Add(lostRound, lostCause);
        }

        _pendingLost.Clear();
        State = ConnectionState.Outage;
        OutageStarted?.Invoke(this, new OutageStart(_current.StartUtc, _current.Cause, _current.Gateway, _current.FailedTargets));
    }

    /// <summary>
    /// Der PC geht in den Standby: ein laufender Aussetzer endet jetzt (Hinweis <c>suspended</c>),
    /// eine angefangene Serie verlorener Runden wird verworfen.
    /// </summary>
    public void Suspend(long atMonotonicMs) => Pause(atMonotonicMs, OutageNotes.Suspended);

    /// <summary>NetWatch wird beendet: ein laufender Aussetzer endet jetzt (Hinweis <c>app_exit</c>).</summary>
    public void Stop(long atMonotonicMs) => Pause(atMonotonicMs, OutageNotes.AppExit);

    private void Pause(long atMonotonicMs, string note)
    {
        if (_current is not null)
        {
            End(atMonotonicMs, note);
        }

        _pendingLost.Clear();
        _warmingUp = true;
        _warmupStartMs = null;
        State = ConnectionState.Paused;
    }

    private void End(long endMonotonicMs, string? note)
    {
        RunningOutage current = _current!;
        _current = null;

        long durationMs = Math.Max(0, endMonotonicMs - current.StartMonotonicMs);
        var outage = new Outage(
            current.StartUtc,
            current.StartUtc.AddMilliseconds(durationMs),
            durationMs,
            current.Cause,
            current.FailedTargets,
            current.Gateway,
            note);

        OutageEnded?.Invoke(this, outage);
    }

    private OutageCause Classify(ProbeRound round)
    {
        if (round.AdapterDown || round.AllProbes.All(p => p.Status == ProbeResult.GeneralFailureStatus))
        {
            return OutageCause.PcLan;
        }

        if (round.Gateway is null || GatewayIgnoresPings)
        {
            return OutageCause.Unknown;
        }

        return round.Gateway.IsLost ? OutageCause.Router : OutageCause.Provider;
    }

    /// <summary>
    /// Merkt sich, ob der Router Pings beantwortet. Bewertet werden nur Runden mit Internet,
    /// denn nur dort ist ein schweigender Router kein Ausfall, sondern eine Router-Einstellung.
    /// </summary>
    private void TrackGateway(ProbeRound round)
    {
        if (round.Gateway is null)
        {
            return;
        }

        if (round.Gateway.Target != _gatewayAddress)
        {
            _gatewayAddress = round.Gateway.Target;
            _silentGatewayRounds = 0;
        }

        if (!round.Gateway.IsLost)
        {
            _silentGatewayRounds = 0;
        }
        else if (!round.InternetLost)
        {
            _silentGatewayRounds++;
        }
    }

    private static int Rank(OutageCause cause) => cause switch
    {
        OutageCause.PcLan => 0,
        OutageCause.Router => 1,
        OutageCause.Unknown => 2,
        OutageCause.Provider => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };

    /// <summary>Daten eines laufenden Aussetzers.</summary>
    private sealed class RunningOutage(DateTimeOffset startUtc, long startMonotonicMs, string? gateway)
    {
        private readonly List<string> _failedTargets = [];

        public DateTimeOffset StartUtc { get; } = startUtc;

        public long StartMonotonicMs { get; } = startMonotonicMs;

        public string? Gateway { get; } = gateway;

        public OutageCause Cause { get; private set; } = OutageCause.Provider;

        /// <summary>Gateway zuerst, danach die übrigen Ziele in der Reihenfolge ihres ersten Ausfalls.</summary>
        public IReadOnlyList<string> FailedTargets => _failedTargets.OrderBy(t => t == Gateway ? 0 : 1).ToList();

        public void Add(ProbeRound round, OutageCause cause)
        {
            if (Rank(cause) < Rank(Cause))
            {
                Cause = cause;
            }

            foreach (ProbeResult probe in round.AllProbes.Where(p => p.IsLost))
            {
                if (!_failedTargets.Contains(probe.Target))
                {
                    _failedTargets.Add(probe.Target);
                }
            }
        }
    }
}
