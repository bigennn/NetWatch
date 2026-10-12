using NetWatch.Core;

namespace NetWatch.App.Logging;

/// <summary>
/// Schreibt unerwartete Fehler nach netwatch_errors.log, damit sie nicht unbemerkt bleiben.
/// Nur außerhalb des Takt-Pfads verwenden.
/// </summary>
internal static class ErrorLog
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        try
        {
            string line = $"{Timestamps.FormatLocal(DateTimeOffset.UtcNow, TimeZoneInfo.Local)}  {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(Path.Combine(AppPaths.DataDirectory, "netwatch_errors.log"), line);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Wenn nicht einmal das Fehlerprotokoll geht, bleibt nichts mehr zu tun.
        }
    }
}
