using NetWatch.App.Diagnostics;

namespace NetWatch.App;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--diagnose")
        {
            int seconds = args.Length > 1 && int.TryParse(args[1], out int parsed) && parsed > 0 ? parsed : 600;
            return DiagnoseCommand.Run(seconds);
        }

        ApplicationConfiguration.Initialize();

        using var context = new TrayAppContext();
        Application.Run(context);
        return 0;
    }
}
