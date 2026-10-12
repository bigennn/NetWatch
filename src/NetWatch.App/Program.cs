namespace NetWatch.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var context = new TrayAppContext();
        Application.Run(context);
    }
}
