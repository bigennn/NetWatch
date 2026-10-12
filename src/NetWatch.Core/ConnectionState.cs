namespace NetWatch.Core;

/// <summary>
/// Zustand der Verbindung, wie ihn das Tray-Symbol anzeigt.
/// </summary>
public enum ConnectionState
{
    /// <summary>Keine gültige Messung (z. B. beim Start oder nach dem Aufwachen). Symbol grau.</summary>
    Paused = 0,

    /// <summary>Mindestens ein Internetziel antwortet. Symbol grün.</summary>
    Connected,

    /// <summary>Ein Aussetzer läuft gerade. Symbol rot.</summary>
    Outage,
}
