using NetWatch.App.Diagnostics;
using NetWatch.Core;

namespace NetWatch.App;

/// <summary>
/// Hält das Tray-Symbol und sein Menü. Die App hat kein Hauptfenster;
/// sie läuft, bis "Beenden" gewählt wird.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly Icon _pausedIcon;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly PowerEventLog _powerLog;

    public TrayAppContext()
    {
        _powerLog = new PowerEventLog(Path.Combine(AppPaths.DataDirectory, "diagnose_power.log"));
        _pausedIcon = StatusIcon.Create(ConnectionState.Paused);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Beenden", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Icon = _pausedIcon,
            Text = "NetWatch – Messung pausiert",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    protected override void ExitThreadCore()
    {
        // Sofort ausblenden, sonst bleibt ein "Geistersymbol" neben der Uhr,
        // bis die Maus darüberfährt.
        _notifyIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _menu.Dispose();
            _pausedIcon.Dispose();
            _powerLog.Dispose();
        }

        base.Dispose(disposing);
    }
}
