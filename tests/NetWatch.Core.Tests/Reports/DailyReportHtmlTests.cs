using System.Globalization;
using System.Text.RegularExpressions;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.Core.Tests.Reports;

public class DailyReportHtmlTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateOnly Friday = new(2026, 10, 9);
    private static readonly DateTimeOffset AfterWeek = Local(13, 0, 0);

    private static DateTimeOffset Local(int day, int hour, int minute, int second = 0, int millisecond = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, day, hour, minute, second, millisecond, DateTimeKind.Unspecified), Berlin), TimeSpan.Zero);

    private static Outage Outage(DateTimeOffset startUtc, long durationMs, OutageCause cause, string? note = null) =>
        new(startUtc, startUtc.AddMilliseconds(durationMs), durationMs, cause, ["1.1.1.1", "8.8.8.8"], "192.168.178.1", note);

    private static string RenderFriday() => DailyReportHtml.Render(DailyReport.Create(
        [
            Outage(Local(9, 2, 0, 3, 300), 140_100, OutageCause.Router, OutageNotes.Suspended),
            Outage(Local(9, 21, 5, 12, 200), 410, OutageCause.Provider),
        ],
        [
            new Session(Local(6, 0, 0), SessionReasons.Start, Local(9, 10, 7, 30), SessionReasons.Exit, []),
            new Session(Local(9, 14, 0), SessionReasons.Start, AfterWeek, SessionReasons.Exit, []),
        ],
        null,
        null,
        Friday,
        AfterWeek,
        Berlin));

    [Fact]
    public void Html_is_a_complete_german_document_with_link_to_the_weekly_report()
    {
        string html = RenderFriday();

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<title>NetWatch – Tagesbericht Fr 09.10.2026</title>", html);
        Assert.Contains("<a href=\"report_week.html\">← Wochenbericht</a>", html);
        Assert.DoesNotContain("<script", html);
        Assert.Equal(4, Regex.Matches(html, "class=\"rowlabel\"").Count);
    }

    [Fact]
    public void Outage_table_shows_times_with_milliseconds_duration_cause_targets_and_hint()
    {
        string html = RenderFriday();

        Assert.Contains(
            "<tr><td class=\"num\">02:00:03.300</td><td class=\"num\">02:02:23.400</td><td class=\"num\">2 min 20 s</td><td>Router</td><td>1.1.1.1, 8.8.8.8</td><td>endete durch Standby</td></tr>",
            html);
    }

    [Fact]
    public void Gap_table_shows_period_duration_and_reason()
    {
        string html = RenderFriday();

        Assert.Contains("<tr><td class=\"num\">10:07:30</td><td class=\"num\">14:00:00</td><td class=\"num\">3 h 52 min</td><td>NetWatch beendet</td></tr>", html);
    }

    [Fact]
    public void Short_outage_is_drawn_at_least_two_pixels_wide()
    {
        string html = RenderFriday();

        Match bar = Regex.Match(html, "<rect class=\"sev1\" x=\"[0-9.]+\" y=\"[0-9]+\" width=\"([0-9.]+)\"");
        Assert.True(bar.Success);
        Assert.True(double.Parse(bar.Groups[1].Value, CultureInfo.InvariantCulture) >= DailyReportHtml.MinOutageWidth);
    }

    [Fact]
    public void Timeline_bars_have_tooltips_with_details()
    {
        string html = RenderFriday();

        Assert.Contains("<title>21:05:12.200 · 0,4 s · Provider</title>", html);
        Assert.Contains("<title>nicht gemessen 10:07:30–14:00:00\nNetWatch beendet</title>", html);
    }

    [Fact]
    public void Weekly_report_links_every_day_to_its_daily_report()
    {
        DateTimeOffset now = Local(12, 23, 0);
        string html = WeeklyReportHtml.Render(WeeklyReport.Create([], [], null, null, now, Berlin));

        Assert.Contains("<a href=\"report_day_2026-10-06.html\">", html);
        Assert.Contains("<a href=\"report_day_2026-10-12.html\">", html);
    }
}
