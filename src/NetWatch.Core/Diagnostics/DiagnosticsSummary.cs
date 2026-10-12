namespace NetWatch.Core.Diagnostics;

/// <summary>
/// Kennzahlen eines Prüfziels aus einer Diagnose-Messung.
/// </summary>
public sealed record TargetStatistics(
    string Target,
    int Probes,
    int Lost,
    long? RttMinMs,
    long? RttMedianMs,
    long? RttMaxMs);

/// <summary>
/// Auswertung einer Diagnose-Messung (Spike S1, Akzeptanztest F-01).
/// </summary>
public sealed record DiagnosticsSummary(
    IReadOnlyList<TargetStatistics> Targets,
    int Rounds,
    long MaxRoundGapMs,
    int LateRounds,
    int ExpectedProbesPerTarget)
{
    /// <summary>Erlaubte Abweichung der Prüfungszahl je Ziel (F-01: ± 1 %).</summary>
    public const double ProbeCountTolerance = 0.01;

    /// <summary>
    /// Mindestens eine Prüfung Abweichung ist immer erlaubt, weil die erste Runde schon bei 0 ms startet
    /// (in 10 s also 51 statt 50 Runden); bei 10 Minuten wirkt nur die 1-%-Grenze.
    /// </summary>
    public bool ProbeCountWithinTolerance =>
        Targets.Count > 0
        && Targets.All(t => Math.Abs(t.Probes - ExpectedProbesPerTarget) <= Math.Max(1, ExpectedProbesPerTarget * ProbeCountTolerance));

    /// <summary>
    /// Wertet die Prüfergebnisse aus. Eine Runde gilt als verspätet, wenn sie mehr als 1,5 Takte nach der vorigen beginnt.
    /// </summary>
    public static DiagnosticsSummary Create(IReadOnlyCollection<ProbeResult> results, int intervalMs, long measuredMs)
    {
        List<TargetStatistics> targets = results
            .GroupBy(r => r.Target)
            .Select(g => CreateTargetStatistics(g.Key, g.ToList()))
            .ToList();

        List<long> roundStarts = results
            .GroupBy(r => r.Round)
            .Select(g => g.Min(r => r.SentAtMonotonicMs))
            .Order()
            .ToList();

        long maxGap = 0;
        int lateRounds = 0;
        for (int i = 1; i < roundStarts.Count; i++)
        {
            long gap = roundStarts[i] - roundStarts[i - 1];
            maxGap = Math.Max(maxGap, gap);
            if (gap * 2 > intervalMs * 3)
            {
                lateRounds++;
            }
        }

        return new DiagnosticsSummary(targets, roundStarts.Count, maxGap, lateRounds, (int)(measuredMs / intervalMs));
    }

    private static TargetStatistics CreateTargetStatistics(string target, List<ProbeResult> probes)
    {
        List<long> rtts = probes
            .Where(p => p.RttMs is not null)
            .Select(p => p.RttMs!.Value)
            .Order()
            .ToList();

        int lost = probes.Count - rtts.Count;
        if (rtts.Count == 0)
        {
            return new TargetStatistics(target, probes.Count, lost, null, null, null);
        }

        return new TargetStatistics(target, probes.Count, lost, rtts[0], rtts[rtts.Count / 2], rtts[^1]);
    }
}
