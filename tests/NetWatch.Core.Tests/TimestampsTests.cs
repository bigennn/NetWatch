using NetWatch.Core;

namespace NetWatch.Core.Tests;

public class TimestampsTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void Utc_is_formatted_with_milliseconds_and_Z()
    {
        var value = new DateTimeOffset(2026, 10, 13, 0, 0, 3, 412, TimeSpan.Zero);

        Assert.Equal("2026-10-13T00:00:03.412Z", Timestamps.FormatUtc(value));
    }

    [Fact]
    public void Local_time_before_the_clock_change_has_summer_offset()
    {
        // 25.10.2026 00:59:59 UTC = 02:59:59 MESZ, eine Sekunde vor der Zeitumstellung.
        var value = new DateTimeOffset(2026, 10, 25, 0, 59, 59, 0, TimeSpan.Zero);

        Assert.Equal("2026-10-25T02:59:59.000+02:00", Timestamps.FormatLocal(value, Berlin));
    }

    [Fact]
    public void Local_time_after_the_clock_change_has_winter_offset()
    {
        // 25.10.2026 01:00:01 UTC = 02:00:01 MEZ, eine Sekunde nach der Zeitumstellung.
        var value = new DateTimeOffset(2026, 10, 25, 1, 0, 1, 0, TimeSpan.Zero);

        Assert.Equal("2026-10-25T02:00:01.000+01:00", Timestamps.FormatLocal(value, Berlin));
    }
}
