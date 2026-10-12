using System.Globalization;
using System.Text;

namespace NetWatch.Core.Reports;

/// <summary>
/// Gemeinsame HTML-Bausteine für Wochen- und Tagesbericht.
/// </summary>
public static class ReportHtml
{
    public static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static readonly string[] WeekdayShort = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    /// <summary>Dateiname des Tagesberichts, auf den der Wochenbericht verlinkt.</summary>
    public static string DailyFileName(DateOnly date) => $"report_day_{date:yyyy-MM-dd}.html";

    public const string WeeklyFileName = "report_week.html";

    /// <summary>Z. B. "Mo 12.10." .</summary>
    public static string DayLabel(DateOnly date) => $"{WeekdayShort[(int)date.DayOfWeek]} {date:dd.MM}.";

    /// <summary>
    /// Maskiert nur die HTML-Sonderzeichen; Umlaute bleiben lesbar, weil die Dateien als UTF-8 geschrieben werden
    /// (<see cref="System.Net.WebUtility.HtmlEncode(string)"/> würde sie in Zahlencodes umwandeln).
    /// </summary>
    public static string Encode(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    public static void AppendHead(StringBuilder html, string title, string extraCss)
    {
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"de\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>").Append(Encode(title)).AppendLine("</title>");
        html.AppendLine("<style>");
        html.AppendLine(BaseCss);
        html.AppendLine(extraCss);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
    }

    public static void AppendFoot(StringBuilder html)
    {
        html.AppendLine("<footer>Erstellt von NetWatch. Grundlage: netwatch_events.csv und netwatch_sessions.csv.</footer>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
    }

    public static void AppendFigure(StringBuilder html, string label, string value, string detail) =>
        html.Append("<div class=\"figure\"><div class=\"label\">").Append(Encode(label))
            .Append("</div><div class=\"value\">").Append(Encode(value))
            .Append("</div><div class=\"detail\">").Append(Encode(detail)).AppendLine("</div></div>");

    public static string Percent(double fraction, string format) => (fraction * 100).ToString(format, German) + " %";

    /// <summary>CSS-Klasse für die Farbstufe eines Aussetzers (wie im Wochenbericht).</summary>
    public static string SeverityClass(long durationMs) => durationMs < 1_000 ? "sev1" : durationMs <= 10_000 ? "sev2" : "sev3";

    private const string BaseCss = """
        body { font-family: "Segoe UI", system-ui, sans-serif; margin: 24px; color: #1f2328; background: #ffffff; }
        h1 { font-size: 22px; margin: 0 0 4px; }
        h2 { font-size: 17px; margin: 28px 0 6px; }
        a { color: #0969da; }
        .meta, .hint, .note, footer { color: #59636e; font-size: 13px; }
        .note { color: #8a5a00; }
        .summary { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 16px; }
        .figure { border: 1px solid #d1d9e0; border-radius: 6px; padding: 10px 14px; min-width: 170px; }
        .figure .label { font-size: 12px; color: #59636e; }
        .figure .value { font-size: 22px; font-weight: 600; margin: 2px 0; }
        .figure .detail { font-size: 12px; color: #59636e; }
        svg text { font-size: 11px; fill: #1f2328; }
        .legend { display: flex; flex-wrap: wrap; gap: 14px; font-size: 12px; margin-top: 8px; }
        .legend svg { vertical-align: -2px; }
        rect.measured { fill: #cdeccd; }
        rect.sev1 { fill: #f6b0b0; }
        rect.sev2 { fill: #e05a5a; }
        rect.sev3 { fill: #a30f0f; }
        rect.partial { fill: url(#hatch); }
        rect.notmeasured { fill: #d6d6d6; }
        rect.nonexistent { fill: #f0f0f0; }
        rect.future { fill: #ffffff; stroke: #e6e6e6; }
        table { border-collapse: collapse; font-size: 14px; }
        th, td { border-bottom: 1px solid #d1d9e0; padding: 6px 12px; text-align: left; vertical-align: top; }
        th { font-weight: 600; }
        td.num { text-align: right; font-variant-numeric: tabular-nums; }
        footer { margin-top: 32px; }
        """;
}
