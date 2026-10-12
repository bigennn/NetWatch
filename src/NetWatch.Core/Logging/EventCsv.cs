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

    public static string CauseName(OutageCause cause) => cause switch
    {
        OutageCause.PcLan => "pc_lan",
        OutageCause.Router => "router",
        OutageCause.Provider => "provider",
        OutageCause.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };
}
