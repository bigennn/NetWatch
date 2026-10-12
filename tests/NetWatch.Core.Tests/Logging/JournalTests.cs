using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Tests.Logging;

public class JournalTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateTimeOffset Noon = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSink _events = new();
    private readonly FakeSink _sessions = new();
    private readonly List<RunState?> _savedStates = [];
    private readonly FakeClock _clock = new(Noon);
    private readonly Journal _journal;

    public JournalTests()
    {
        _journal = new Journal(_events, _sessions, _savedStates.Add, _clock, Berlin);
    }

    private RunState? LastState => _savedStates[^1];

    private static Outage ProviderOutage(DateTimeOffset start, long durationMs, string? note = null) =>
        new(start, start.AddMilliseconds(durationMs), durationMs, OutageCause.Provider, ["1.1.1.1", "8.8.8.8"], "192.168.178.1", note);

    [Fact]
    public void Ended_outage_becomes_one_line_in_the_event_log()
    {
        _journal.OutageEnded(ProviderOutage(Noon, 1_500));

        string line = Assert.Single(_events.Lines);
        Assert.StartsWith("2026-10-13T14:00:00.000+02:00;2026-10-13T14:00:01.500+02:00;", line);
    }

    [Fact]
    public void Running_outage_is_kept_in_the_state_file_until_it_ends()
    {
        var start = new OutageStart(Noon, OutageCause.Router, "192.168.178.1", ["192.168.178.1", "1.1.1.1", "8.8.8.8"]);

        _journal.OutageStarted(start);
        Assert.Equal(start, LastState!.OpenOutage);

        _journal.OutageEnded(ProviderOutage(Noon, 2_000));
        Assert.Null(LastState!.OpenOutage);
    }

    [Fact]
    public void Session_lines_contain_start_end_reasons_and_notes()
    {
        _journal.SessionStarted(Noon, SessionReasons.Start);
        _journal.AddSessionNote(SessionNotes.GatewayIgnoresPing);
        _journal.AddSessionNote(SessionNotes.GatewayIgnoresPing);
        _journal.SessionEnded(Noon.AddHours(2), SessionReasons.Suspend);
        _journal.SessionStarted(Noon.AddHours(3), SessionReasons.Resume);
        _journal.SessionEnded(Noon.AddHours(4), SessionReasons.Exit);

        Assert.Equal(
            new[]
            {
                "2026-10-13T14:00:00.000+02:00;2026-10-13T16:00:00.000+02:00;2026-10-13T12:00:00.000Z;2026-10-13T14:00:00.000Z;start;suspend;gateway_ignores_ping",
                "2026-10-13T17:00:00.000+02:00;2026-10-13T18:00:00.000+02:00;2026-10-13T15:00:00.000Z;2026-10-13T16:00:00.000Z;resume;exit;",
            },
            _sessions.Lines);
    }

    [Fact]
    public void Locked_event_log_loses_nothing_and_tick_writes_the_lines_once_unlocked()
    {
        _events.Locked = true;
        _journal.OutageEnded(ProviderOutage(Noon, 1_000));
        _journal.OutageEnded(ProviderOutage(Noon.AddMinutes(1), 2_000));

        Assert.Empty(_events.Lines);
        Assert.Equal(2, LastState!.PendingEventLines.Count);

        _events.Locked = false;
        _clock.Advance(TimeSpan.FromSeconds(10));
        _journal.Tick();

        Assert.Equal(2, _events.Lines.Count);
        Assert.Contains(";1000;", _events.Lines[0]);
        Assert.Contains(";2000;", _events.Lines[1]);
        Assert.Empty(LastState!.PendingEventLines);
    }

    [Fact]
    public void Close_with_everything_written_deletes_the_state_file()
    {
        _journal.SessionStarted(Noon, SessionReasons.Start);
        _journal.SessionEnded(Noon.AddHours(1), SessionReasons.Exit);

        _journal.Close();

        Assert.Null(LastState);
    }

    [Fact]
    public void Close_with_locked_file_keeps_pending_lines_in_the_state_file_without_a_running_session()
    {
        _journal.SessionStarted(Noon, SessionReasons.Start);
        _events.Locked = true;
        _journal.OutageEnded(ProviderOutage(Noon, 1_000));
        _journal.SessionEnded(Noon.AddHours(1), SessionReasons.Exit);

        _journal.Close();

        Assert.NotNull(LastState);
        Assert.Null(LastState!.Session);
        Assert.Single(LastState.PendingEventLines);
    }

    [Fact]
    public void Pending_lines_from_the_last_run_are_written_on_recovery()
    {
        var previous = new RunState
        {
            LastHeartbeatUtc = Noon,
            PendingEventLines = ["old event line"],
            PendingSessionLines = ["old session line"],
        };

        _journal.Recover(previous);

        Assert.Equal(new[] { "old event line" }, _events.Lines);
        Assert.Equal(new[] { "old session line" }, _sessions.Lines);
    }

    [Fact]
    public void Crash_with_running_session_gives_crash_detected_ending_at_the_last_heartbeat()
    {
        var previous = new RunState
        {
            Session = new RunningSession(Noon, SessionReasons.Start, []),
            LastHeartbeatUtc = Noon.AddMinutes(42),
        };

        _journal.Recover(previous);

        string[] columns = Assert.Single(_sessions.Lines).Split(';');
        Assert.Equal("2026-10-13T12:42:00.000Z", columns[3]);
        Assert.Equal(SessionReasons.CrashDetected, columns[5]);
        Assert.Equal(SessionNotes.EndEstimated, columns[6]);
        Assert.Empty(_events.Lines);
    }

    [Fact]
    public void Crash_during_outage_closes_it_at_the_last_heartbeat_with_note_end_unknown()
    {
        var previous = new RunState
        {
            Session = new RunningSession(Noon, SessionReasons.Start, []),
            LastHeartbeatUtc = Noon.AddMinutes(10).AddSeconds(30),
            OpenOutage = new OutageStart(Noon.AddMinutes(10), OutageCause.Router, "192.168.178.1", ["192.168.178.1", "1.1.1.1", "8.8.8.8"]),
        };

        _journal.Recover(previous);

        string[] columns = Assert.Single(_events.Lines).Split(';');
        Assert.Equal("2026-10-13T12:10:00.000Z", columns[2]);
        Assert.Equal("2026-10-13T12:10:30.000Z", columns[3]);
        Assert.Equal("30000", columns[4]);
        Assert.Equal("router", columns[5]);
        Assert.Equal(OutageNotes.EndUnknown, columns[8]);
    }

    [Fact]
    public void No_state_file_means_nothing_to_recover()
    {
        _journal.Recover(null);

        Assert.Empty(_events.Lines);
        Assert.Empty(_sessions.Lines);
        Assert.Empty(_savedStates);
    }

    [Fact]
    public void Session_lines_can_be_read_back()
    {
        _journal.SessionStarted(Noon, SessionReasons.Resume);
        _journal.AddSessionNote(SessionNotes.GatewayIgnoresPing);
        _journal.SessionEnded(Noon.AddHours(2), SessionReasons.Suspend);

        Assert.True(SessionCsv.TryParseLine(_sessions.Lines.Single(), out Session? session));
        Assert.Equal(Noon, session.StartUtc);
        Assert.Equal(Noon.AddHours(2), session.EndUtc);
        Assert.Equal(SessionReasons.Resume, session.StartReason);
        Assert.Equal(SessionReasons.Suspend, session.EndReason);
        Assert.Equal(new[] { SessionNotes.GatewayIgnoresPing }, session.Notes);
    }

    [Theory]
    [InlineData(SessionCsv.Header)]
    [InlineData("﻿" + SessionCsv.Header)]
    [InlineData("")]
    [InlineData("a;b;c")]
    public void Session_header_and_broken_lines_are_not_read(string line)
    {
        Assert.False(SessionCsv.TryParseLine(line, out _));
    }

    [Fact]
    public void Tick_renews_the_heartbeat_once_per_minute()
    {
        _journal.SessionStarted(Noon, SessionReasons.Start);
        int savesAfterStart = _savedStates.Count;

        _clock.Advance(TimeSpan.FromSeconds(30));
        _journal.Tick();
        Assert.Equal(savesAfterStart, _savedStates.Count);

        _clock.Advance(TimeSpan.FromSeconds(30));
        _journal.Tick();
        Assert.Equal(savesAfterStart + 1, _savedStates.Count);
        Assert.Equal(Noon.AddMinutes(1), LastState!.LastHeartbeatUtc);
    }
}
