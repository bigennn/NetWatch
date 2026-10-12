using System.Globalization;
using NetWatch.Core.Detection;

namespace NetWatch.Core;

/// <summary>
/// Erzeugt den deutschen Hover-Text des Tray-Symbols.
/// </summary>
public static class TrayStatusText
{
    /// <summary>Höchstlänge, die Windows für den Hover-Text eines Tray-Symbols erlaubt.</summary>
    public const int MaxLength = 127;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <param name="state">Aktueller Zustand.</param>
    /// <param name="nowUtc">Jetzt; für die Dauer der Verbindung.</param>
    /// <param name="connectedSinceUtc">Seit wann die Verbindung besteht; <c>null</c>, wenn unbekannt.</param>
    /// <param name="runningOutage">Gerade laufender Aussetzer, falls <paramref name="state"/> <c>Outage</c> ist.</param>
    /// <param name="lastOutage">Letzter abgeschlossener Aussetzer, falls bekannt.</param>
    /// <param name="gatewayIgnoresPings">Der Router beantwortet keine Pings.</param>
    /// <param name="timeZone">Zeitzone für die angezeigten Uhrzeiten.</param>
    public static string Create(
        ConnectionState state,
        DateTimeOffset nowUtc,
        DateTimeOffset? connectedSinceUtc,
        OutageStart? runningOutage,
        Outage? lastOutage,
        bool gatewayIgnoresPings,
        TimeZoneInfo timeZone)
    {
        var lines = new List<string>(3);
        lines.Add(state switch
        {
            ConnectionState.Connected when connectedSinceUtc is { } since =>
                $"NetWatch – verbunden seit {FormatTime(since, nowUtc, timeZone, withSeconds: false)} ({FormatDuration((long)(nowUtc - since).TotalMilliseconds)})",
            ConnectionState.Connected => "NetWatch – verbunden",
            ConnectionState.Outage when runningOutage is not null =>
                $"NetWatch – Aussetzer seit {FormatTime(runningOutage.StartUtc, nowUtc, timeZone, withSeconds: true)} ({CauseText(runningOutage.Cause)})",
            ConnectionState.Outage => "NetWatch – Aussetzer läuft",
            _ => "NetWatch – keine gültige Messung (Start oder Aufwachen)",
        });

        if (lastOutage is not null)
        {
            DateTimeOffset localStart = TimeZoneInfo.ConvertTime(lastOutage.StartUtc, timeZone);
            lines.Add($"Letzter Aussetzer: {localStart.ToString("dd.MM. HH:mm:ss", German)}, {FormatDuration(lastOutage.DurationMs)}, {CauseText(lastOutage.Cause)}");
        }

        if (gatewayIgnoresPings)
        {
            lines.Add("Router beantwortet keine Pings");
        }

        return FitToMaxLength(lines);
    }

    /// <summary>Dauer für Menschen: "8,8 s", "4 min 5 s", "2 h 5 min", "3 d 4 h".</summary>
    public static string FormatDuration(long ms)
    {
        var span = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        double seconds = Math.Round(span.TotalSeconds, 1, MidpointRounding.AwayFromZero);
        if (seconds < 60)
        {
            return seconds.ToString("0.0", German) + " s";
        }

        long totalSeconds = (long)Math.Round(span.TotalSeconds, MidpointRounding.AwayFromZero);
        if (totalSeconds < 3600)
        {
            return $"{totalSeconds / 60} min {totalSeconds % 60} s";
        }

        if (span.TotalDays < 1)
        {
            return $"{span.Hours} h {span.Minutes} min";
        }

        return $"{(int)span.TotalDays} d {span.Hours} h";
    }

    public static string CauseText(OutageCause cause) => cause switch
    {
        OutageCause.PcLan => "PC/LAN",
        OutageCause.Router => "Router",
        OutageCause.Provider => "Provider",
        _ => "unbekannt",
    };

    /// <summary>Uhrzeit, mit Datum, wenn sie nicht von heute ist.</summary>
    private static string FormatTime(DateTimeOffset utc, DateTimeOffset nowUtc, TimeZoneInfo timeZone, bool withSeconds)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, timeZone);
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        string time = withSeconds ? "HH:mm:ss" : "HH:mm";
        return local.ToString(local.Date == localNow.Date ? time : $"dd.MM. {time}", German);
    }

    /// <summary>Lässt Zeilen von hinten weg, bis der Text passt; kürzt notfalls die erste Zeile.</summary>
    private static string FitToMaxLength(List<string> lines)
    {
        while (lines.Count > 1 && string.Join('\n', lines).Length > MaxLength)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        string text = string.Join('\n', lines);
        return text.Length <= MaxLength ? text : text[..(MaxLength - 1)] + "…";
    }
}
