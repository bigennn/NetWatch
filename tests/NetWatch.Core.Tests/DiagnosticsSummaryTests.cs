using NetWatch.Core;
using NetWatch.Core.Diagnostics;

namespace NetWatch.Core.Tests;

public class DiagnosticsSummaryTests
{
    private const int IntervalMs = 200;
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Erzeugt Runden im festen Takt; jede Prüfung antwortet nach 10 ms, außer sie ist in <paramref name="lost"/>.</summary>
    private static List<ProbeResult> Rounds(int count, string[] targets, Func<long, string, bool>? lost = null, Func<long, long>? startOfRound = null)
    {
        var results = new List<ProbeResult>();
        for (long round = 0; round < count; round++)
        {
            long startMs = startOfRound?.Invoke(round) ?? round * IntervalMs;
            foreach (string target in targets)
            {
                bool isLost = lost?.Invoke(round, target) ?? false;
                results.Add(new ProbeResult(round, target, Start.AddMilliseconds(startMs), startMs, isLost ? null : 10, isLost ? "TimedOut" : "Success"));
            }
        }

        return results;
    }

    [Fact]
    public void Counts_probes_and_losses_per_target()
    {
        var results = Rounds(10, ["1.1.1.1", "8.8.8.8"], lost: (round, target) => target == "8.8.8.8" && round is 3 or 7);

        DiagnosticsSummary summary = DiagnosticsSummary.Create(results, IntervalMs, measuredMs: 2_000);

        Assert.Equal(10, summary.Rounds);
        Assert.Collection(summary.Targets,
            t => Assert.Equal(("1.1.1.1", 10, 0), (t.Target, t.Probes, t.Lost)),
            t => Assert.Equal(("8.8.8.8", 10, 2), (t.Target, t.Probes, t.Lost)));
    }

    [Fact]
    public void Finds_largest_gap_between_rounds_and_counts_late_rounds()
    {
        // Runde 5 beginnt 600 ms nach Runde 4 statt 200 ms; alle späteren Runden verschieben sich mit.
        var results = Rounds(10, ["1.1.1.1"], startOfRound: round => round * IntervalMs + (round >= 5 ? 400 : 0));

        DiagnosticsSummary summary = DiagnosticsSummary.Create(results, IntervalMs, measuredMs: 2_400);

        Assert.Equal(600, summary.MaxRoundGapMs);
        Assert.Equal(1, summary.LateRounds);
    }

    [Fact]
    public void Rtt_statistics_ignore_lost_probes()
    {
        var results = new List<ProbeResult>
        {
            new(0, "1.1.1.1", Start, 0, 12, "Success"),
            new(1, "1.1.1.1", Start, 200, null, "TimedOut"),
            new(2, "1.1.1.1", Start, 400, 30, "Success"),
            new(3, "1.1.1.1", Start, 600, 15, "Success"),
        };

        TargetStatistics stats = DiagnosticsSummary.Create(results, IntervalMs, measuredMs: 800).Targets.Single();

        Assert.Equal((12L, 15L, 30L), (stats.RttMinMs!.Value, stats.RttMedianMs!.Value, stats.RttMaxMs!.Value));
    }

    [Theory]
    [InlineData(2_970, true)]
    [InlineData(3_030, true)]
    [InlineData(2_969, false)]
    [InlineData(3_031, false)]
    public void Probe_count_tolerance_is_one_percent_of_expected(int probes, bool expected)
    {
        var results = Rounds(probes, ["1.1.1.1"]);

        DiagnosticsSummary summary = DiagnosticsSummary.Create(results, IntervalMs, measuredMs: 600_000);

        Assert.Equal(3_000, summary.ExpectedProbesPerTarget);
        Assert.Equal(expected, summary.ProbeCountWithinTolerance);
    }

    [Fact]
    public void Short_measurement_allows_the_extra_round_at_time_zero()
    {
        var results = Rounds(51, ["1.1.1.1"]);

        DiagnosticsSummary summary = DiagnosticsSummary.Create(results, IntervalMs, measuredMs: 10_000);

        Assert.Equal(50, summary.ExpectedProbesPerTarget);
        Assert.True(summary.ProbeCountWithinTolerance);
    }

    [Fact]
    public void Empty_measurement_has_no_rounds_and_does_not_pass()
    {
        DiagnosticsSummary summary = DiagnosticsSummary.Create([], IntervalMs, measuredMs: 0);

        Assert.Equal(0, summary.Rounds);
        Assert.Empty(summary.Targets);
        Assert.False(summary.ProbeCountWithinTolerance);
    }
}
