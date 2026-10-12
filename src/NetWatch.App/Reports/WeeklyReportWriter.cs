using System.Text;
using NetWatch.Core;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.App.Reports;

/// <summary>
/// Liest die Protokolle eines Ordners und schreibt daraus den Wochenbericht als HTML-Datei.
/// </summary>
internal static class WeeklyReportWriter
{
    public const string FileName = "report_week.html";

    /// <param name="logDirectory">Ordner mit netwatch_events.csv und netwatch_sessions.csv.</param>
    /// <param name="outputPath">Zieldatei.</param>
    /// <param name="nowUtc">Ende des Berichtszeitraums.</param>
    /// <param name="runningSessionStartUtc">Beginn der laufenden Messphase, falls NetWatch gerade misst.</param>
    /// <param name="runningOutage">Gerade laufender Aussetzer.</param>
    public static WeeklyReport Write(
        string logDirectory,
        string outputPath,
        DateTimeOffset nowUtc,
        DateTimeOffset? runningSessionStartUtc = null,
        OutageStart? runningOutage = null)
    {
        List<Outage> outages = ReadLines<Outage>(Path.Combine(logDirectory, EventCsv.FileName), EventCsv.TryParseLine);
        List<Session> sessions = ReadLines<Session>(Path.Combine(logDirectory, SessionCsv.FileName), SessionCsv.TryParseLine);

        // Von außen aufgerufen (Befehlszeile): Die laufende Messphase und ein laufender Aussetzer stehen noch
        // nicht in den CSV-Dateien, sondern nur in state.json; sie zählen bis zum letzten Lebenszeichen.
        if (runningSessionStartUtc is null && ReadState(logDirectory) is { } state)
        {
            AddRunningFromState(state, sessions, outages);
        }

        WeeklyReport report = WeeklyReport.Create(outages, sessions, runningSessionStartUtc, runningOutage, nowUtc, TimeZoneInfo.Local);
        File.WriteAllText(outputPath, WeeklyReportHtml.Render(report), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return report;
    }

    private static RunState? ReadState(string logDirectory)
    {
        string path = Path.Combine(logDirectory, RunState.FileName);
        try
        {
            return File.Exists(path) ? RunState.FromJson(File.ReadAllText(path, Encoding.UTF8)) : null;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            return null; // ohne Zustandsdatei fehlt im Bericht nur die laufende Messphase
        }
    }

    private static void AddRunningFromState(RunState state, List<Session> sessions, List<Outage> outages)
    {
        DateTimeOffset heartbeat = state.LastHeartbeatUtc;
        if (state.Session is { } running && heartbeat > running.StartUtc)
        {
            sessions.Add(new Session(running.StartUtc, running.StartReason, heartbeat, WeeklyReport.RunningNote, running.Notes));
        }

        if (state.OpenOutage is { } open && heartbeat >= open.StartUtc)
        {
            long durationMs = (long)(heartbeat - open.StartUtc).TotalMilliseconds;
            outages.Add(new Outage(open.StartUtc, heartbeat, durationMs, open.Cause, open.FailedTargets, open.Gateway, WeeklyReport.RunningNote));
        }
    }

    private delegate bool LineParser<T>(string line, out T? value);

    /// <summary>Liest alle gültigen Zeilen; die Datei darf dabei in Excel geöffnet sein. Fehlt sie, ist die Liste leer.</summary>
    private static List<T> ReadLines<T>(string path, LineParser<T> parse)
        where T : class
    {
        var items = new List<T>();
        if (!File.Exists(path))
        {
            return items;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (parse(line, out T? item) && item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }
}
