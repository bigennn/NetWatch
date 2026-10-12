using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Reports;

/// <summary>Zustand eines Feldes im Wochenraster.</summary>
public enum SlotState
{
    /// <summary>Liegt nach "jetzt".</summary>
    Future,

    /// <summary>Gibt es wegen der Zeitumstellung nicht (Frühjahr, 02:00–03:00).</summary>
    Nonexistent,

    /// <summary>Keine Messung.</summary>
    NotMeasured,

    /// <summary>Nur ein Teil des Feldes wurde gemessen.</summary>
    Partial,

    /// <summary>Vollständig gemessen.</summary>
    Measured,
}

/// <summary>Ein 15-Minuten-Feld im Wochenraster.</summary>
/// <param name="Index">Spalte 0–95 (00:00–00:15 … 23:45–24:00 Ortszeit).</param>
/// <param name="ElapsedMs">Bis "jetzt" verstrichene Zeit des Feldes; 30 min in der doppelten Stunde der Zeitumstellung.</param>
/// <param name="MeasuredMs">Davon gemessen.</param>
/// <param name="Outages">Aussetzer, die das Feld berühren.</param>
/// <param name="Severity">0 = kein Aussetzer; 1 = unter 1 s; 2 = 1–10 s; 3 = über 10 s (längster Aussetzer).</param>
/// <param name="RepeatedHour">Das Feld liegt in der Stunde, die bei der Zeitumstellung im Herbst doppelt vorkommt.</param>
public sealed record ReportSlot(
    int Index,
    long ElapsedMs,
    long MeasuredMs,
    IReadOnlyList<Outage> Outages,
    SlotState State,
    int Severity,
    bool RepeatedHour)
{
    public double Coverage => ElapsedMs == 0 ? 0 : (double)MeasuredMs / ElapsedMs;
}

/// <summary>Eine Zeile des Rasters (ein Kalendertag in Ortszeit).</summary>
public sealed record ReportDay(DateOnly Date, IReadOnlyList<ReportSlot> Slots);

/// <summary>Ein Feld mit Aussetzern an mehreren Tagen.</summary>
/// <param name="DaysMeasured">An wie vielen Tagen das Feld überhaupt gemessen wurde.</param>
/// <param name="Causes">Vorkommende Ursachen, häufigste zuerst.</param>
public sealed record RecurringWindow(int SlotIndex, int DaysWithOutage, int DaysMeasured, IReadOnlyList<OutageCause> Causes);

/// <summary>Kennzahlen über alle sieben Tage.</summary>
/// <param name="MeasuredMs">Gemessene Zeit.</param>
/// <param name="ElapsedMs">Verstrichene Zeit im Berichtszeitraum (bis "jetzt").</param>
/// <param name="OutageMs">Summe der Aussetzer innerhalb des Zeitraums.</param>
public sealed record ReportSummary(
    long MeasuredMs,
    long ElapsedMs,
    int OutageCount,
    long OutageMs,
    IReadOnlyDictionary<OutageCause, int> CountByCause)
{
    public double Coverage => ElapsedMs == 0 ? 0 : (double)MeasuredMs / ElapsedMs;

    /// <summary>Anteil der gemessenen Zeit ohne Aussetzer; <c>null</c> ohne Messung.</summary>
    public double? Availability => MeasuredMs == 0 ? null : 1 - (double)Math.Min(OutageMs, MeasuredMs) / MeasuredMs;
}

/// <summary>
/// Wochenbericht (F-09): die letzten sieben Tage als Raster Tag × Uhrzeit in 15-Minuten-Feldern.
/// Reine Logik; das HTML erzeugt <see cref="WeeklyReportHtml"/>.
/// </summary>
public sealed class WeeklyReport
{
    public const int SlotMinutes = 15;
    public const int SlotsPerDay = 24 * 60 / SlotMinutes;
    public const int DayCount = 7;

    /// <summary>Hinweis für einen Aussetzer, der beim Erstellen des Berichts noch läuft.</summary>
    public const string RunningNote = ReportInputs.RunningNote;

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private WeeklyReport(
        IReadOnlyList<ReportDay> days,
        IReadOnlyList<RecurringWindow> recurring,
        ReportSummary summary,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone)
    {
        Days = days;
        Recurring = recurring;
        Summary = summary;
        NowUtc = nowUtc;
        TimeZone = timeZone;
    }

    /// <summary>Älteste Zeile zuerst, heute zuletzt.</summary>
    public IReadOnlyList<ReportDay> Days { get; }

    /// <summary>Felder mit Aussetzern an mindestens zwei Tagen, häufigste zuerst.</summary>
    public IReadOnlyList<RecurringWindow> Recurring { get; }

    public ReportSummary Summary { get; }

    public DateTimeOffset NowUtc { get; }

    public TimeZoneInfo TimeZone { get; }

    /// <param name="outages">Abgeschlossene Aussetzer (aus netwatch_events.csv).</param>
    /// <param name="sessions">Abgeschlossene Messphasen (aus netwatch_sessions.csv).</param>
    /// <param name="runningSessionStartUtc">Beginn der laufenden Messphase; sie zählt bis <paramref name="nowUtc"/>.</param>
    /// <param name="runningOutage">Gerade laufender Aussetzer; er zählt bis <paramref name="nowUtc"/>.</param>
    /// <param name="nowUtc">Ende des Berichtszeitraums; der Zeitraum umfasst diesen und die sechs Tage davor.</param>
    public static WeeklyReport Create(
        IEnumerable<Outage> outages,
        IEnumerable<Session> sessions,
        DateTimeOffset? runningSessionStartUtc,
        OutageStart? runningOutage,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone)
    {
        // Genau um Mitternacht gehört "jetzt" noch zum Vortag (ein Tag endet um 24:00), sonst bekäme ein
        // Bericht "bis Ende 12.10." eine leere Zeile für den 13.10.
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc.AddTicks(-1), timeZone).DateTime);
        DateOnly firstDay = today.AddDays(-(DayCount - 1));

        List<MeasuredSpan> measured = ReportInputs.MeasuredSpans(sessions, runningSessionStartUtc, nowUtc);
        List<Outage> allOutages = ReportInputs.OutagesWithRunning(outages, runningOutage, nowUtc);

        var grid = new SlotAccumulator[DayCount, SlotsPerDay];
        for (int d = 0; d < DayCount; d++)
        {
            for (int s = 0; s < SlotsPerDay; s++)
            {
                grid[d, s] = new SlotAccumulator();
            }
        }

        // Zeit minutenweise durchgehen: Feldgrenzen und Zeitumstellungen liegen immer auf vollen Minuten.
        DateTimeOffset windowStart = ReportInputs.LocalMidnightUtc(firstDay, timeZone);
        DateTimeOffset windowEnd = ReportInputs.LocalMidnightUtc(today.AddDays(1), timeZone);
        for (DateTimeOffset minute = windowStart; minute < windowEnd; minute += Minute)
        {
            if (!TryLocate(minute, firstDay, timeZone, out int day, out int slot, out bool repeated))
            {
                continue;
            }

            SlotAccumulator cell = grid[day, slot];
            cell.Exists = true;
            cell.RepeatedHour |= repeated;
            if (minute >= nowUtc)
            {
                continue;
            }

            DateTimeOffset minuteEnd = Min(minute + Minute, nowUtc);
            cell.ElapsedMs += (long)(minuteEnd - minute).TotalMilliseconds;
            foreach (MeasuredSpan span in measured)
            {
                cell.MeasuredMs += ReportInputs.OverlapMs(minute, minuteEnd, span.StartUtc, span.EndUtc);
            }
        }

        // Aussetzer allen Feldern zuordnen, die sie berühren.
        DateTimeOffset summaryEnd = Min(nowUtc, windowEnd);
        int outageCount = 0;
        long outageMs = 0;
        var countByCause = new Dictionary<OutageCause, int>();
        foreach (Outage outage in allOutages)
        {
            if (outage.EndUtc < windowStart || outage.StartUtc >= summaryEnd)
            {
                continue; // liegt ganz außerhalb des Berichtszeitraums
            }

            DateTimeOffset start = Max(outage.StartUtc, windowStart);
            DateTimeOffset end = Min(outage.EndUtc, summaryEnd);

            outageCount++;
            outageMs += (long)(end - start).TotalMilliseconds;
            countByCause[outage.Cause] = countByCause.GetValueOrDefault(outage.Cause) + 1;

            for (DateTimeOffset minute = FloorToMinute(start); minute <= end && minute < windowEnd; minute += Minute)
            {
                if (minute == end && minute > start)
                {
                    break; // Endet genau auf einer Minutengrenze: das nächste Feld ist nicht mehr betroffen.
                }

                if (TryLocate(minute, firstDay, timeZone, out int day, out int slot, out _))
                {
                    grid[day, slot].Outages.Add(outage);
                }
            }
        }

        var days = new List<ReportDay>(DayCount);
        for (int d = 0; d < DayCount; d++)
        {
            var slots = new List<ReportSlot>(SlotsPerDay);
            for (int s = 0; s < SlotsPerDay; s++)
            {
                slots.Add(grid[d, s].ToSlot(s));
            }

            days.Add(new ReportDay(firstDay.AddDays(d), slots));
        }

        var summary = new ReportSummary(
            days.Sum(day => day.Slots.Sum(slot => slot.MeasuredMs)),
            days.Sum(day => day.Slots.Sum(slot => slot.ElapsedMs)),
            outageCount,
            outageMs,
            countByCause);

        return new WeeklyReport(days, FindRecurring(days), summary, nowUtc, timeZone);
    }

    /// <summary>Erste Minute des Feldes als Ortszeit-Text, z. B. "02:00".</summary>
    public static string SlotStartText(int slotIndex) => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(slotIndex * SlotMinutes)).ToString("HH:mm");

    /// <summary>Ende des Feldes als Ortszeit-Text, z. B. "02:15"; das letzte Feld endet um "24:00".</summary>
    public static string SlotEndText(int slotIndex) =>
        slotIndex == SlotsPerDay - 1 ? "24:00" : SlotStartText(slotIndex + 1);

    private static List<RecurringWindow> FindRecurring(List<ReportDay> days)
    {
        var recurring = new List<RecurringWindow>();
        for (int s = 0; s < SlotsPerDay; s++)
        {
            List<ReportSlot> column = days.Select(day => day.Slots[s]).ToList();
            int daysWithOutage = column.Count(slot => slot.Outages.Count > 0);
            if (daysWithOutage < 2)
            {
                continue;
            }

            int daysMeasured = column.Count(slot => slot.MeasuredMs > 0 || slot.Outages.Count > 0);
            List<OutageCause> causes = column
                .SelectMany(slot => slot.Outages)
                .Distinct()
                .GroupBy(outage => outage.Cause)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .ToList();
            recurring.Add(new RecurringWindow(s, daysWithOutage, daysMeasured, causes));
        }

        return recurring
            .OrderByDescending(window => window.DaysWithOutage)
            .ThenBy(window => window.SlotIndex)
            .ToList();
    }

    private static bool TryLocate(DateTimeOffset utc, DateOnly firstDay, TimeZoneInfo timeZone, out int day, out int slot, out bool repeated)
    {
        DateTime local = TimeZoneInfo.ConvertTime(utc, timeZone).DateTime;
        day = DateOnly.FromDateTime(local).DayNumber - firstDay.DayNumber;
        slot = (local.Hour * 60 + local.Minute) / SlotMinutes;
        repeated = timeZone.IsAmbiguousTime(local);
        return day is >= 0 and < DayCount;
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private sealed class SlotAccumulator
    {
        public bool Exists { get; set; }

        public bool RepeatedHour { get; set; }

        public long ElapsedMs { get; set; }

        public long MeasuredMs { get; set; }

        public HashSet<Outage> Outages { get; } = [];

        public ReportSlot ToSlot(int index)
        {
            List<Outage> outages = Outages.OrderBy(o => o.StartUtc).ToList();
            long measured = Math.Min(MeasuredMs, ElapsedMs);
            SlotState state = !Exists ? SlotState.Nonexistent
                : ElapsedMs == 0 ? SlotState.Future
                : measured == 0 ? SlotState.NotMeasured
                : measured >= ElapsedMs - 1_000 ? SlotState.Measured
                : SlotState.Partial;

            long longest = outages.Count == 0 ? 0 : outages.Max(o => o.DurationMs);
            int severity = outages.Count == 0 ? 0 : longest < 1_000 ? 1 : longest <= 10_000 ? 2 : 3;
            return new ReportSlot(index, ElapsedMs, measured, outages, state, severity, RepeatedHour);
        }
    }
}
