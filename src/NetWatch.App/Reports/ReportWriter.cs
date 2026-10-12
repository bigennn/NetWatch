using System.Text;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;
using NetWatch.Core.Reports;

namespace NetWatch.App.Reports;

/// <summary>
/// Liest die Protokolle eines Ordners und schreibt daraus Wochen- und Tagesberichte als HTML-Dateien.
/// </summary>
/// <param name="logDirectory">Ordner mit netwatch_events.csv, netwatch_sessions.csv und gegebenenfalls state.json.</param>
/// <param name="runningSessionStartUtc">
/// Beginn der laufenden Messphase, wenn NetWatch selbst den Bericht erzeugt; sonst wird sie aus state.json gelesen
/// und zählt bis zum letzten Lebenszeichen.
/// </param>
/// <param name="runningOutage">Gerade laufender Aussetzer.</param>
internal sealed class ReportWriter(string logDirectory, DateTimeOffset? runningSessionStartUtc = null, OutageStart? runningOutage = null)
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private (List<Outage> Outages, List<Session> Sessions)? _data;

    /// <summary>Schreibt den Wochenbericht und daneben die sieben verlinkten Tagesberichte.</summary>
    public WeeklyReport WriteWeekly(string outputPath, DateTimeOffset nowUtc)
    {
        (List<Outage> outages, List<Session> sessions) = Load();
        WeeklyReport report = WeeklyReport.Create(outages, sessions, runningSessionStartUtc, runningOutage, nowUtc, TimeZoneInfo.Local);
        File.WriteAllText(outputPath, WeeklyReportHtml.Render(report), Utf8);

        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        foreach (ReportDay day in report.Days)
        {
            WriteDaily(day.Date, Path.Combine(directory, ReportHtml.DailyFileName(day.Date)), nowUtc);
        }

        return report;
    }

    public DailyReport WriteDaily(DateOnly date, string outputPath, DateTimeOffset nowUtc)
    {
        (List<Outage> outages, List<Session> sessions) = Load();
        DailyReport report = DailyReport.Create(outages, sessions, runningSessionStartUtc, runningOutage, date, nowUtc, TimeZoneInfo.Local);
        File.WriteAllText(outputPath, DailyReportHtml.Render(report), Utf8);
        return report;
    }

    /// <summary>Tage (Ortszeit), an denen gemessen wurde; für die Kennzeichnung im Tray-Menü.</summary>
    public HashSet<DateOnly> MeasuredDays(DateTimeOffset nowUtc)
    {
        var days = new HashSet<DateOnly>();
        foreach (MeasuredSpan span in ReportInputs.MeasuredSpans(Load().Sessions, runningSessionStartUtc, nowUtc))
        {
            DateOnly first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(span.StartUtc, TimeZoneInfo.Local).DateTime);
            DateOnly last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(span.EndUtc.AddTicks(-1), TimeZoneInfo.Local).DateTime);
            for (DateOnly day = first; day <= last; day = day.AddDays(1))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private (List<Outage> Outages, List<Session> Sessions) Load()
    {
        if (_data is { } data)
        {
            return data;
        }

        List<Outage> outages = ReadLines<Outage>(Path.Combine(logDirectory, EventCsv.FileName), EventCsv.TryParseLine);
        List<Session> sessions = ReadLines<Session>(Path.Combine(logDirectory, SessionCsv.FileName), SessionCsv.TryParseLine);

        // Von außen aufgerufen (Befehlszeile): Die laufende Messphase und ein laufender Aussetzer stehen noch
        // nicht in den CSV-Dateien, sondern nur in state.json; sie zählen bis zum letzten Lebenszeichen.
        if (runningSessionStartUtc is null && ReadState() is { } state)
        {
            AddRunningFromState(state, sessions, outages);
        }

        _data = (outages, sessions);
        return _data.Value;
    }

    private RunState? ReadState()
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
            sessions.Add(new Session(running.StartUtc, running.StartReason, heartbeat, ReportInputs.RunningNote, running.Notes));
        }

        if (state.OpenOutage is { } open && heartbeat >= open.StartUtc)
        {
            long durationMs = (long)(heartbeat - open.StartUtc).TotalMilliseconds;
            outages.Add(new Outage(open.StartUtc, heartbeat, durationMs, open.Cause, open.FailedTargets, open.Gateway, ReportInputs.RunningNote));
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
