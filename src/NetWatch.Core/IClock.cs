namespace NetWatch.Core;

/// <summary>
/// Uhr mit zwei Zeitquellen: Systemzeit für Zeitpunkte, monotone Zeit für Abstände und Dauern.
/// </summary>
public interface IClock
{
    /// <summary>Aktueller Zeitpunkt laut Systemuhr (kann von Windows nachgestellt werden).</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Monotone Zeit in ms; springt nie, eignet sich nur für Differenzen.</summary>
    long MonotonicMs { get; }
}
