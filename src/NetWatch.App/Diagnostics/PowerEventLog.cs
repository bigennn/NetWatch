using System.Diagnostics;
using Microsoft.Win32;
using NetWatch.Core;

namespace NetWatch.App.Diagnostics;

/// <summary>
/// Spike S2: schreibt Standby, Aufwachen und Herunterfahren nach diagnose_power.log.
/// Vergleicht beim Aufwachen die Standby-Dauer laut Systemuhr mit der laut Stopwatch,
/// um zu klären, ob die monotone Uhr im Standby weiterläuft. Wird in M3 durch den SessionTracker ersetzt.
/// </summary>
internal sealed class PowerEventLog : IDisposable
{
    private const string Header = "utc;local;monotonic_ms;event;detail";

    private readonly string _filePath;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DateTimeOffset _suspendedAtUtc;
    private long _suspendedAtMs = -1;
    private bool _disposed;

    public PowerEventLog(string filePath)
    {
        _filePath = filePath;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        Write("app_start");
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _suspendedAtUtc = DateTimeOffset.UtcNow;
                _suspendedAtMs = _clock.ElapsedMilliseconds;
                Write("suspend");
                break;

            case PowerModes.Resume:
                string detail = "";
                if (_suspendedAtMs >= 0)
                {
                    long wallClockMs = (long)(DateTimeOffset.UtcNow - _suspendedAtUtc).TotalMilliseconds;
                    long stopwatchMs = _clock.ElapsedMilliseconds - _suspendedAtMs;
                    detail = $"wall_clock_gap_ms={wallClockMs}, stopwatch_gap_ms={stopwatchMs}";
                }

                Write("resume", detail);
                break;

            default:
                Write("status_change");
                break;
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => Write("session_ending", e.Reason.ToString());

    private void Write(string eventName, string detail = "")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string line = $"{Timestamps.FormatUtc(now)};{Timestamps.FormatLocal(now, TimeZoneInfo.Local)};{_clock.ElapsedMilliseconds};{eventName};{detail}";
        try
        {
            if (!File.Exists(_filePath))
            {
                File.AppendAllText(_filePath, Header + Environment.NewLine);
            }

            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
        catch (IOException)
        {
            // Diagnosedatei ist nicht wichtig genug, um die App zu stören.
        }
    }

    public void Dispose()
    {
        // WinForms ruft Dispose beim Beenden mehrfach auf; app_exit soll nur einmal im Log stehen.
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // SystemEvents sind statisch: ohne Abmelden hielte Windows unser Objekt fest.
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        Write("app_exit");
    }
}
