namespace NetWatch.Core.Detection;

/// <summary>
/// Eine Prüfrunde: alle Ziele wurden im selben Takt gleichzeitig gepingt.
/// </summary>
/// <param name="Number">Laufende Nummer der Runde.</param>
/// <param name="SentAtUtc">Sendezeitpunkt der Runde laut Systemuhr.</param>
/// <param name="SentAtMonotonicMs">Sendezeitpunkt laut monotoner Uhr in ms; Grundlage aller Dauern.</param>
/// <param name="Gateway">Ergebnis für den Router; <c>null</c>, wenn kein Gateway bekannt ist.</param>
/// <param name="Internet">Ergebnisse für die Internetziele (z. B. 1.1.1.1 und 8.8.8.8).</param>
/// <param name="AdapterDown">Windows meldet den Netzwerkadapter als getrennt.</param>
public sealed record ProbeRound(
    long Number,
    DateTimeOffset SentAtUtc,
    long SentAtMonotonicMs,
    ProbeResult? Gateway,
    IReadOnlyList<ProbeResult> Internet,
    bool AdapterDown = false)
{
    /// <summary>Kein Internetziel hat geantwortet.</summary>
    public bool InternetLost => Internet.All(p => p.IsLost);

    /// <summary>Alle Prüfungen der Runde, Gateway zuerst.</summary>
    public IEnumerable<ProbeResult> AllProbes => Gateway is null ? Internet : Internet.Prepend(Gateway);
}
