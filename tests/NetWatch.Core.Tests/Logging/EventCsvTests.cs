using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Tests.Logging;

public class EventCsvTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static Outage Outage(DateTimeOffset startUtc, long durationMs, OutageCause cause, string[] failed, string? gateway = "192.168.178.1", string? note = null) =>
        new(startUtc, startUtc.AddMilliseconds(durationMs), durationMs, cause, failed, gateway, note);

    [Fact]
    public void Header_matches_requirements()
    {
        Assert.Equal("start_local;end_local;start_utc;end_utc;duration_ms;cause;failed_targets;gateway;note", EventCsv.Header);
    }

    [Fact]
    public void Example_lines_from_requirements_are_reproduced_exactly()
    {
        Outage routerRestart = Outage(new DateTimeOffset(2026, 10, 13, 0, 0, 3, 412, TimeSpan.Zero), 1802, OutageCause.Router, ["192.168.178.1", "1.1.1.1", "8.8.8.8"]);
        Outage providerOutage = Outage(new DateTimeOffset(2026, 10, 13, 12, 38, 51, 90, TimeSpan.Zero), 245030, OutageCause.Provider, ["1.1.1.1", "8.8.8.8"]);

        Assert.Equal(
            "2026-10-13T02:00:03.412+02:00;2026-10-13T02:00:05.214+02:00;2026-10-13T00:00:03.412Z;2026-10-13T00:00:05.214Z;1802;router;192.168.178.1,1.1.1.1,8.8.8.8;192.168.178.1;",
            EventCsv.FormatLine(routerRestart, Berlin));
        Assert.Equal(
            "2026-10-13T14:38:51.090+02:00;2026-10-13T14:42:56.120+02:00;2026-10-13T12:38:51.090Z;2026-10-13T12:42:56.120Z;245030;provider;1.1.1.1,8.8.8.8;192.168.178.1;",
            EventCsv.FormatLine(providerOutage, Berlin));
    }

    [Fact]
    public void F04_outage_across_the_clock_change_is_written_with_both_offsets_and_two_seconds()
    {
        // 25.10.2026 02:59:59 MESZ (00:59:59 UTC) bis 02:00:01 MEZ (01:00:01 UTC).
        Outage outage = Outage(new DateTimeOffset(2026, 10, 25, 0, 59, 59, TimeSpan.Zero), 2_000, OutageCause.Provider, ["1.1.1.1", "8.8.8.8"]);

        string[] columns = EventCsv.FormatLine(outage, Berlin).Split(';');

        Assert.Equal("2026-10-25T02:59:59.000+02:00", columns[0]);
        Assert.Equal("2026-10-25T02:00:01.000+01:00", columns[1]);
        Assert.Equal("2026-10-25T00:59:59.000Z", columns[2]);
        Assert.Equal("2026-10-25T01:00:01.000Z", columns[3]);
        Assert.Equal("2000", columns[4]);
    }

    [Theory]
    [InlineData(OutageCause.PcLan, "pc_lan")]
    [InlineData(OutageCause.Router, "router")]
    [InlineData(OutageCause.Provider, "provider")]
    [InlineData(OutageCause.Unknown, "unknown")]
    public void Causes_are_written_with_their_csv_names(OutageCause cause, string expected)
    {
        Assert.Equal(expected, EventCsv.CauseName(cause));
    }

    [Theory]
    [InlineData(OutageNotes.Suspended)]
    [InlineData(OutageNotes.AppExit)]
    [InlineData(OutageNotes.EndUnknown)]
    public void Note_is_the_last_column(string note)
    {
        Outage outage = Outage(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero), 1_000, OutageCause.Provider, ["1.1.1.1"], note: note);

        Assert.EndsWith($";{note}", EventCsv.FormatLine(outage, Berlin));
    }

    [Fact]
    public void Missing_gateway_leaves_the_column_empty_and_keeps_nine_columns()
    {
        Outage outage = Outage(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero), 1_000, OutageCause.Unknown, ["1.1.1.1", "8.8.8.8"], gateway: null);

        string[] columns = EventCsv.FormatLine(outage, Berlin).Split(';');

        Assert.Equal(9, columns.Length);
        Assert.Equal("", columns[7]);
    }
}
