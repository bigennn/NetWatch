using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Reports;

/// <summary>Ein gemessener Zeitabschnitt.</summary>
public sealed record MeasuredSpan(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// Gemeinsame Aufbereitung der Protokolldaten für Wochen- und Tagesbericht.
/// </summary>
public static class ReportInputs
{
    /// <summary>Hinweis für einen Aussetzer oder eine Messphase, die beim Erstellen des Berichts noch läuft.</summary>
    public const string RunningNote = "läuft";

    /// <summary>
    /// Gemessene Abschnitte, nach Beginn sortiert und zusammengefasst; eine laufende Messphase zählt bis <paramref name="nowUtc"/>.
    /// </summary>
    public static List<MeasuredSpan> MeasuredSpans(IEnumerable<Session> sessions, DateTimeOffset? runningSessionStartUtc, DateTimeOffset nowUtc)
    {
        var spans = sessions
            .Select(s => new MeasuredSpan(s.StartUtc, s.EndUtc < nowUtc ? s.EndUtc : nowUtc))
            .ToList();
        if (runningSessionStartUtc is { } start)
        {
            spans.Add(new MeasuredSpan(start, nowUtc));
        }

        var merged = new List<MeasuredSpan>();
        foreach (MeasuredSpan span in spans.Where(s => s.EndUtc > s.StartUtc).OrderBy(s => s.StartUtc))
        {
            if (merged.Count > 0 && span.StartUtc <= merged[^1].EndUtc)
            {
                if (span.EndUtc > merged[^1].EndUtc)
                {
                    merged[^1] = merged[^1] with { EndUtc = span.EndUtc };
                }
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }

    /// <summary>Alle Aussetzer; ein laufender wird bis <paramref name="nowUtc"/> mit Hinweis <see cref="RunningNote"/> angehängt.</summary>
    public static List<Outage> OutagesWithRunning(IEnumerable<Outage> outages, OutageStart? runningOutage, DateTimeOffset nowUtc)
    {
        var all = outages.ToList();
        if (runningOutage is not null)
        {
            long runningMs = (long)Math.Max(0, (nowUtc - runningOutage.StartUtc).TotalMilliseconds);
            all.Add(new Outage(
                runningOutage.StartUtc,
                nowUtc,
                runningMs,
                runningOutage.Cause,
                runningOutage.FailedTargets,
                runningOutage.Gateway,
                RunningNote));
        }

        return all.OrderBy(o => o.StartUtc).ToList();
    }

    /// <summary>Beginn eines Kalendertags in Ortszeit als UTC-Zeitpunkt.</summary>
    public static DateTimeOffset LocalMidnightUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    /// <summary>Gemeinsame Länge zweier Zeitabschnitte in ms.</summary>
    public static long OverlapMs(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd)
    {
        DateTimeOffset start = aStart > bStart ? aStart : bStart;
        DateTimeOffset end = aEnd < bEnd ? aEnd : bEnd;
        return end > start ? (long)(end - start).TotalMilliseconds : 0;
    }
}
