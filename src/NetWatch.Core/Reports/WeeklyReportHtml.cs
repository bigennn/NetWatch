using System.Text;
using NetWatch.Core.Detection;
using static NetWatch.Core.Reports.ReportHtml;

namespace NetWatch.Core.Reports;

/// <summary>
/// Erzeugt den Wochenbericht als eigenständige HTML-Datei mit selbst gezeichnetem SVG
/// (kein JavaScript, keine Diagrammbibliothek; Details erscheinen als Hover über SVG-<c>title</c>).
/// Die Tagesbeschriftungen verlinken auf die Tagesberichte (<see cref="ReportHtml.DailyFileName"/>).
/// </summary>
public static class WeeklyReportHtml
{
    private const int LabelWidth = 78;
    private const int CellWidth = 9;
    private const int CellHeight = 22;
    private const int HeaderHeight = 22;
    private const int MaxOutagesInTooltip = 8;

    public static string Render(WeeklyReport report)
    {
        var html = new StringBuilder();
        DateOnly first = report.Days[0].Date;
        DateOnly last = report.Days[^1].Date;
        DateTimeOffset generatedLocal = TimeZoneInfo.ConvertTime(report.NowUtc, report.TimeZone);

        AppendHead(html, "NetWatch – Wochenbericht", Css);
        html.AppendLine("<h1>NetWatch – Wochenbericht</h1>");
        html.Append("<p class=\"meta\">Zeitraum: ").Append(Encode(DayLabel(first) + first.Year))
            .Append(" bis ").Append(Encode(DayLabel(last) + last.Year))
            .Append(" · Stand ").Append(Encode(generatedLocal.ToString("dd.MM.yyyy HH:mm", German)))
            .AppendLine(" · alle Zeiten in Ortszeit</p>");

        AppendSummary(html, report.Summary);

        html.AppendLine("<h2>Raster: Tag × Uhrzeit</h2>");
        html.AppendLine("<p class=\"hint\">Ein Feld = 15 Minuten. Fahre mit der Maus über ein Feld, um Uhrzeit, Dauer und Ursache der Aussetzer zu sehen. Ein Klick auf einen Tag öffnet den Tagesbericht.</p>");
        AppendGrid(html, report);
        AppendLegend(html);

        foreach (DateOnly day in report.Days.Where(day => day.Slots.Any(slot => slot.RepeatedHour)).Select(day => day.Date))
        {
            html.Append("<p class=\"note\">Zeitumstellung am ").Append(Encode(DayLabel(day)))
                .AppendLine(": Die Stunde 02:00–03:00 gab es zweimal; beide liegen in denselben Feldern (im Hover markiert).</p>");
        }

        AppendRecurring(html, report);
        AppendFoot(html);
        return html.ToString();
    }

    private static void AppendSummary(StringBuilder html, ReportSummary summary)
    {
        string causes = summary.CountByCause.Count == 0
            ? "–"
            : string.Join(" · ", summary.CountByCause
                .OrderByDescending(pair => pair.Value)
                .Select(pair => $"{TrayStatusText.CauseText(pair.Key)} {pair.Value}"));

        html.AppendLine("<div class=\"summary\">");
        AppendFigure(html, "Messabdeckung", Percent(summary.Coverage, "0.0"),
            $"{TrayStatusText.FormatDuration(summary.MeasuredMs)} von {TrayStatusText.FormatDuration(summary.ElapsedMs)}");
        AppendFigure(html, "Aussetzer", summary.OutageCount.ToString(German), causes);
        AppendFigure(html, "Gesamtdauer", summary.OutageCount == 0 ? "–" : TrayStatusText.FormatDuration(summary.OutageMs), "aller Aussetzer im Zeitraum");
        AppendFigure(html, "Verfügbarkeit", summary.Availability is { } value ? Percent(value, "0.000") : "–", "bezogen auf die gemessene Zeit");
        html.AppendLine("</div>");
    }

    private static void AppendGrid(StringBuilder html, WeeklyReport report)
    {
        int width = LabelWidth + WeeklyReport.SlotsPerDay * CellWidth;
        int height = HeaderHeight + report.Days.Count * CellHeight;
        html.Append("<svg class=\"grid\" role=\"img\" aria-label=\"Wochenraster\" viewBox=\"0 0 ")
            .Append(width).Append(' ').Append(height).Append("\" width=\"").Append(width).Append("\" height=\"").Append(height).AppendLine("\">");
        html.AppendLine("<defs><pattern id=\"hatch\" width=\"4\" height=\"4\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\">"
            + "<rect width=\"4\" height=\"4\" fill=\"#d6d6d6\"/><rect width=\"2\" height=\"4\" fill=\"#b7dfb7\"/></pattern></defs>");

        for (int hour = 0; hour < 24; hour++)
        {
            int x = LabelWidth + hour * 4 * CellWidth;
            html.Append("<text class=\"hour\" x=\"").Append(x).Append("\" y=\"14\">").Append(hour.ToString("00")).AppendLine("</text>");
            html.Append("<line class=\"tick\" x1=\"").Append(x).Append("\" y1=\"17\" x2=\"").Append(x).Append("\" y2=\"").Append(height).AppendLine("\"/>");
        }

        for (int d = 0; d < report.Days.Count; d++)
        {
            ReportDay day = report.Days[d];
            int y = HeaderHeight + d * CellHeight;
            html.Append("<a href=\"").Append(DailyFileName(day.Date)).Append("\"><text class=\"day\" x=\"0\" y=\"").Append(y + 15).Append("\">")
                .Append(Encode(DayLabel(day.Date))).AppendLine("</text></a>");
            foreach (ReportSlot slot in day.Slots)
            {
                int x = LabelWidth + slot.Index * CellWidth;
                html.Append("<rect class=\"").Append(CssClass(slot)).Append("\" x=\"").Append(x + 1).Append("\" y=\"").Append(y + 1)
                    .Append("\" width=\"").Append(CellWidth - 1).Append("\" height=\"").Append(CellHeight - 2).Append("\">")
                    .Append("<title>").Append(Encode(Tooltip(day.Date, slot, report.TimeZone))).AppendLine("</title></rect>");
            }
        }

        html.AppendLine("</svg>");
    }

    private static void AppendLegend(StringBuilder html)
    {
        (string css, string text)[] entries =
        [
            ("measured", "gemessen, kein Aussetzer"),
            ("sev1", "Aussetzer unter 1 s"),
            ("sev2", "Aussetzer 1–10 s"),
            ("sev3", "Aussetzer über 10 s"),
            ("partial", "teilweise gemessen"),
            ("notmeasured", "nicht gemessen"),
            ("future", "noch nicht erreicht"),
        ];

        html.AppendLine("<div class=\"legend\">");
        foreach ((string css, string text) in entries)
        {
            html.Append("<span><svg width=\"12\" height=\"12\"><rect class=\"").Append(css)
                .Append("\" width=\"12\" height=\"12\"/></svg> ").Append(Encode(text)).AppendLine("</span>");
        }

        html.AppendLine("</div>");
    }

    private static void AppendRecurring(StringBuilder html, WeeklyReport report)
    {
        html.AppendLine("<h2>Wiederkehrende Zeitfenster</h2>");
        if (report.Recurring.Count == 0)
        {
            html.AppendLine("<p>Kein 15-Minuten-Feld hatte an mehr als einem Tag einen Aussetzer.</p>");
            return;
        }

        html.AppendLine("<p class=\"hint\">Felder mit Aussetzern an mindestens zwei Tagen. Für die Rennplanung sind das die kritischen Uhrzeiten.</p>");
        html.AppendLine("<table><thead><tr><th>Uhrzeit</th><th>Tage mit Aussetzer</th><th>Ursache</th><th>längster Aussetzer</th></tr></thead><tbody>");
        foreach (RecurringWindow window in report.Recurring)
        {
            long longest = report.Days
                .SelectMany(day => day.Slots[window.SlotIndex].Outages)
                .Max(outage => outage.DurationMs);
            html.Append("<tr><td>").Append(WeeklyReport.SlotStartText(window.SlotIndex)).Append('–').Append(WeeklyReport.SlotEndText(window.SlotIndex))
                .Append("</td><td>").Append(window.DaysWithOutage).Append(" von ").Append(window.DaysMeasured).Append(" gemessenen Tagen")
                .Append("</td><td>").Append(Encode(string.Join(", ", window.Causes.Select(TrayStatusText.CauseText))))
                .Append("</td><td>").Append(Encode(TrayStatusText.FormatDuration(longest)))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static string Tooltip(DateOnly date, ReportSlot slot, TimeZoneInfo timeZone)
    {
        var text = new StringBuilder();
        text.Append(DayLabel(date)).Append(' ').Append(WeeklyReport.SlotStartText(slot.Index)).Append('–').Append(WeeklyReport.SlotEndText(slot.Index));
        if (slot.RepeatedHour)
        {
            text.Append(" (doppelte Stunde, Zeitumstellung)");
        }

        text.Append('\n').Append(slot.State switch
        {
            SlotState.Future => "noch nicht erreicht",
            SlotState.Nonexistent => "gibt es nicht (Zeitumstellung)",
            SlotState.NotMeasured => "nicht gemessen",
            _ => "gemessen: " + Percent(slot.Coverage, "0"),
        });

        foreach (Outage outage in slot.Outages.Take(MaxOutagesInTooltip))
        {
            DateTimeOffset localStart = TimeZoneInfo.ConvertTime(outage.StartUtc, timeZone);
            string start = DateOnly.FromDateTime(localStart.DateTime) == date
                ? localStart.ToString("HH:mm:ss", German)
                : localStart.ToString("dd.MM. HH:mm:ss", German);
            text.Append('\n').Append(start).Append(" · ").Append(TrayStatusText.FormatDuration(outage.DurationMs))
                .Append(" · ").Append(TrayStatusText.CauseText(outage.Cause));
            if (outage.Note == ReportInputs.RunningNote)
            {
                text.Append(" (läuft)");
            }
        }

        if (slot.Outages.Count > MaxOutagesInTooltip)
        {
            text.Append("\n… und ").Append(slot.Outages.Count - MaxOutagesInTooltip).Append(" weitere");
        }

        return text.ToString();
    }

    private static string CssClass(ReportSlot slot) => slot.Severity switch
    {
        3 => "sev3",
        2 => "sev2",
        1 => "sev1",
        _ => slot.State switch
        {
            SlotState.Future => "future",
            SlotState.Nonexistent => "nonexistent",
            SlotState.NotMeasured => "notmeasured",
            SlotState.Partial => "partial",
            _ => "measured",
        },
    };

    private const string Css = """
        .grid { display: block; max-width: 100%; height: auto; }
        .grid .hour { fill: #59636e; }
        .grid .tick { stroke: #ffffff; stroke-width: 1; }
        .grid a text { fill: #0969da; text-decoration: underline; }
        .grid rect:hover { stroke: #1f2328; stroke-width: 1.5; }
        """;
}
