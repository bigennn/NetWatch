using NetWatch.Core;
using NetWatch.Core.Logging;

namespace NetWatch.App;

/// <summary>
/// Hält das Tray-Symbol und sein Menü und startet den Messbetrieb. Die App hat kein Hauptfenster;
/// sie läuft, bis "Beenden" gewählt wird.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly Icon _pausedIcon;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly NetWatchHost _host;

    public TrayAppContext()
    {
        _pausedIcon = StatusIcon.Create(ConnectionState.Paused);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Beenden", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Icon = _pausedIcon,
            Text = "NetWatch – Messung läuft",
            ContextMenuStrip = _menu,
            Visible = true,
        };

        _host = new NetWatchHost();
        _host.Start();
    }

    protected override void ExitThreadCore()
    {
        // Sofort ausblenden, sonst bleibt ein "Geistersymbol" neben der Uhr,
        // bis die Maus darüberfährt.
        _notifyIcon.Visible = false;

        // Auf einem Hintergrund-Thread warten: Die Abschluss-Schritte dürfen nicht auf den
        // UI-Thread angewiesen sein, den wir hier blockieren.
        Task.Run(() => _host.StopAsync(SessionReasons.Exit)).GetAwaiter().GetResult();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _menu.Dispose();
            _pausedIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
