using System.Globalization;
using NetWatch.App.Reports;
using NetWatch.Core.Reports;

namespace NetWatch.App.Cli;

/// <summary>
/// Befehlszeile <c>--report-week &lt;Ordner&gt; [--bis JJJJ-MM-TT] [--aus Datei]</c>: erzeugt den Wochenbericht
/// aus beliebigen NetWatch-Protokollen, z. B. denen eines Teamkollegen, ohne die laufende Messung zu stören.
/// </summary>
internal static class ReportWeekCommand
{
    private const string Usage = "Aufruf: NetWatch.exe --report-week <Ordner> [--bis JJJJ-MM-TT] [--aus <Datei.html>]";

    public static int Run(string[] args)
    {
        ParentConsole.Attach();

        string? folder = null;
        string? output = null;
        DateOnly? until = null;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bis" when i + 1 < args.Length
                    && DateOnly.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date):
                    until = date;
                    i++;
                    break;
                case "--aus" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                default:
                    if (folder is not null || args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unbekanntes Argument: {args[i]}");
                        Console.Error.WriteLine(Usage);
                        return 2;
                    }

                    folder = args[i];
                    break;
            }
        }

        if (folder is null || !Directory.Exists(folder))
        {
            Console.Error.WriteLine(folder is null ? "Kein Ordner angegeben." : $"Ordner nicht gefunden: {folder}");
            Console.Error.WriteLine(Usage);
            return 2;
        }

        output ??= Path.Combine(folder, WeeklyReportWriter.FileName);

        // Mit --bis endet der Bericht am Ende dieses Tages, sonst jetzt.
        DateTimeOffset nowUtc = until is { } day
            ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local), TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

        WeeklyReport report = WeeklyReportWriter.Write(folder, output, nowUtc);
        Console.WriteLine($"Wochenbericht geschrieben: {Path.GetFullPath(output)}");
        Console.WriteLine($"Aussetzer: {report.Summary.OutageCount}, Messabdeckung: {report.Summary.Coverage:P1}, wiederkehrende Zeitfenster: {report.Recurring.Count}");
        return 0;
    }
}
