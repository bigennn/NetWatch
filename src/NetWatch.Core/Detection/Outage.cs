namespace NetWatch.Core.Detection;

/// <summary>
/// Eingegrenzte Ursache eines Aussetzers.
/// </summary>
public enum OutageCause
{
    /// <summary>Netzwerkadapter getrennt (Kabel, Netzwerkkarte).</summary>
    PcLan,

    /// <summary>Auch der Router antwortet nicht, typisch für einen Router-Neustart.</summary>
    Router,

    /// <summary>Der Router antwortet, das Internet dahinter nicht.</summary>
    Provider,

    /// <summary>Nicht eingrenzbar, weil der Router grundsätzlich keine Pings beantwortet.</summary>
    Unknown,
}

/// <summary>
/// Werte für <see cref="Outage.Note"/>.
/// </summary>
public static class OutageNotes
{
    /// <summary>Der PC ging während des Aussetzers in den Standby; das Ende ist der Standby-Beginn.</summary>
    public const string Suspended = "suspended";

    /// <summary>NetWatch wurde während des Aussetzers beendet; das Ende ist der Zeitpunkt des Beendens.</summary>
    public const string AppExit = "app_exit";

    /// <summary>NetWatch stürzte während des Aussetzers ab; das Ende ist unbekannt (M3).</summary>
    public const string EndUnknown = "end_unknown";
}

/// <summary>
/// Ein abgeschlossener Aussetzer.
/// </summary>
/// <param name="StartUtc">Sendezeit der ersten Runde ohne Antwort eines Internetziels.</param>
/// <param name="EndUtc">Immer <see cref="StartUtc"/> plus <see cref="DurationMs"/>, damit beides zusammenpasst.</param>
/// <param name="DurationMs">Dauer laut monotoner Uhr.</param>
/// <param name="Cause">Am weitesten vorne liegende Ursache, die während des Aussetzers auftrat.</param>
/// <param name="FailedTargets">Alle Ziele, die während des Aussetzers mindestens einmal nicht antworteten; Gateway zuerst.</param>
/// <param name="Gateway">Gateway zu Beginn des Aussetzers.</param>
/// <param name="Note">Besonderheit beim Ende, siehe <see cref="OutageNotes"/>; sonst <c>null</c>.</param>
public sealed record Outage(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    long DurationMs,
    OutageCause Cause,
    IReadOnlyList<string> FailedTargets,
    string? Gateway,
    string? Note);

/// <summary>
/// Meldung, dass ein Aussetzer begonnen hat (nach der Mindestzahl verlorener Runden, Beginn rückdatiert).
/// </summary>
public sealed record OutageStart(DateTimeOffset StartUtc, OutageCause Cause, string? Gateway);
