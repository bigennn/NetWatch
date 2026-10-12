using NetWatch.Core.Detection;

namespace NetWatch.Core.Tests.Detection;

/// <summary>
/// Simulierter Prüfer: erzeugt Prüfrunden im festen Takt aus Ausfallmustern.
/// Zeiten sind absolute UTC-Zeitpunkte; ein Muster gilt für Runden mit <c>from &lt;= Sendezeit &lt; to</c>.
/// </summary>
internal sealed class Scenario
{
    public const string GatewayAddress = "192.168.178.1";
    public const string Cloudflare = "1.1.1.1";
    public const string Google = "8.8.8.8";

    // Beliebiger Startwert der monotonen Uhr: Dauern dürfen nie aus absoluten Werten abgeleitet werden.
    private const long MonotonicOffsetMs = 7_000_000;

    private readonly List<(DateTimeOffset From, DateTimeOffset To, Fault Fault, string? Target)> _faults = [];
    private bool _gatewayIgnoresPings;
    private bool _noGateway;

    public Scenario(int intervalMs = 200) => IntervalMs = intervalMs;

    private enum Fault { InternetDown, RouterDown, AdapterDown, AdapterDownWithoutFlag, SingleProbeLost }

    public int IntervalMs { get; }

    public static DateTimeOffset Utc(int hour, int minute, int second, int millisecond = 0, int day = 13) =>
        new(2026, 10, day, hour, minute, second, millisecond, TimeSpan.Zero);

    /// <summary>Kein Internetziel antwortet, der Router schon (Providerausfall).</summary>
    public Scenario InternetDown(DateTimeOffset from, DateTimeOffset to) => Add(from, to, Fault.InternetDown);

    /// <summary>Router und Internetziele antworten nicht (Router-Neustart).</summary>
    public Scenario RouterDown(DateTimeOffset from, DateTimeOffset to) => Add(from, to, Fault.RouterDown);

    /// <summary>Adapter getrennt: Windows meldet "Down", jeder Ping liefert sofort 11050.</summary>
    public Scenario AdapterDown(DateTimeOffset from, DateTimeOffset to) => Add(from, to, Fault.AdapterDown);

    /// <summary>Wie <see cref="AdapterDown"/>, aber die Adaptermeldung fehlt; nur der Ping-Status zeigt es.</summary>
    public Scenario AdapterDownOnlyPingStatus(DateTimeOffset from, DateTimeOffset to) => Add(from, to, Fault.AdapterDownWithoutFlag);

    /// <summary>Genau eine Prüfung an einem Ziel geht verloren.</summary>
    public Scenario LoseProbe(DateTimeOffset at, string target) => Add(at, at.AddMilliseconds(1), Fault.SingleProbeLost, target);

    /// <summary>Der Router beantwortet grundsätzlich keine Pings.</summary>
    public Scenario GatewayIgnoresPings()
    {
        _gatewayIgnoresPings = true;
        return this;
    }

    /// <summary>Es ist kein Gateway bekannt (keine Standardroute ermittelt).</summary>
    public Scenario WithoutGateway()
    {
        _noGateway = true;
        return this;
    }

    public IEnumerable<ProbeRound> Rounds(DateTimeOffset from, DateTimeOffset to)
    {
        long number = 0;
        for (DateTimeOffset t = from; t < to; t = t.AddMilliseconds(IntervalMs))
        {
            yield return CreateRound(number++, t, from);
        }
    }

    /// <summary>Lässt einen Detektor über alle Runden laufen und sammelt die abgeschlossenen Aussetzer.</summary>
    public List<Outage> Run(OutageDetector detector, DateTimeOffset from, DateTimeOffset to)
    {
        var outages = new List<Outage>();
        detector.OutageEnded += (_, outage) => outages.Add(outage);
        foreach (ProbeRound round in Rounds(from, to))
        {
            detector.Process(round);
        }

        return outages;
    }

    public static long MonotonicMs(DateTimeOffset t, DateTimeOffset origin) =>
        MonotonicOffsetMs + (long)(t - origin).TotalMilliseconds;

    private Scenario Add(DateTimeOffset from, DateTimeOffset to, Fault fault, string? target = null)
    {
        _faults.Add((from, to, fault, target));
        return this;
    }

    private bool Has(DateTimeOffset t, Fault fault, string? target = null) =>
        _faults.Any(f => f.Fault == fault && f.From <= t && t < f.To && (target is null || f.Target == target));

    private ProbeRound CreateRound(long number, DateTimeOffset t, DateTimeOffset origin)
    {
        bool adapterDown = Has(t, Fault.AdapterDown) || Has(t, Fault.AdapterDownWithoutFlag);
        bool routerDown = Has(t, Fault.RouterDown);
        bool internetDown = routerDown || Has(t, Fault.InternetDown);
        long monotonicMs = MonotonicMs(t, origin);

        ProbeResult Probe(string target, bool down, long rttMs)
        {
            if (adapterDown)
            {
                return new ProbeResult(number, target, t, monotonicMs, null, ProbeResult.GeneralFailureStatus);
            }

            bool lost = down || Has(t, Fault.SingleProbeLost, target);
            return new ProbeResult(number, target, t, monotonicMs, lost ? null : rttMs, lost ? "TimedOut" : "Success");
        }

        ProbeResult? gateway = _noGateway ? null : Probe(GatewayAddress, routerDown || _gatewayIgnoresPings, 1);
        ProbeResult[] internet = [Probe(Cloudflare, internetDown, 11), Probe(Google, internetDown, 13)];
        return new ProbeRound(number, t, monotonicMs, gateway, internet, AdapterDown: Has(t, Fault.AdapterDown));
    }
}
