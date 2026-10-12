using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Reports;

/// <summary>Eine Zeitspanne ohne Messung mit ihrem Grund.</summary>
/// <param name="Reason">Deutscher Grund, z. B. "Standby" oder "Absturz (Ende geschätzt)".</param>
public sealed record MeasurementGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Reason)
{
    public long DurationMs => (long)(EndUtc - StartUtc).TotalMilliseconds;
}

/// <summary>Ein Aussetzer, der den Tag berührt.</summary>
/// <param name="Outage">Der vollständige Aussetzer (Tabelle zeigt die volle Dauer).</param>
/// <param name="StartedPreviousDay">Begann vor Mitternacht am Tagesanfang.</param>
/// <param name="EndsNextDay">Endet nach Mitternacht am Tagesende.</param>
public sealed record DayOutage(Outage Outage, bool StartedPreviousDay, bool EndsNextDay);

/// <summary>Kennzahlen eines Tages.</summary>
public sealed record DailySummary(long MeasuredMs, long ElapsedMs, int OutageCount, long OutageMs, long LongestOutageMs)
{
    public double Coverage => ElapsedMs == 0 ? 0 : (double)MeasuredMs / ElapsedMs;

    /// <summary>Anteil der gemessenen Zeit ohne Aussetzer; <c>null</c> ohne Messung.</summary>
    public double? Availability => MeasuredMs == 0 ? null : 1 - (double)Math.Min(OutageMs, MeasuredMs) / MeasuredMs;
}

/// <summary>
/// Tagesbericht (F-08): ein Kalendertag in Ortszeit mit gemessenen Abschnitten, Messlücken samt Grund,
/// Aussetzern und Kennzahlen. Reine Logik; das HTML erzeugt <see cref="DailyReportHtml"/>.
/// </summary>
public sealed class DailyReport
{
    private DailyReport(
        DateOnly date,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        IReadOnlyList<MeasuredSpan> measured,
        IReadOnlyList<MeasurementGap> gaps,
        IReadOnlyList<DayOutage> outages,
        DailySummary summary)
    {
        Date = date;
        DayStartUtc = dayStartUtc;
        DayEndUtc = dayEndUtc;
        NowUtc = nowUtc;
        TimeZone = timeZone;
        Measured = measured;
        Gaps = gaps;
        Outages = outages;
        Summary = summary;
    }

    public DateOnly Date { get; }

    /// <summary>Lokale Mitternacht am Tagesanfang als UTC-Zeitpunkt.</summary>
    public DateTimeOffset DayStartUtc { get; }

    /// <summary>Lokale Mitternacht am Tagesende; der Tag hat 23, 24 oder 25 Stunden.</summary>
    public DateTimeOffset DayEndUtc { get; }

    public DateTimeOffset NowUtc { get; }

    public TimeZoneInfo TimeZone { get; }

    /// <summary>Gemessene Abschnitte, auf den Tag und auf "jetzt" zugeschnitten.</summary>
    public IReadOnlyList<MeasuredSpan> Measured { get; }

    /// <summary>Lücken zwischen Tagesbeginn und min(Tagesende, jetzt).</summary>
    public IReadOnlyList<MeasurementGap> Gaps { get; }

    /// <summary>Aussetzer des Tages, chronologisch.</summary>
    public IReadOnlyList<DayOutage> Outages { get; }

    public DailySummary Summary { get; }

    /// <summary>Ende des bis "jetzt" verstrichenen Teils des Tages.</summary>
    public DateTimeOffset ElapsedEndUtc => NowUtc < DayEndUtc ? (NowUtc > DayStartUtc ? NowUtc : DayStartUtc) : DayEndUtc;

    public static DailyReport Create(
        IEnumerable<Outage> outages,
        IEnumerable<Session> sessions,
        DateTimeOffset? runningSessionStartUtc,
        OutageStart? runningOutage,
        DateOnly date,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone)
    {
        DateTimeOffset dayStart = ReportInputs.LocalMidnightUtc(date, timeZone);
        DateTimeOffset dayEnd = ReportInputs.LocalMidnightUtc(date.AddDays(1), timeZone);
        DateTimeOffset elapsedEnd = nowUtc < dayEnd ? (nowUtc > dayStart ? nowUtc : dayStart) : dayEnd;

        List<Session> sessionList = sessions.OrderBy(s => s.StartUtc).ToList();
        List<MeasuredSpan> measured = ReportInputs.MeasuredSpans(sessionList, runningSessionStartUtc, nowUtc)
            .Select(span => new MeasuredSpan(Max(span.StartUtc, dayStart), Min(span.EndUtc, elapsedEnd)))
            .Where(span => span.EndUtc > span.StartUtc)
            .ToList();

        var gaps = new List<MeasurementGap>();
        DateTimeOffset cursor = dayStart;
        foreach (MeasuredSpan span in measured)
        {
            if (span.StartUtc > cursor)
            {
                gaps.Add(new MeasurementGap(cursor, span.StartUtc, GapReason(sessionList, cursor)));
            }

            cursor = Max(cursor, span.EndUtc);
        }

        if (elapsedEnd > cursor)
        {
            gaps.Add(new MeasurementGap(cursor, elapsedEnd, GapReason(sessionList, cursor)));
        }

        List<DayOutage> dayOutages = ReportInputs.OutagesWithRunning(outages, runningOutage, nowUtc)
            .Where(o => o.StartUtc < dayEnd && o.EndUtc >= dayStart && o.StartUtc < elapsedEnd)
            .Select(o => new DayOutage(o, o.StartUtc < dayStart, o.EndUtc > dayEnd))
            .ToList();

        long outageMs = dayOutages.Sum(d => ReportInputs.OverlapMs(d.Outage.StartUtc, d.Outage.EndUtc, dayStart, elapsedEnd));
        var summary = new DailySummary(
            measured.Sum(span => (long)(span.EndUtc - span.StartUtc).TotalMilliseconds),
            (long)(elapsedEnd - dayStart).TotalMilliseconds,
            dayOutages.Count,
            outageMs,
            dayOutages.Count == 0 ? 0 : dayOutages.Max(d => d.Outage.DurationMs));

        return new DailyReport(date, dayStart, dayEnd, nowUtc, timeZone, measured, gaps, dayOutages, summary);
    }

    /// <summary>
    /// Grund einer Lücke: der Endgrund der letzten Messphase, die zu Beginn der Lücke (oder davor) endete.
    /// </summary>
    private static string GapReason(List<Session> sessions, DateTimeOffset gapStart)
    {
        Session? previous = sessions.LastOrDefault(s => s.EndUtc <= gapStart.AddSeconds(1));
        if (previous is null)
        {
            return "keine Messung (vor dem ersten Start)";
        }

        return previous.EndReason switch
        {
            SessionReasons.Suspend => "Standby",
            SessionReasons.Exit => "NetWatch beendet",
            SessionReasons.Shutdown => "Herunterfahren oder Abmelden",
            SessionReasons.CrashDetected => "Absturz (Ende geschätzt)",
            ReportInputs.RunningNote => "keine Daten nach dem letzten Lebenszeichen",
            _ => "unbekannt",
        };
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
