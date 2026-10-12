using System.Text.RegularExpressions;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.Core.Tests.Reports;

public class DailyReportTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateOnly Friday = new(2026, 10, 9);
    private static readonly DateTimeOffset AfterWeek = Local(13, 0, 0);

    private static DateTimeOffset Local(int day, int hour, int minute, int second = 0, int millisecond = 0, int month = 10) =>
        new(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified), Berlin), TimeSpan.Zero);

    private static Outage Outage(DateTimeOffset startUtc, long durationMs, OutageCause cause = OutageCause.Router, string? note = null) =>
        new(startUtc, startUtc.AddMilliseconds(durationMs), durationMs, cause, ["192.168.178.1", "1.1.1.1", "8.8.8.8"], "192.168.178.1", note);

    private static Session Session(DateTimeOffset start, DateTimeOffset end, string endReason = SessionReasons.Exit, string startReason = SessionReasons.Start) =>
        new(start, startReason, end, endReason, []);

    /// <summary>Die erfundene Beispielwoche: Fr 09.10. mit Router-Neustart, Providerausfall und Messlücke 10:07:30–14:00.</summary>
    private static DailyReport ExampleFriday() => DailyReport.Create(
        [Outage(Local(9, 2, 0, 3, 300), 140_100), Outage(Local(9, 18, 44, 2), 6_300, OutageCause.Provider)],
        [Session(Local(6, 0, 0), Local(9, 10, 7, 30)), Session(Local(9, 14, 0), Local(11, 23, 30), SessionReasons.Suspend)],
        null,
        null,
        Friday,
        AfterWeek,
        Berlin);

    [Fact]
    public void Day_lists_outages_chronologically_with_gap_and_reason()
    {
        DailyReport report = ExampleFriday();

        Assert.Equal(2, report.Outages.Count);
        Assert.Equal(OutageCause.Router, report.Outages[0].Outage.Cause);
        MeasurementGap gap = Assert.Single(report.Gaps);
        Assert.Equal(Local(9, 10, 7, 30), gap.StartUtc);
        Assert.Equal(Local(9, 14, 0), gap.EndUtc);
        Assert.Equal("NetWatch beendet", gap.Reason);
    }

    [Fact]
    public void Summary_relates_availability_to_measured_time()
    {
        DailySummary summary = ExampleFriday().Summary;

        long measured = 24 * 3_600_000L - (long)(Local(9, 14, 0) - Local(9, 10, 7, 30)).TotalMilliseconds;
        Assert.Equal(measured, summary.MeasuredMs);
        Assert.Equal(24 * 3_600_000L, summary.ElapsedMs);
        Assert.Equal(146_400, summary.OutageMs);
        Assert.Equal(140_100, summary.LongestOutageMs);
        Assert.Equal(1 - 146_400.0 / measured, summary.Availability!.Value, precision: 9);
    }

    [Theory]
    [InlineData(SessionReasons.Suspend, "Standby")]
    [InlineData(SessionReasons.Exit, "NetWatch beendet")]
    [InlineData(SessionReasons.Shutdown, "Herunterfahren oder Abmelden")]
    [InlineData(SessionReasons.CrashDetected, "Absturz (Ende geschätzt)")]
    public void Gap_reason_comes_from_the_end_of_the_previous_session(string endReason, string expected)
    {
        Session[] sessions = [Session(Local(9, 0, 0), Local(9, 12, 0), endReason), Session(Local(9, 13, 0), AfterWeek, SessionReasons.Exit, SessionReasons.Resume)];

        DailyReport report = DailyReport.Create([], sessions, null, null, Friday, AfterWeek, Berlin);

        Assert.Equal(expected, Assert.Single(report.Gaps).Reason);
    }

    [Fact]
    public void Gap_before_the_first_start_is_named_as_such()
    {
        DailyReport report = DailyReport.Create([], [Session(Local(9, 8, 0), AfterWeek)], null, null, Friday, AfterWeek, Berlin);

        MeasurementGap gap = Assert.Single(report.Gaps);
        Assert.Equal(Local(9, 0, 0), gap.StartUtc);
        Assert.Equal("keine Messung (vor dem ersten Start)", gap.Reason);
    }

    [Fact]
    public void Outage_across_midnight_appears_on_both_days_with_full_duration_and_hint()
    {
        Outage outage = Outage(Local(7, 23, 59, 50), 30_000, OutageCause.Provider);
        Session[] sessions = [Session(Local(6, 0, 0), AfterWeek)];

        DailyReport wednesday = DailyReport.Create([outage], sessions, null, null, new DateOnly(2026, 10, 7), AfterWeek, Berlin);
        DailyReport thursday = DailyReport.Create([outage], sessions, null, null, new DateOnly(2026, 10, 8), AfterWeek, Berlin);

        Assert.True(Assert.Single(wednesday.Outages).EndsNextDay);
        Assert.True(Assert.Single(thursday.Outages).StartedPreviousDay);
        Assert.Equal(10_000, wednesday.Summary.OutageMs);
        Assert.Equal(20_000, thursday.Summary.OutageMs);
        Assert.Equal(30_000, thursday.Summary.LongestOutageMs);
    }

    [Fact]
    public void Running_session_and_outage_count_until_now_and_rest_is_not_reached()
    {
        DateOnly monday = new(2026, 10, 12);
        DateTimeOffset now = Local(12, 20, 0);
        var running = new OutageStart(Local(12, 19, 59, 50), OutageCause.Provider, "192.168.178.1", ["1.1.1.1", "8.8.8.8"]);

        DailyReport report = DailyReport.Create([], [], Local(12, 7, 0), running, monday, now, Berlin);

        Assert.Equal(20 * 3_600_000L, report.Summary.ElapsedMs);
        Assert.Equal(13 * 3_600_000L, report.Summary.MeasuredMs);
        Assert.Equal(ReportInputs.RunningNote, Assert.Single(report.Outages).Outage.Note);
        Assert.Equal(now, report.ElapsedEndUtc);
        Assert.Equal("keine Messung (vor dem ersten Start)", Assert.Single(report.Gaps).Reason);
    }

    [Fact]
    public void Day_of_the_autumn_clock_change_has_25_hours_and_five_timeline_rows()
    {
        DateOnly sunday = new(2026, 10, 25);
        DateTimeOffset now = Local(26, 12, 0);

        DailyReport report = DailyReport.Create([], [Session(Local(24, 0, 0), now)], null, null, sunday, now, Berlin);

        Assert.Equal(TimeSpan.FromHours(25), report.DayEndUtc - report.DayStartUtc);
        Assert.Equal(25 * 3_600_000L, report.Summary.MeasuredMs);
        string html = DailyReportHtml.Render(report);
        Assert.Equal(5, Regex.Matches(html, "class=\"rowlabel\"").Count);
        Assert.Contains("02 (+02:00)", html);
        Assert.Contains("02 (+01:00)", html);
        Assert.Contains("25 Stunden", html);
    }

    [Fact]
    public void Outages_in_the_repeated_hour_are_distinguishable_in_the_table()
    {
        DateOnly sunday = new(2026, 10, 25);
        DateTimeOffset now = Local(26, 12, 0);
        Outage[] outages =
        [
            Outage(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), 1_800),
            Outage(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), 9_400, OutageCause.Provider),
        ];

        string html = DailyReportHtml.Render(DailyReport.Create(outages, [Session(Local(24, 0, 0), now)], null, null, sunday, now, Berlin));

        Assert.Contains("02:30:00.000 (+02:00)", html);
        Assert.Contains("02:30:00.000 (+01:00)", html);
    }

    [Fact]
    public void Day_of_the_spring_clock_change_has_23_hours_and_four_rows()
    {
        DateOnly sunday = new(2026, 3, 29);
        DateTimeOffset now = Local(30, 12, 0, month: 3);

        DailyReport report = DailyReport.Create([], [Session(Local(28, 0, 0, month: 3), now)], null, null, sunday, now, Berlin);

        Assert.Equal(TimeSpan.FromHours(23), report.DayEndUtc - report.DayStartUtc);
        Assert.Equal(4, Regex.Matches(DailyReportHtml.Render(report), "class=\"rowlabel\"").Count);
    }

    [Fact]
    public void Day_without_measurement_has_no_availability_and_says_so()
    {
        DailyReport report = DailyReport.Create([], [], null, null, Friday, AfterWeek, Berlin);

        Assert.Equal(0, report.Summary.Coverage);
        Assert.Null(report.Summary.Availability);
        Assert.Contains("Keine Messung an diesem Tag.", DailyReportHtml.Render(report));
    }
}
