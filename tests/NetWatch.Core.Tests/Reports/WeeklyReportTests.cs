using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.Core.Tests.Reports;

public class WeeklyReportTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Mo 12.10.2026 23:59:59 Ortszeit; Berichtszeitraum Di 06.10. bis Mo 12.10.</summary>
    private static readonly DateTimeOffset EndOfMonday = Local(12, 23, 59, 59);

    private static DateTimeOffset Local(int day, int hour, int minute, int second = 0, int millisecond = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, day, hour, minute, second, millisecond, DateTimeKind.Unspecified), Berlin), TimeSpan.Zero);

    private static Outage Outage(DateTimeOffset startUtc, long durationMs, OutageCause cause = OutageCause.Router, string? note = null) =>
        new(startUtc, startUtc.AddMilliseconds(durationMs), durationMs, cause, ["192.168.178.1", "1.1.1.1", "8.8.8.8"], "192.168.178.1", note);

    private static Session Session(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        new(startUtc, SessionReasons.Start, endUtc, SessionReasons.Exit, []);

    private static Session WholeWeek => Session(Local(6, 0, 0), EndOfMonday);

    private static WeeklyReport Create(IEnumerable<Outage> outages, IEnumerable<Session> sessions, DateTimeOffset? now = null, DateTimeOffset? runningSession = null, OutageStart? runningOutage = null) =>
        WeeklyReport.Create(outages, sessions, runningSession, runningOutage, now ?? EndOfMonday, Berlin);

    private static int Slot(int hour, int minute) => (hour * 60 + minute) / WeeklyReport.SlotMinutes;

    [Fact]
    public void Grid_has_seven_days_of_96_slots_oldest_first()
    {
        WeeklyReport report = Create([], [WholeWeek]);

        Assert.Equal(7, report.Days.Count);
        Assert.All(report.Days, day => Assert.Equal(96, day.Slots.Count));
        Assert.Equal(new DateOnly(2026, 10, 6), report.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 12), report.Days[^1].Date);
    }

    [Fact]
    public void Daily_router_restart_at_two_is_marked_every_day_and_tops_the_recurring_list()
    {
        Outage[] restarts = Enumerable.Range(6, 7).Select(day => Outage(Local(day, 2, 0, 30), 1_800)).ToArray();

        WeeklyReport report = Create(restarts, [WholeWeek]);

        Assert.All(report.Days, day => Assert.Equal(2, day.Slots[Slot(2, 0)].Severity));
        RecurringWindow top = report.Recurring[0];
        Assert.Equal(Slot(2, 0), top.SlotIndex);
        Assert.Equal(7, top.DaysWithOutage);
        Assert.Equal(7, top.DaysMeasured);
        Assert.Equal([OutageCause.Router], top.Causes);
    }

    [Fact]
    public void Outage_on_a_single_day_is_not_recurring()
    {
        WeeklyReport report = Create([Outage(Local(9, 14, 38), 600, OutageCause.PcLan)], [WholeWeek]);

        Assert.Empty(report.Recurring);
        Assert.Equal(1, report.Days[3].Slots[Slot(14, 30)].Severity);
    }

    [Fact]
    public void Time_without_measurement_is_marked_and_does_not_count_for_availability()
    {
        Session[] sessions = [Session(Local(6, 0, 0), Local(9, 10, 0)), Session(Local(9, 14, 0), EndOfMonday)];

        WeeklyReport report = Create([], sessions);

        ReportDay thursday = report.Days[3];
        Assert.All(thursday.Slots.Skip(Slot(10, 0)).Take(16), slot => Assert.Equal(SlotState.NotMeasured, slot.State));
        Assert.Equal(SlotState.Measured, thursday.Slots[Slot(9, 45)].State);
        Assert.Equal(SlotState.Measured, thursday.Slots[Slot(14, 0)].State);
        Assert.Equal(1.0, report.Summary.Availability);
        Assert.True(report.Summary.Coverage < 1);
    }

    [Fact]
    public void Half_measured_slot_is_partial_with_fifty_percent_coverage()
    {
        WeeklyReport report = Create([], [Session(Local(6, 0, 0), Local(9, 10, 7, 30))]);

        ReportSlot slot = report.Days[3].Slots[Slot(10, 0)];
        Assert.Equal(SlotState.Partial, slot.State);
        Assert.Equal(0.5, slot.Coverage, precision: 3);
    }

    [Fact]
    public void Outage_across_midnight_counts_on_both_days()
    {
        Outage outage = Outage(Local(7, 23, 59, 50), 30_000, OutageCause.Provider);

        WeeklyReport report = Create([outage], [WholeWeek]);

        Assert.Contains(outage, report.Days[1].Slots[95].Outages);
        Assert.Contains(outage, report.Days[2].Slots[0].Outages);
    }

    [Fact]
    public void Outage_across_a_slot_boundary_counts_in_both_slots()
    {
        Outage outage = Outage(Local(8, 2, 14, 50), 20_000);

        WeeklyReport report = Create([outage], [WholeWeek]);

        Assert.Contains(outage, report.Days[2].Slots[Slot(2, 0)].Outages);
        Assert.Contains(outage, report.Days[2].Slots[Slot(2, 15)].Outages);
    }

    [Fact]
    public void Outage_ending_exactly_on_a_slot_boundary_stays_in_its_slot()
    {
        Outage outage = Outage(Local(8, 2, 14, 50), 10_000);

        WeeklyReport report = Create([outage], [WholeWeek]);

        Assert.Contains(outage, report.Days[2].Slots[Slot(2, 0)].Outages);
        Assert.Empty(report.Days[2].Slots[Slot(2, 15)].Outages);
    }

    [Fact]
    public void Running_session_and_running_outage_count_until_now()
    {
        var running = new OutageStart(Local(12, 23, 50), OutageCause.Provider, "192.168.178.1", ["1.1.1.1", "8.8.8.8"]);

        WeeklyReport report = Create([], [], runningSession: Local(12, 20, 0), runningOutage: running);

        ReportSlot last = report.Days[^1].Slots[95];
        Outage outage = Assert.Single(last.Outages);
        Assert.Equal(WeeklyReport.RunningNote, outage.Note);
        Assert.Equal(SlotState.Measured, last.State);
        Assert.Equal(SlotState.NotMeasured, report.Days[^1].Slots[Slot(19, 45)].State);
        Assert.Equal(1, report.Summary.OutageCount);
    }

    [Fact]
    public void Slots_after_now_are_future()
    {
        WeeklyReport report = Create([], [Session(Local(6, 0, 0), Local(12, 12, 0))], now: Local(12, 12, 0));

        Assert.Equal(SlotState.Measured, report.Days[^1].Slots[Slot(11, 45)].State);
        Assert.All(report.Days[^1].Slots.Skip(Slot(12, 0)), slot => Assert.Equal(SlotState.Future, slot.State));
    }

    [Fact]
    public void Report_ending_exactly_at_midnight_ends_with_the_previous_day()
    {
        DateTimeOffset midnight = Local(13, 0, 0);

        WeeklyReport report = Create([], [Session(Local(6, 0, 0), midnight)], now: midnight);

        Assert.Equal(new DateOnly(2026, 10, 6), report.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 12), report.Days[^1].Date);
        Assert.DoesNotContain(report.Days.SelectMany(day => day.Slots), slot => slot.State == SlotState.Future);
    }

    [Fact]
    public void Clock_change_puts_the_repeated_hour_into_the_same_slots()
    {
        // 25.10.2026: 02:30 MESZ = 00:30 UTC und 02:30 MEZ = 01:30 UTC fallen beide ins Feld 02:30.
        DateTimeOffset now = new(2026, 10, 25, 22, 0, 0, TimeSpan.Zero);
        Outage[] outages =
        [
            Outage(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), 2_000),
            Outage(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), 2_000),
        ];
        Session session = Session(new DateTimeOffset(2026, 10, 18, 22, 0, 0, TimeSpan.Zero), now);

        WeeklyReport report = WeeklyReport.Create(outages, [session], null, null, now, Berlin);

        ReportDay sunday = report.Days[^1];
        Assert.Equal(new DateOnly(2026, 10, 25), sunday.Date);
        Assert.Equal(96, sunday.Slots.Count);
        Assert.Equal(2, sunday.Slots[Slot(2, 30)].Outages.Count);
        Assert.All(sunday.Slots.Skip(Slot(2, 0)).Take(4), slot =>
        {
            Assert.True(slot.RepeatedHour);
            Assert.Equal(30 * 60_000, slot.ElapsedMs);
        });
        Assert.False(sunday.Slots[Slot(3, 0)].RepeatedHour);
        Assert.DoesNotContain(report.Days.SelectMany(day => day.Slots), slot => slot.State == SlotState.Nonexistent);
    }

    [Theory]
    [InlineData(400, 1)]
    [InlineData(999, 1)]
    [InlineData(1_000, 2)]
    [InlineData(10_000, 2)]
    [InlineData(10_001, 3)]
    public void Severity_follows_the_longest_outage_in_the_slot(long durationMs, int expected)
    {
        WeeklyReport report = Create([Outage(Local(10, 8, 0), durationMs)], [WholeWeek]);

        Assert.Equal(expected, report.Days[4].Slots[Slot(8, 0)].Severity);
    }

    [Fact]
    public void Summary_counts_outages_duration_causes_and_availability()
    {
        Outage[] outages = [Outage(Local(7, 2, 0, 3), 1_802), Outage(Local(10, 14, 38, 51), 245_030, OutageCause.Provider)];

        WeeklyReport report = Create(outages, [WholeWeek]);

        Assert.Equal(2, report.Summary.OutageCount);
        Assert.Equal(246_832, report.Summary.OutageMs);
        Assert.Equal(1, report.Summary.CountByCause[OutageCause.Router]);
        Assert.Equal(1, report.Summary.CountByCause[OutageCause.Provider]);
        Assert.Equal(1 - 246_832.0 / report.Summary.MeasuredMs, report.Summary.Availability!.Value, precision: 9);
        Assert.Equal(1.0, report.Summary.Coverage, precision: 6);
    }

    [Fact]
    public void Outages_before_the_period_are_ignored()
    {
        WeeklyReport report = Create([Outage(Local(5, 23, 0), 5_000)], [WholeWeek]);

        Assert.Equal(0, report.Summary.OutageCount);
        Assert.Empty(report.Recurring);
    }
}
