namespace NetWatch.Core;

/// <summary>
/// Ergebnis einer einzelnen Prüfung (Ping) an ein Prüfziel.
/// </summary>
/// <param name="Round">Nummer der Prüfrunde; alle Ziele einer Runde werden gleichzeitig geprüft.</param>
/// <param name="Target">IP-Adresse des Prüfziels.</param>
/// <param name="SentAtUtc">Sendezeitpunkt laut Systemuhr.</param>
/// <param name="SentAtMonotonicMs">Sendezeitpunkt laut monotoner Uhr in ms; nur für Abstände und Dauern verwenden.</param>
/// <param name="RttMs">Antwortzeit in ms; <c>null</c> bei Verlust.</param>
/// <param name="Status">Status laut Windows, z. B. "Success" oder "TimedOut".</param>
public sealed record ProbeResult(
    long Round,
    string Target,
    DateTimeOffset SentAtUtc,
    long SentAtMonotonicMs,
    long? RttMs,
    string Status)
{
    /// <summary>
    /// Status, den Windows sofort für jeden Ping liefert, solange der Netzwerkadapter getrennt ist
    /// (IP_GENERAL_FAILURE, beobachtet im Kabeltest von M1).
    /// </summary>
    public const string GeneralFailureStatus = "11050";

    public bool IsLost => RttMs is null;
}
