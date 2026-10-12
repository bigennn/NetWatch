using System.Globalization;

namespace NetWatch.Core;

/// <summary>
/// Einheitliches Zeitstempelformat: ISO 8601 mit Millisekunden, UTC mit "Z", Ortszeit mit Versatz.
/// </summary>
public static class Timestamps
{
    public static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string FormatLocal(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
}
