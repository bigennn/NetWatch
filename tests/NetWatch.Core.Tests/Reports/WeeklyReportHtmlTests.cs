using System.Text.RegularExpressions;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.Core.Tests.Reports;

public class WeeklyReportHtmlTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static DateTimeOffset Local(int day, int hour, int minute, int second = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, day, hour, minute, second, DateTimeKind.Unspecified), Berlin), TimeSpan.Zero);

    private static string RenderDailyRestartWeek()
    {
        DateTimeOffset now = Local(12, 23, 59, 59);
        Outage[] outages = Enumerable.Range(6, 7)
            .Select(day => new Outage(Local(day, 2, 0, 30), Local(day, 2, 0, 31).AddMilliseconds(800), 1_800, OutageCause.Router, ["192.168.178.1"], "192.168.178.1", null))
            .ToArray();
        var session = new Session(Local(6, 0, 0), SessionReasons.Start, now, SessionReasons.Exit, []);
        return WeeklyReportHtml.Render(WeeklyReport.Create(outages, [session], null, null, now, Berlin));
    }

    [Fact]
    public void Html_is_a_complete_german_document()
    {
        string html = RenderDailyRestartWeek();

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"de\">", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("NetWatch – Wochenbericht", html);
        Assert.EndsWith("</html>" + Environment.NewLine, html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Grid_contains_one_rectangle_with_tooltip_per_slot()
    {
        string html = RenderDailyRestartWeek();

        int gridCells = Regex.Matches(html, "<rect class=\"[a-z0-9]+\" x=").Count;
        Assert.Equal(7 * 96, gridCells);
        Assert.Equal(7 * 96, Regex.Matches(html, "<title>").Count - 1); // minus Seitentitel
    }

    [Fact]
    public void Tooltip_lists_time_duration_and_cause()
    {
        string html = RenderDailyRestartWeek();

        Assert.Contains("Mo 12.10. 02:00–02:15\ngemessen: 100 %\n02:00:30 · 1,8 s · Router", html);
    }

    [Fact]
    public void Recurring_table_shows_the_daily_window()
    {
        string html = RenderDailyRestartWeek();

        Assert.Contains("<td>02:00–02:15</td><td>7 von 7 gemessenen Tagen</td><td>Router</td><td>1,8 s</td>", html);
    }

    [Fact]
    public void Summary_shows_coverage_count_and_availability()
    {
        string html = RenderDailyRestartWeek();

        Assert.Contains("Messabdeckung", html);
        Assert.Contains(">100,0 %<", html);
        Assert.Contains(">7<", html);
        Assert.Contains("Router 7", html);
        Assert.Contains("Verfügbarkeit", html);
    }

    [Fact]
    public void Html_special_characters_are_escaped_but_umlauts_stay_readable()
    {
        string html = RenderDailyRestartWeek();

        Assert.Contains("Verfügbarkeit", html);
        Assert.DoesNotContain("&#252;", html);
    }

    [Fact]
    public void Empty_week_says_that_nothing_recurred()
    {
        DateTimeOffset now = Local(12, 23, 0);
        string html = WeeklyReportHtml.Render(WeeklyReport.Create([], [], null, null, now, Berlin));

        Assert.Contains("Kein 15-Minuten-Feld hatte an mehr als einem Tag einen Aussetzer.", html);
        Assert.Contains("class=\"notmeasured\"", html);
    }
}
