using NetWatch.Core.Detection;
using static NetWatch.Core.Tests.Detection.Scenario;

namespace NetWatch.Core.Tests.Detection;

public class OutageDetectorTests
{
    private const int Tick = 200;

    // ---- Akzeptanzkriterien F-02 ----

    [Fact]
    public void F02_internet_down_for_1500_ms_gives_exactly_one_outage_with_start_and_duration_within_one_tick()
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 0, 1, 500));

        List<Outage> outages = scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10));

        Outage outage = Assert.Single(outages);
        Assert.InRange((outage.StartUtc - Utc(12, 0, 0)).TotalMilliseconds, -Tick, Tick);
        Assert.InRange(outage.DurationMs, 1_500 - Tick, 1_500 + Tick);
        Assert.Equal(OutageCause.Provider, outage.Cause);
        Assert.Null(outage.Note);
    }

    [Fact]
    public void F02_single_lost_probe_at_one_target_is_no_outage()
    {
        var scenario = new Scenario().LoseProbe(Utc(12, 0, 0), Google);

        List<Outage> outages = scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10));

        Assert.Empty(outages);
    }

    // ---- Akzeptanzkriterien F-03 ----

    [Fact]
    public void F03_router_and_internet_down_gives_cause_router()
    {
        var scenario = new Scenario().RouterDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.Router, outage.Cause);
    }

    [Fact]
    public void F03_only_internet_down_gives_cause_provider()
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.Provider, outage.Cause);
    }

    [Fact]
    public void F03_adapter_disconnected_gives_cause_pc_lan()
    {
        var scenario = new Scenario().AdapterDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.PcLan, outage.Cause);
    }

    [Fact]
    public void F03_general_failure_status_alone_gives_cause_pc_lan()
    {
        var scenario = new Scenario().AdapterDownOnlyPingStatus(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.PcLan, outage.Cause);
    }

    // ---- Mindestzahl verlorener Runden ----

    [Fact]
    public void One_completely_lost_round_is_no_outage()
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 0, 0, Tick));

        Assert.Empty(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));
    }

    [Fact]
    public void Two_lost_rounds_in_a_row_are_one_outage_of_two_ticks()
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 0, 0, 2 * Tick));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(Utc(12, 0, 0), outage.StartUtc);
        Assert.Equal(2 * Tick, outage.DurationMs);
    }

    [Fact]
    public void One_internet_target_down_while_the_other_answers_is_no_outage()
    {
        var scenario = new Scenario();
        for (DateTimeOffset t = Utc(12, 0, 0); t < Utc(12, 0, 5); t = t.AddMilliseconds(Tick))
        {
            scenario.LoseProbe(t, Cloudflare);
        }

        Assert.Empty(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));
    }

    [Fact]
    public void Lost_rounds_separated_by_an_answer_do_not_add_up()
    {
        var scenario = new Scenario()
            .InternetDown(Utc(12, 0, 0), Utc(12, 0, 0, Tick))
            .InternetDown(Utc(12, 0, 0, 2 * Tick), Utc(12, 0, 0, 3 * Tick));

        Assert.Empty(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));
    }

    // ---- Ursachen über den Verlauf ----

    [Fact]
    public void Router_restart_with_later_provider_phase_is_one_outage_with_cause_router()
    {
        // Fritzbox-Neustart: erst antwortet nichts, dann ist das LAN zurück, aber DSL synchronisiert noch.
        var scenario = new Scenario()
            .RouterDown(Utc(2, 0, 3), Utc(2, 1, 10))
            .InternetDown(Utc(2, 1, 10), Utc(2, 2, 30));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(1, 59, 0), Utc(2, 5, 0)));

        Assert.Equal(OutageCause.Router, outage.Cause);
        Assert.Equal(Utc(2, 0, 3, 0), outage.StartUtc);
        Assert.Equal(147_000, outage.DurationMs);
    }

    [Fact]
    public void Pc_lan_outranks_router_within_the_same_outage()
    {
        var scenario = new Scenario()
            .AdapterDown(Utc(12, 0, 0), Utc(12, 0, 3))
            .RouterDown(Utc(12, 0, 3), Utc(12, 0, 6));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.PcLan, outage.Cause);
    }

    [Fact]
    public void Failed_targets_contain_every_target_that_failed_during_the_outage_gateway_first()
    {
        var scenario = new Scenario()
            .InternetDown(Utc(12, 0, 0), Utc(12, 0, 2))
            .RouterDown(Utc(12, 0, 2), Utc(12, 0, 4));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(new[] { GatewayAddress, Cloudflare, Google }, outage.FailedTargets);
        Assert.Equal(GatewayAddress, outage.Gateway);
    }

    [Fact]
    public void Router_that_never_answers_pings_gives_cause_unknown()
    {
        var detector = new OutageDetector();
        var scenario = new Scenario().GatewayIgnoresPings().InternetDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(detector, Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.True(detector.GatewayIgnoresPings);
        Assert.Equal(OutageCause.Unknown, outage.Cause);
    }

    [Fact]
    public void Without_known_gateway_the_cause_is_unknown()
    {
        var scenario = new Scenario().WithoutGateway().InternetDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.Unknown, outage.Cause);
        Assert.Null(outage.Gateway);
    }

    [Fact]
    public void Occasional_lost_gateway_ping_does_not_mark_router_as_ignoring_pings()
    {
        var detector = new OutageDetector();
        var scenario = new Scenario()
            .LoseProbe(Utc(11, 59, 59, 400), GatewayAddress)
            .LoseProbe(Utc(11, 59, 59, 600), GatewayAddress)
            .RouterDown(Utc(12, 0, 0), Utc(12, 0, 5));

        Outage outage = Assert.Single(scenario.Run(detector, Utc(11, 59, 50), Utc(12, 0, 10)));

        Assert.Equal(OutageCause.Router, outage.Cause);
    }

    // ---- Zustand und Meldungen ----

    [Fact]
    public void State_goes_paused_connected_outage_connected_and_start_is_reported_backdated()
    {
        var detector = new OutageDetector();
        var started = new List<OutageStart>();
        detector.OutageStarted += (_, start) => started.Add(start);
        ProbeRound[] rounds = new Scenario()
            .RouterDown(Utc(12, 0, 0, 200), Utc(12, 0, 0, 600))
            .Rounds(Utc(12, 0, 0), Utc(12, 0, 1))
            .ToArray();

        Assert.Equal(ConnectionState.Paused, detector.State);

        detector.Process(rounds[0]);                 // 12:00:00.000 Antwort
        Assert.Equal(ConnectionState.Connected, detector.State);

        detector.Process(rounds[1]);                 // 12:00:00.200 verloren, noch kein Aussetzer
        Assert.Equal(ConnectionState.Connected, detector.State);
        Assert.Empty(started);

        detector.Process(rounds[2]);                 // 12:00:00.400 verloren, Aussetzer steht fest
        Assert.Equal(ConnectionState.Outage, detector.State);
        OutageStart start = Assert.Single(started);
        Assert.Equal(Utc(12, 0, 0, 200), start.StartUtc);
        Assert.Equal(OutageCause.Router, start.Cause);

        detector.Process(rounds[3]);                 // 12:00:00.600 Antwort
        Assert.Equal(ConnectionState.Connected, detector.State);
    }

    [Fact]
    public void Suspend_during_outage_ends_it_with_note_suspended_and_pauses()
    {
        var detector = new OutageDetector();
        var outages = new List<Outage>();
        detector.OutageEnded += (_, o) => outages.Add(o);
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 1, 0));
        foreach (ProbeRound round in scenario.Rounds(Utc(11, 59, 59), Utc(12, 0, 3)))
        {
            detector.Process(round);
        }

        detector.Suspend(MonotonicMs(Utc(12, 0, 3), Utc(11, 59, 59)));

        Outage outage = Assert.Single(outages);
        Assert.Equal(OutageNotes.Suspended, outage.Note);
        Assert.Equal(3_000, outage.DurationMs);
        Assert.Equal(ConnectionState.Paused, detector.State);
    }

    [Fact]
    public void Suspend_after_a_single_lost_round_reports_nothing()
    {
        var detector = new OutageDetector();
        var outages = new List<Outage>();
        var started = new List<OutageStart>();
        detector.OutageEnded += (_, o) => outages.Add(o);
        detector.OutageStarted += (_, s) => started.Add(s);
        ProbeRound[] rounds = new Scenario().InternetDown(Utc(12, 0, 0, 200), Utc(12, 1, 0)).Rounds(Utc(12, 0, 0), Utc(12, 0, 1)).ToArray();

        detector.Process(rounds[0]);
        detector.Process(rounds[1]);
        detector.Suspend(rounds[1].SentAtMonotonicMs + 100);
        detector.Process(rounds[2]);   // nach dem Aufwachen: erst eine verlorene Runde, noch keine gültige Aussage

        Assert.Empty(outages);
        Assert.Empty(started);
        Assert.Equal(ConnectionState.Paused, detector.State);
    }

    [Fact]
    public void Stop_during_outage_ends_it_with_note_app_exit()
    {
        var detector = new OutageDetector();
        var outages = new List<Outage>();
        detector.OutageEnded += (_, o) => outages.Add(o);
        var scenario = new Scenario().RouterDown(Utc(12, 0, 0), Utc(12, 1, 0));
        foreach (ProbeRound round in scenario.Rounds(Utc(11, 59, 59), Utc(12, 0, 2)))
        {
            detector.Process(round);
        }

        detector.Stop(MonotonicMs(Utc(12, 0, 2), Utc(11, 59, 59)));

        Outage outage = Assert.Single(outages);
        Assert.Equal(OutageNotes.AppExit, outage.Note);
        Assert.Equal(OutageCause.Router, outage.Cause);
        Assert.Equal(2_000, outage.DurationMs);
    }

    // ---- Anlaufphase nach Start und Aufwachen (R2 in M3) ----

    [Fact]
    public void Lost_rounds_right_after_start_are_ignored_until_the_first_answer()
    {
        // Autostart nach der Anmeldung: das Netz ist noch nicht bereit.
        var detector = new OutageDetector();
        var scenario = new Scenario().AdapterDown(Utc(12, 0, 0), Utc(12, 0, 6));

        List<Outage> outages = scenario.Run(detector, Utc(12, 0, 0), Utc(12, 0, 10));

        Assert.Empty(outages);
        Assert.Equal(ConnectionState.Connected, detector.State);
    }

    [Fact]
    public void Without_any_answer_detection_starts_after_the_warmup_time()
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 1, 0));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(12, 0, 0), Utc(12, 1, 10)));

        Assert.Equal(Utc(12, 0, 0).AddMilliseconds(OutageDetector.DefaultWarmupMs), outage.StartUtc);
        Assert.Equal(60_000 - OutageDetector.DefaultWarmupMs, outage.DurationMs);
    }

    [Fact]
    public void Lost_rounds_right_after_resume_are_ignored_until_the_first_answer()
    {
        var detector = new OutageDetector();
        var outages = new List<Outage>();
        detector.OutageEnded += (_, o) => outages.Add(o);
        var scenario = new Scenario().AdapterDown(Utc(12, 0, 5), Utc(12, 0, 12));
        ProbeRound[] rounds = scenario.Rounds(Utc(12, 0, 0), Utc(12, 0, 20)).ToArray();

        foreach (ProbeRound round in rounds.Where(r => r.SentAtUtc < Utc(12, 0, 4)))
        {
            detector.Process(round);
        }

        detector.Suspend(rounds[20].SentAtMonotonicMs);

        // Aufwachen um 12:00:05; der Adapter verbindet sich erst um 12:00:12.
        foreach (ProbeRound round in rounds.Where(r => r.SentAtUtc >= Utc(12, 0, 5)))
        {
            detector.Process(round);
        }

        Assert.Empty(outages);
        Assert.Equal(ConnectionState.Connected, detector.State);
    }

    // ---- Zeit ----

    [Fact]
    public void Outage_lasting_hours_is_a_single_event()
    {
        var scenario = new Scenario(intervalMs: 1_000).InternetDown(Utc(8, 0, 0), Utc(11, 30, 0));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(7, 59, 0), Utc(11, 31, 0)));

        Assert.Equal(12_600_000L, outage.DurationMs);   // 3,5 h
    }

    [Fact]
    public void Outage_across_the_clock_change_lasts_two_seconds_and_end_equals_start_plus_duration()
    {
        // 25.10.2026: 02:59:59 MESZ (00:59:59 UTC) bis 02:00:01 MEZ (01:00:01 UTC).
        var scenario = new Scenario().InternetDown(Utc(0, 59, 59, day: 25), Utc(1, 0, 1, day: 25));

        Outage outage = Assert.Single(scenario.Run(new OutageDetector(), Utc(0, 59, 50, day: 25), Utc(1, 0, 10, day: 25)));

        Assert.Equal(2_000, outage.DurationMs);
        Assert.Equal(outage.StartUtc.AddMilliseconds(outage.DurationMs), outage.EndUtc);
        TimeZoneInfo berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        Assert.Equal("2026-10-25T02:59:59.000+02:00", Timestamps.FormatLocal(outage.StartUtc, berlin));
        Assert.Equal("2026-10-25T02:00:01.000+01:00", Timestamps.FormatLocal(outage.EndUtc, berlin));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public void Minimum_lost_rounds_is_configurable(int minLostRounds, int expectedOutages)
    {
        var scenario = new Scenario().InternetDown(Utc(12, 0, 0), Utc(12, 0, 0, 2 * Tick));

        List<Outage> outages = scenario.Run(new OutageDetector(minLostRounds), Utc(11, 59, 50), Utc(12, 0, 10));

        Assert.Equal(expectedOutages, outages.Count);
    }
}
