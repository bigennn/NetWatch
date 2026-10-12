using System.Text;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using static NetWatch.Core.Reports.ReportHtml;

namespace NetWatch.Core.Reports;

/// <summary>
/// Erzeugt den Tagesbericht als eigenständige HTML-Datei: Kennzahlen, Zeitleiste (selbst gezeichnetes SVG,
/// Zeilen zu 6 Stunden in echter Zeit) und Tabellen der Aussetzer und Messlücken.
/// </summary>
public static class DailyReportHtml
{
    private const int RowMinutes = 360;
    private const double PixelsPerMinute = 3;
    private const int LabelWidth = 48;
    private const int RowHeight = 42;
    private const int BarTop = 4;
    private const int BarHeight = 18;

    /// <summary>Mindestbreite eines Aussetzers in Pixeln, damit auch 0,4 s sichtbar sind.</summary>
    public const double MinOutageWidth = 2;

    public static string Render(DailyReport report)
    {
        var html = new StringBuilder();
        string title = $"NetWatch – Tagesbericht {DayLabel(report.Date)}{report.Date.Year}";
        DateTimeOffset generatedLocal = TimeZoneInfo.ConvertTime(report.NowUtc, report.TimeZone);

        AppendHead(html, title, Css);
        html.Append("<p class=\"meta\"><a href=\"").Append(WeeklyFileName).AppendLine("\">← Wochenbericht</a></p>");
        html.Append("<h1>").Append(Encode(title)).AppendLine("</h1>");
        html.Append("<p class=\"meta\">Stand ").Append(Encode(generatedLocal.ToString("dd.MM.yyyy HH:mm", German)))
            .AppendLine(" · alle Zeiten in Ortszeit</p>");

        AppendSummary(html, report);

        html.AppendLine("<h2>Zeitleiste</h2>");
        html.AppendLine("<p class=\"hint\">Jede Zeile umfasst 6 Stunden. Fahre mit der Maus über einen roten Balken oder eine graue Lücke, um Details zu sehen.</p>");
        double hours = (report.DayEndUtc - report.DayStartUtc).TotalHours;
        if (Math.Abs(hours - 24) > 0.01)
        {
            html.Append("<p class=\"note\">Dieser Tag hatte wegen der Zeitumstellung ").Append(Math.Round(hours))
                .AppendLine(" Stunden. Die Zeitleiste folgt der echten Zeit; die Stundenmarken zeigen die Ortszeit.</p>");
        }

        AppendTimeline(html, report);
        AppendLegend(html);
        AppendOutageTable(html, report);
        AppendGapTable(html, report);
        AppendFoot(html);
        return html.ToString();
    }

    private static void AppendSummary(StringBuilder html, DailyReport report)
    {
        DailySummary summary = report.Summary;
        string causes = summary.OutageCount == 0
            ? "–"
            : string.Join(" · ", report.Outages
                .GroupBy(o => o.Outage.Cause)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{TrayStatusText.CauseText(group.Key)} {group.Count()}"));

        html.AppendLine("<div class=\"summary\">");
        AppendFigure(html, "Messabdeckung", Percent(summary.Coverage, "0.0"),
            $"{TrayStatusText.FormatDuration(summary.MeasuredMs)} von {TrayStatusText.FormatDuration(summary.ElapsedMs)}");
        AppendFigure(html, "Aussetzer", summary.OutageCount.ToString(German), causes);
        AppendFigure(html, "Gesamtdauer", summary.OutageCount == 0 ? "–" : TrayStatusText.FormatDuration(summary.OutageMs), "an diesem Tag");
        AppendFigure(html, "Verfügbarkeit", summary.Availability is { } value ? Percent(value, "0.000") : "–", "bezogen auf die gemessene Zeit");
        AppendFigure(html, "Längster Aussetzer", summary.OutageCount == 0 ? "–" : TrayStatusText.FormatDuration(summary.LongestOutageMs), "volle Dauer");
        html.AppendLine("</div>");
    }

    private static void AppendTimeline(StringBuilder html, DailyReport report)
    {
        int rows = (int)Math.Ceiling((report.DayEndUtc - report.DayStartUtc).TotalMinutes / RowMinutes);
        int width = LabelWidth + (int)(RowMinutes * PixelsPerMinute) + 20;
        int height = rows * RowHeight;
        html.Append("<svg class=\"timeline\" role=\"img\" aria-label=\"Zeitleiste\" viewBox=\"0 0 ").Append(width).Append(' ').Append(height)
            .Append("\" width=\"").Append(width).Append("\" height=\"").Append(height).AppendLine("\">");

        for (int row = 0; row < rows; row++)
        {
            DateTimeOffset rowStart = report.DayStartUtc.AddMinutes(row * RowMinutes);
            DateTimeOffset rowEnd = Min(rowStart.AddMinutes(RowMinutes), report.DayEndUtc);
            int y = row * RowHeight;

            html.Append("<text class=\"rowlabel\" x=\"0\" y=\"").Append(y + BarTop + 13).Append("\">")
                .Append(Encode(Local(rowStart, report.TimeZone).ToString("HH:mm", German))).AppendLine("</text>");

            // Noch nicht erreichter Teil.
            if (report.ElapsedEndUtc < rowEnd)
            {
                AppendBar(html, "future", Max(rowStart, report.ElapsedEndUtc), rowEnd, rowStart, y, null);
            }

            foreach (MeasurementGap gap in report.Gaps)
            {
                if (Overlaps(gap.StartUtc, gap.EndUtc, rowStart, rowEnd))
                {
                    string tooltip = $"nicht gemessen {TimeText(report, gap.StartUtc, false)}–{TimeText(report, gap.EndUtc, false)}\n{gap.Reason}";
                    AppendBar(html, "notmeasured", Max(gap.StartUtc, rowStart), Min(gap.EndUtc, rowEnd), rowStart, y, tooltip);
                }
            }

            foreach (MeasuredSpan span in report.Measured)
            {
                if (Overlaps(span.StartUtc, span.EndUtc, rowStart, rowEnd))
                {
                    string tooltip = $"gemessen {TimeText(report, span.StartUtc, false)}–{TimeText(report, span.EndUtc, false)}";
                    AppendBar(html, "measured", Max(span.StartUtc, rowStart), Min(span.EndUtc, rowEnd), rowStart, y, tooltip);
                }
            }

            foreach (DayOutage dayOutage in report.Outages)
            {
                Outage outage = dayOutage.Outage;
                if (outage.StartUtc < rowEnd && outage.EndUtc >= rowStart)
                {
                    AppendBar(html, SeverityClass(outage.DurationMs), Max(outage.StartUtc, rowStart), Min(outage.EndUtc, rowEnd), rowStart, y,
                        OutageTooltip(report, dayOutage), MinOutageWidth);
                }
            }

            // Stundenmarken in Ortszeit; in der doppelten Stunde mit Versatz, damit beide 02:00 unterscheidbar sind.
            for (DateTimeOffset tick = rowStart; tick <= rowEnd; tick = tick.AddHours(1))
            {
                double x = X(tick, rowStart);
                DateTimeOffset local = Local(tick, report.TimeZone);
                string label = local.ToString("HH", German);
                if (report.TimeZone.IsAmbiguousTime(local.DateTime))
                {
                    label += $" ({local:zzz})";
                }

                html.Append("<line class=\"tick\" x1=\"").Append(Num(x)).Append("\" y1=\"").Append(y + BarTop)
                    .Append("\" x2=\"").Append(Num(x)).Append("\" y2=\"").Append(y + BarTop + BarHeight + 3).AppendLine("\"/>");
                html.Append("<text class=\"hour\" x=\"").Append(Num(x)).Append("\" y=\"").Append(y + BarTop + BarHeight + 14).Append("\">")
                    .Append(Encode(label)).AppendLine("</text>");
            }
        }

        html.AppendLine("</svg>");
    }

    private static void AppendBar(StringBuilder html, string css, DateTimeOffset from, DateTimeOffset to, DateTimeOffset rowStart, int y, string? tooltip, double minWidth = 0)
    {
        double x = X(from, rowStart);
        double width = Math.Max(minWidth, X(to, rowStart) - x);
        if (width <= 0)
        {
            return;
        }

        html.Append("<rect class=\"").Append(css).Append("\" x=\"").Append(Num(x)).Append("\" y=\"").Append(y + BarTop)
            .Append("\" width=\"").Append(Num(width)).Append("\" height=\"").Append(BarHeight).Append('"');
        if (tooltip is null)
        {
            html.AppendLine("/>");
        }
        else
        {
            html.Append("><title>").Append(Encode(tooltip)).AppendLine("</title></rect>");
        }
    }

    private static void AppendLegend(StringBuilder html)
    {
        (string css, string text)[] entries =
        [
            ("measured", "gemessen"),
            ("sev1", "Aussetzer unter 1 s"),
            ("sev2", "Aussetzer 1–10 s"),
            ("sev3", "Aussetzer über 10 s"),
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

    private static void AppendOutageTable(StringBuilder html, DailyReport report)
    {
        html.AppendLine("<h2>Aussetzer</h2>");
        if (report.Outages.Count == 0)
        {
            html.AppendLine(report.Summary.MeasuredMs == 0
                ? "<p>Keine Messung an diesem Tag.</p>"
                : "<p>Keine Aussetzer an diesem Tag.</p>");
            return;
        }

        html.AppendLine("<table><thead><tr><th>Beginn</th><th>Ende</th><th>Dauer</th><th>Ursache</th><th>Betroffene Ziele</th><th>Hinweis</th></tr></thead><tbody>");
        foreach (DayOutage dayOutage in report.Outages)
        {
            Outage outage = dayOutage.Outage;
            html.Append("<tr><td class=\"num\">").Append(Encode(TimeText(report, outage.StartUtc, true)))
                .Append("</td><td class=\"num\">").Append(Encode(TimeText(report, outage.EndUtc, true)))
                .Append("</td><td class=\"num\">").Append(Encode(TrayStatusText.FormatDuration(outage.DurationMs)))
                .Append("</td><td>").Append(Encode(TrayStatusText.CauseText(outage.Cause)))
                .Append("</td><td>").Append(Encode(string.Join(", ", outage.FailedTargets)))
                .Append("</td><td>").Append(Encode(string.Join(", ", Notes(dayOutage))))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void AppendGapTable(StringBuilder html, DailyReport report)
    {
        html.AppendLine("<h2>Messlücken</h2>");
        if (report.Gaps.Count == 0)
        {
            html.AppendLine("<p>Keine Messlücken.</p>");
            return;
        }

        html.AppendLine("<table><thead><tr><th>Von</th><th>Bis</th><th>Dauer</th><th>Grund</th></tr></thead><tbody>");
        foreach (MeasurementGap gap in report.Gaps)
        {
            html.Append("<tr><td class=\"num\">").Append(Encode(TimeText(report, gap.StartUtc, false)))
                .Append("</td><td class=\"num\">").Append(Encode(TimeText(report, gap.EndUtc, false)))
                .Append("</td><td class=\"num\">").Append(Encode(TrayStatusText.FormatDuration(gap.DurationMs)))
                .Append("</td><td>").Append(Encode(gap.Reason))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static string OutageTooltip(DailyReport report, DayOutage dayOutage)
    {
        Outage outage = dayOutage.Outage;
        string text = $"{TimeText(report, outage.StartUtc, true)} · {TrayStatusText.FormatDuration(outage.DurationMs)} · {TrayStatusText.CauseText(outage.Cause)}";
        List<string> notes = Notes(dayOutage);
        return notes.Count == 0 ? text : $"{text}\n{string.Join(", ", notes)}";
    }

    private static List<string> Notes(DayOutage dayOutage)
    {
        var notes = new List<string>();
        switch (dayOutage.Outage.Note)
        {
            case ReportInputs.RunningNote:
                notes.Add("läuft");
                break;
            case OutageNotes.Suspended:
                notes.Add("endete durch Standby");
                break;
            case OutageNotes.AppExit:
                notes.Add("endete durch Beenden");
                break;
            case OutageNotes.EndUnknown:
                notes.Add("Ende geschätzt (Absturz)");
                break;
        }

        if (dayOutage.StartedPreviousDay)
        {
            notes.Add("begann am Vortag");
        }

        if (dayOutage.EndsNextDay)
        {
            notes.Add("endet am Folgetag");
        }

        return notes;
    }

    /// <summary>
    /// Uhrzeit in Ortszeit; mit Datum, wenn sie nicht zum Berichtstag gehört; das Tagesende heißt 24:00.
    /// In der doppelten Stunde der Zeitumstellung mit Versatz, z. B. "02:30:00 (+01:00)".
    /// </summary>
    private static string TimeText(DailyReport report, DateTimeOffset utc, bool withMilliseconds)
    {
        string format = withMilliseconds ? "HH:mm:ss.fff" : "HH:mm:ss";
        if (utc == report.DayEndUtc)
        {
            return withMilliseconds ? "24:00:00.000" : "24:00:00";
        }

        DateTimeOffset local = Local(utc, report.TimeZone);
        string text = DateOnly.FromDateTime(local.DateTime) == report.Date
            ? local.ToString(format, German)
            : local.ToString("dd.MM. " + format, German);
        return report.TimeZone.IsAmbiguousTime(local.DateTime) ? $"{text} ({local:zzz})" : text;
    }

    private static DateTimeOffset Local(DateTimeOffset utc, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTime(utc, timeZone);

    private static double X(DateTimeOffset t, DateTimeOffset rowStart) => LabelWidth + (t - rowStart).TotalMinutes * PixelsPerMinute;

    private static string Num(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) => aStart < bEnd && aEnd > bStart;

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private const string Css = """
        .timeline { display: block; max-width: 100%; height: auto; }
        .timeline .hour { fill: #59636e; font-size: 10px; }
        .timeline .tick { stroke: #8c959f; stroke-width: 1; }
        .timeline rect.sev1, .timeline rect.sev2, .timeline rect.sev3 { stroke: none; }
        .timeline rect:hover { stroke: #1f2328; stroke-width: 1.5; }
        """;
}
