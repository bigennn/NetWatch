using System.Globalization;
using NetWatch.App.Reports;
using NetWatch.Core.Reports;

namespace NetWatch.App.Cli;

/// <summary>
/// Befehlszeile für Berichte aus beliebigen NetWatch-Protokollen (z. B. von einem Teamkollegen), ohne die
/// laufende Messung zu stören:
/// <list type="bullet">
/// <item><c>--report-week &lt;Ordner&gt; [--bis JJJJ-MM-TT] [--aus Datei]</c>: Wochenbericht samt den sieben Tagesberichten.</item>
/// <item><c>--report-day &lt;Ordner&gt; --tag JJJJ-MM-TT [--aus Datei]</c>: ein Tagesbericht.</item>
/// </list>
/// </summary>
internal static class ReportCommand
{
    private const string Usage =
        "Aufruf: NetWatch.exe --report-week <Ordner> [--bis JJJJ-MM-TT] [--aus <Datei.html>]\n" +
        "        NetWatch.exe --report-day <Ordner> --tag JJJJ-MM-TT [--aus <Datei.html>]";

    public static int Run(string[] args)
    {
        ParentConsole.Attach();
        bool daily = args[0] == "--report-day";

        string? folder = null;
        string? output = null;
        DateOnly? date = null;
        for (int i = 1; i < args.Length; i++)
        {
            string option = args[i];
            if ((option == "--bis" && !daily) || (option == "--tag" && daily))
            {
                if (i + 1 >= args.Length || !TryParseDate(args[++i], out DateOnly parsed))
                {
                    return Fail($"Datum nach {option} fehlt oder ist ungültig (Format JJJJ-MM-TT).");
                }

                date = parsed;
            }
            else if (option == "--aus" && i + 1 < args.Length)
            {
                output = args[++i];
            }
            else if (folder is null && !option.StartsWith("--", StringComparison.Ordinal))
            {
                folder = option;
            }
            else
            {
                return Fail($"Unbekanntes Argument: {option}");
            }
        }

        if (folder is null || !Directory.Exists(folder))
        {
            return Fail(folder is null ? "Kein Ordner angegeben." : $"Ordner nicht gefunden: {folder}");
        }

        var writer = new ReportWriter(folder);
        if (daily)
        {
            if (date is not { } day)
            {
                return Fail("--tag fehlt.");
            }

            output ??= Path.Combine(folder, ReportHtml.DailyFileName(day));

            // Liegt der Tag ganz in der Zukunft (z. B. Protokolle eines PCs mit anderem Datum), gilt sein Ende als Stand.
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            if (ReportInputs.LocalMidnightUtc(day, TimeZoneInfo.Local) >= nowUtc)
            {
                nowUtc = ReportInputs.LocalMidnightUtc(day.AddDays(1), TimeZoneInfo.Local);
            }

            DailyReport report = writer.WriteDaily(day, output, nowUtc);
            Console.WriteLine($"Tagesbericht geschrieben: {Path.GetFullPath(output)}");
            Console.WriteLine($"Aussetzer: {report.Summary.OutageCount}, Messabdeckung: {report.Summary.Coverage:P1}, Messlücken: {report.Gaps.Count}");
        }
        else
        {
            // Mit --bis endet der Bericht am Ende dieses Tages, sonst jetzt.
            DateTimeOffset nowUtc = date is { } until
                ? ReportInputs.LocalMidnightUtc(until.AddDays(1), TimeZoneInfo.Local)
                : DateTimeOffset.UtcNow;
            output ??= Path.Combine(folder, ReportHtml.WeeklyFileName);
            WeeklyReport report = writer.WriteWeekly(output, nowUtc);
            Console.WriteLine($"Wochenbericht geschrieben: {Path.GetFullPath(output)} (dazu 7 Tagesberichte im selben Ordner)");
            Console.WriteLine($"Aussetzer: {report.Summary.OutageCount}, Messabdeckung: {report.Summary.Coverage:P1}, wiederkehrende Zeitfenster: {report.Recurring.Count}");
        }

        return 0;
    }

    private static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
