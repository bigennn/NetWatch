namespace NetWatch.Core.Logging;

/// <summary>Werte für Start- und Endgründe einer Messphase.</summary>
public static class SessionReasons
{
    public const string Start = "start";
    public const string Resume = "resume";
    public const string Exit = "exit";
    public const string Suspend = "suspend";
    public const string Shutdown = "shutdown";
    public const string CrashDetected = "crash_detected";
}

/// <summary>Werte für die Spalte <c>note</c> im Laufzeitprotokoll.</summary>
public static class SessionNotes
{
    /// <summary>Der Router beantwortete keine Pings; Ursachen heißen dann <c>unknown</c>.</summary>
    public const string GatewayIgnoresPing = "gateway_ignores_ping";

    /// <summary>Ende nach einem Absturz aus dem letzten Lebenszeichen geschätzt.</summary>
    public const string EndEstimated = "end_estimated";
}

/// <summary>Eine abgeschlossene Messphase.</summary>
public sealed record Session(
    DateTimeOffset StartUtc,
    string StartReason,
    DateTimeOffset EndUtc,
    string EndReason,
    IReadOnlyList<string> Notes);

/// <summary>
/// Format des Laufzeitprotokolls netwatch_sessions.csv.
/// </summary>
public static class SessionCsv
{
    public const string FileName = "netwatch_sessions.csv";

    public const string Header = "start_local;end_local;start_utc;end_utc;start_reason;end_reason;note";

    public static string FormatLine(Session session, TimeZoneInfo timeZone) => string.Join(';',
        Timestamps.FormatLocal(session.StartUtc, timeZone),
        Timestamps.FormatLocal(session.EndUtc, timeZone),
        Timestamps.FormatUtc(session.StartUtc),
        Timestamps.FormatUtc(session.EndUtc),
        session.StartReason,
        session.EndReason,
        string.Join(',', session.Notes));
}
