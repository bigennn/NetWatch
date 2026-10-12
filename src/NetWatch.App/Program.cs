using NetWatch.App.Cli;
using NetWatch.App.Diagnostics;

namespace NetWatch.App;

static class Program
{
    // "Local\" = je Windows-Anmeldung; jeder Benutzer kann sein eigenes NetWatch haben.
    private const string SingleInstanceName = @"Local\NetWatch.SingleInstance";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--diagnose")
        {
            int seconds = args.Length > 1 && int.TryParse(args[1], out int parsed) && parsed > 0 ? parsed : 600;
            return DiagnoseCommand.Run(seconds);
        }

        if (args.Length > 0 && args[0] is "--report-week" or "--report-day")
        {
            return ReportCommand.Run(args);
        }

        ApplicationConfiguration.Initialize();

        // Nur eine Instanz, sonst schrieben zwei Programme in dieselben Protokolle.
        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "NetWatch läuft bereits. Das Symbol findest du neben der Uhr, eventuell unter dem Pfeil „Ausgeblendete Symbole“.",
                "NetWatch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        using var context = new TrayAppContext();
        Application.Run(context);
        return 0;
    }
}
