using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NetWatch.App.Cli;
using NetWatch.App.Network;
using NetWatch.Core;
using NetWatch.Core.Diagnostics;

namespace NetWatch.App.Diagnostics;

/// <summary>
/// Diagnose-Modus <c>--diagnose &lt;Sekunden&gt;</c> (Spikes S1 und S3): ermittelt das Gateway,
/// pingt Gateway und Internetziele im Prüftakt und gibt eine Auswertung aus.
/// </summary>
internal static class DiagnoseCommand
{
    private const int IntervalMs = 200;
    private const int TimeoutMs = 180;
    private const int ProgressEveryMs = 60_000;
    private static readonly IPAddress[] InternetTargets = [IPAddress.Parse("1.1.1.1"), IPAddress.Parse("8.8.8.8")];

    public static int Run(int durationSeconds)
    {
        ParentConsole.Attach();
        return RunAsync(durationSeconds).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(int durationSeconds)
    {
        TimeZoneInfo localZone = TimeZoneInfo.Local;
        string Now() => Timestamps.FormatLocal(DateTimeOffset.UtcNow, localZone);

        string filePath = Path.Combine(AppPaths.DataDirectory, $"diagnose_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        await using var log = new DiagnosticLog(filePath);

        log.Write($"NetWatch-Diagnose {typeof(DiagnoseCommand).Assembly.GetName().Version}");
        log.Write($"Start: {Now()} ({Timestamps.FormatUtc(DateTimeOffset.UtcNow)}), Dauer {durationSeconds} s, Takt {IntervalMs} ms, Zeitlimit {TimeoutMs} ms");
        log.Write($"Rechner: {Environment.ProcessorCount} logische Kerne, {RuntimeInformation.OSDescription}");

        // S3: Gateway ermitteln und prüfen, ob es Pings beantwortet.
        GatewayInfo? gateway = GatewayResolver.Resolve();
        var targets = new List<IPAddress>();
        if (gateway is null)
        {
            log.Write("Gateway: nicht gefunden (keine Route ins Internet)");
        }
        else
        {
            log.Write($"Adapter: {gateway.AdapterName} ({gateway.AdapterDescription}), Typ {gateway.AdapterType}, Status {gateway.AdapterStatus}");
            log.Write($"Gateway: {gateway.Gateway}");
            bool answers = await AnswersPingAsync(gateway.Gateway);
            log.Write($"Gateway beantwortet Pings: {(answers ? "ja" : "nein")}");
            targets.Add(gateway.Gateway);
        }

        targets.AddRange(InternetTargets);
        log.Write($"Prüfziele: {string.Join(", ", targets)}");
        log.Write("");

        // Für Lücke L2 (pc_lan): Was meldet Windows, wenn der Adapter getrennt wird?
        NetworkAvailabilityChangedEventHandler onAvailability = (_, e) =>
            log.Write($"{Now()}  Ereignis: Netzwerk verfügbar = {e.IsAvailable}; {DescribeRoute()}");
        NetworkAddressChangedEventHandler onAddress = (_, _) =>
            log.Write($"{Now()}  Ereignis: Netzwerkadresse geändert; {DescribeRoute()}");
        NetworkChange.NetworkAvailabilityChanged += onAvailability;
        NetworkChange.NetworkAddressChanged += onAddress;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var clock = new SystemClock();
        var prober = new IcmpProber(clock, TimeoutMs);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMs));
        var results = new List<ProbeResult>(durationSeconds * 1000 / IntervalMs * targets.Count + 64);
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuAtStart = process.TotalProcessorTime;

        long round = 0;
        long longestRoundMs = 0;
        int roundsLongerThanInterval = 0;
        long nextProgressMs = ProgressEveryMs;
        try
        {
            do
            {
                long roundStartMs = clock.MonotonicMs;
                ProbeResult[] roundResults = await prober.ProbeRoundAsync(round, targets);
                long roundMs = clock.MonotonicMs - roundStartMs;

                longestRoundMs = Math.Max(longestRoundMs, roundMs);
                if (roundMs > IntervalMs)
                {
                    roundsLongerThanInterval++;
                }

                results.AddRange(roundResults);
                if (roundResults.Any(r => r.IsLost))
                {
                    log.Write($"{Now()}  Runde {round}: {string.Join(", ", roundResults.Select(Describe))}");
                }

                round++;
                if (clock.MonotonicMs >= nextProgressMs)
                {
                    log.Write($"{Now()}  {clock.MonotonicMs / 1000} s gemessen, {round} Runden");
                    nextProgressMs += ProgressEveryMs;
                }
            }
            while (await timer.WaitForNextTickAsync(cancellation.Token));
        }
        catch (OperationCanceledException)
        {
            // Messdauer abgelaufen oder Strg+C.
        }
        finally
        {
            NetworkChange.NetworkAvailabilityChanged -= onAvailability;
            NetworkChange.NetworkAddressChanged -= onAddress;
        }

        long measuredMs = clock.MonotonicMs;
        process.Refresh();
        double cpuPercent = (process.TotalProcessorTime - cpuAtStart).TotalMilliseconds
            / (measuredMs * Environment.ProcessorCount) * 100;

        DiagnosticsSummary summary = DiagnosticsSummary.Create(results, IntervalMs, measuredMs);
        log.Write("");
        log.Write("=== Ergebnis ===");
        log.Write($"Messdauer: {measuredMs / 1000.0:F1} s, Runden: {summary.Rounds}");
        log.Write($"Größter Abstand zwischen zwei Runden: {summary.MaxRoundGapMs} ms; verspätete Runden (> {IntervalMs * 3 / 2} ms): {summary.LateRounds}");
        log.Write($"Längste Runde: {longestRoundMs} ms; Runden länger als der Takt: {roundsLongerThanInterval}");
        log.Write($"{"Ziel",-16} {"Prüfungen",10} {"Verluste",9}   RTT min / Median / max");
        foreach (TargetStatistics t in summary.Targets)
        {
            string rtt = t.RttMinMs is null ? "-" : $"{t.RttMinMs} / {t.RttMedianMs} / {t.RttMaxMs} ms";
            log.Write($"{t.Target,-16} {t.Probes,10} {t.Lost,9}   {rtt}");
        }

        log.Write($"Erwartet je Ziel: {summary.ExpectedProbesPerTarget} ± 1 %  ->  {(summary.ProbeCountWithinTolerance ? "bestanden" : "NICHT bestanden")}");
        log.Write($"CPU-Last (Mittel über alle Kerne): {cpuPercent:F2} %");
        log.Write($"Arbeitsspeicher: {process.WorkingSet64 / 1024 / 1024} MB (Spitze {process.PeakWorkingSet64 / 1024 / 1024} MB)");
        log.Write($"Bericht gespeichert: {log.FilePath}");

        return summary.ProbeCountWithinTolerance ? 0 : 1;
    }

    private static string Describe(ProbeResult r) =>
        r.IsLost ? $"{r.Target}={r.Status}" : $"{r.Target}={r.RttMs} ms";

    private static string DescribeRoute()
    {
        GatewayInfo? gateway = GatewayResolver.Resolve();
        return gateway is null
            ? "keine Route ins Internet"
            : $"Gateway {gateway.Gateway} über {gateway.AdapterName} ({gateway.AdapterStatus})";
    }

    private static async Task<bool> AnswersPingAsync(IPAddress address)
    {
        using var ping = new Ping();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if ((await ping.SendPingAsync(address, 1000)).Status == IPStatus.Success)
                {
                    return true;
                }
            }
            catch (PingException)
            {
                // Zählt als keine Antwort.
            }
        }

        return false;
    }
}
