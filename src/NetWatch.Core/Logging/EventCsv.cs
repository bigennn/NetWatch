using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NetWatch.Core.Detection;

namespace NetWatch.Core.Logging;

/// <summary>
/// Format des Ereignisprotokolls netwatch_events.csv (siehe docs/requirements.md).
/// </summary>
public static class EventCsv
{
    public const string FileName = "netwatch_events.csv";

    public const string Header = "start_local;end_local;start_utc;end_utc;duration_ms;cause;failed_targets;gateway;note";

    public static string FormatLine(Outage outage, TimeZoneInfo timeZone) => string.Join(';',
        Timestamps.FormatLocal(outage.StartUtc, timeZone),
        Timestamps.FormatLocal(outage.EndUtc, timeZone),
        Timestamps.FormatUtc(outage.StartUtc),
        Timestamps.FormatUtc(outage.EndUtc),
        outage.DurationMs.ToString(CultureInfo.InvariantCulture),
        CauseName(outage.Cause),
        string.Join(',', outage.FailedTargets),
        outage.Gateway ?? "",
        outage.Note ?? "");

    /// <summary>
    /// Liest eine Zeile zurück, die <see cref="FormatLine"/> erzeugt hat. Die Ortszeit-Spalten werden ignoriert,
    /// maßgeblich sind UTC und Dauer. Kopfzeile und unlesbare Zeilen liefern <c>false</c>.
    /// </summary>
    public static bool TryParseLine(string line, [NotNullWhen(true)] out Outage? outage)
    {
        outage = null;
        string[] columns = line.Split(';');
        if (columns.Length != 9
            || !TryParseUtc(columns[2], out DateTimeOffset startUtc)
            || !TryParseUtc(columns[3], out DateTimeOffset endUtc)
            || !long.TryParse(columns[4], NumberStyles.None, CultureInfo.InvariantCulture, out long durationMs)
            || !TryParseCause(columns[5], out OutageCause cause))
        {
            return false;
        }

        string[] failedTargets = columns[6].Length == 0 ? [] : columns[6].Split(',');
        outage = new Outage(
            startUtc,
            endUtc,
            durationMs,
            cause,
            failedTargets,
            columns[7].Length == 0 ? null : columns[7],
            columns[8].Length == 0 ? null : columns[8]);
        return true;
    }

    private static bool TryParseUtc(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            text,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);

    private static bool TryParseCause(string name, out OutageCause cause)
    {
        foreach (OutageCause candidate in Enum.GetValues<OutageCause>())
        {
            if (CauseName(candidate) == name)
            {
                cause = candidate;
                return true;
            }
        }

        cause = default;
        return false;
    }

    public static string CauseName(OutageCause cause) => cause switch
    {
        OutageCause.PcLan => "pc_lan",
        OutageCause.Router => "router",
        OutageCause.Provider => "provider",
        OutageCause.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };
}
