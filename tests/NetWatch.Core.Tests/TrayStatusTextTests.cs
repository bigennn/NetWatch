using NetWatch.Core;
using NetWatch.Core.Detection;

namespace NetWatch.Core.Tests;

public class TrayStatusTextTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // 12.10.2026 16:37:00 MESZ
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 37, 0, TimeSpan.Zero);

    private static readonly Outage LastCableTest = new(
        new DateTimeOffset(2026, 10, 12, 14, 32, 29, 357, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 12, 14, 32, 39, 761, TimeSpan.Zero),
        10_404,
        OutageCause.PcLan,
        ["192.168.178.1", "1.1.1.1", "8.8.8.8"],
        "192.168.178.1",
        null);

    [Fact]
    public void Connected_shows_since_when_and_for_how_long_plus_the_last_outage()
    {
        string text = TrayStatusText.Create(ConnectionState.Connected, Now, Now.AddMinutes(-125), null, LastCableTest, false, Berlin);

        Assert.Equal("NetWatch – verbunden seit 14:32 (2 h 5 min)\nLetzter Aussetzer: 12.10. 16:32:29, 10,4 s, PC/LAN", text);
    }

    [Fact]
    public void Outage_shows_start_time_with_seconds_and_cause()
    {
        var running = new OutageStart(Now.AddSeconds(-20), OutageCause.Router, "192.168.178.1", ["192.168.178.1"]);

        string text = TrayStatusText.Create(ConnectionState.Outage, Now, null, running, null, false, Berlin);

        Assert.Equal("NetWatch – Aussetzer seit 16:36:40 (Router)", text);
    }

    [Fact]
    public void Paused_explains_that_there_is_no_valid_measurement()
    {
        string text = TrayStatusText.Create(ConnectionState.Paused, Now, null, null, null, false, Berlin);

        Assert.Equal("NetWatch – keine gültige Messung (Start oder Aufwachen)", text);
    }

    [Fact]
    public void Router_hint_is_added_as_its_own_line()
    {
        string text = TrayStatusText.Create(ConnectionState.Connected, Now, Now.AddMinutes(-3), null, null, true, Berlin);

        Assert.EndsWith("\nRouter beantwortet keine Pings", text);
    }

    [Fact]
    public void Connected_since_yesterday_includes_the_date()
    {
        string text = TrayStatusText.Create(ConnectionState.Connected, Now, Now.AddDays(-1), null, null, false, Berlin);

        Assert.Equal("NetWatch – verbunden seit 11.10. 16:37 (1 d 0 h)", text);
    }

    [Fact]
    public void Text_never_exceeds_the_windows_limit_and_drops_lines_from_the_end()
    {
        var running = new OutageStart(Now.AddDays(-2), OutageCause.Unknown, null, []);

        string text = TrayStatusText.Create(ConnectionState.Outage, Now, null, running, LastCableTest, true, Berlin);

        Assert.True(text.Length <= TrayStatusText.MaxLength, $"{text.Length} Zeichen");
        Assert.StartsWith("NetWatch – Aussetzer seit 10.10. 16:37:00 (unbekannt)", text);
    }

    [Theory]
    [InlineData(400, "0,4 s")]
    [InlineData(8_790, "8,8 s")]
    [InlineData(59_940, "59,9 s")]
    [InlineData(59_960, "1 min 0 s")]
    [InlineData(245_030, "4 min 5 s")]
    [InlineData(7_500_000, "2 h 5 min")]
    [InlineData(273_600_000, "3 d 4 h")]
    public void Durations_are_formatted_for_people(long ms, string expected)
    {
        Assert.Equal(expected, TrayStatusText.FormatDuration(ms));
    }
}
