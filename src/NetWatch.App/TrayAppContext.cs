using System.Diagnostics;
using System.Text;
using NetWatch.App.Logging;
using NetWatch.Core;
using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.App;

/// <summary>
/// Tray-Symbol (grau/grün/rot), Hover-Text und Menü; startet den Messbetrieb.
/// Die App hat kein Hauptfenster; sie läuft, bis "Beenden" gewählt wird.
/// </summary>
/// <remarks>
/// Alles hier läuft auf dem UI-Thread. Meldungen vom Mess-Task kommen über <see cref="SynchronizationContext.Post"/>
/// herein: Das reiht nur ein und wartet nicht, damit die Messung nie auf die Oberfläche warten muss,
/// auch nicht, solange das Menü offen ist.
/// </remarks>
internal sealed class TrayAppContext : ApplicationContext
{
    private static readonly TimeSpan TextRefreshInterval = TimeSpan.FromSeconds(10);

    private readonly Dictionary<ConnectionState, Icon> _icons;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _textTimer;
    private readonly SynchronizationContext _ui;
    private readonly NetWatchHost _host;

    private ConnectionState _state = ConnectionState.Paused;
    private DateTimeOffset? _connectedSinceUtc;
    private OutageStart? _runningOutage;
    private Outage? _lastOutage;

    public TrayAppContext()
    {
        _icons = Enum.GetValues<ConnectionState>().ToDictionary(state => state, StatusIcon.Create);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Protokollordner öffnen", null, (_, _) => OpenLogFolder());
        _autostartItem = new ToolStripMenuItem("Autostart", null, (_, _) => ToggleAutostart());
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Beenden", null, (_, _) => ExitThread());
        _menu.Opening += (_, _) => _autostartItem.Checked = Autostart.IsEnabled;

        // Das Erzeugen des Menüs (ein WinForms-Steuerelement) hat den UI-Synchronisationskontext installiert.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icons[ConnectionState.Paused],
            ContextMenuStrip = _menu,
            Visible = true,
        };

        _lastOutage = ReadLastOutage();

        _host = new NetWatchHost();
        _host.Measurement.StateChanged += (_, state) => _ui.Post(_ => OnStateChanged(state), null);
        _host.Measurement.OutageStarted += (_, start) => _ui.Post(_ => _runningOutage = start, null);
        _host.Measurement.OutageEnded += (_, outage) => _ui.Post(_ => OnOutageEnded(outage), null);

        _textTimer = new System.Windows.Forms.Timer { Interval = (int)TextRefreshInterval.TotalMilliseconds };
        _textTimer.Tick += (_, _) => UpdateText();
        _textTimer.Start();

        UpdateText();
        _host.Start();
    }

    private void OnStateChanged(ConnectionState state)
    {
        if (state == ConnectionState.Connected && _state != ConnectionState.Connected)
        {
            _connectedSinceUtc = DateTimeOffset.UtcNow;
        }

        _state = state;
        _notifyIcon.Icon = _icons[state];
        UpdateText();
    }

    private void OnOutageEnded(Outage outage)
    {
        _runningOutage = null;
        _lastOutage = outage;
        UpdateText();
    }

    private void UpdateText() =>
        _notifyIcon.Text = TrayStatusText.Create(
            _state,
            DateTimeOffset.UtcNow,
            _connectedSinceUtc,
            _runningOutage,
            _lastOutage,
            _host.Measurement.GatewayIgnoresPings,
            TimeZoneInfo.Local);

    private static void OpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            ErrorLog.Write($"Protokollordner nicht geöffnet: {ex.Message}");
        }
    }

    private void ToggleAutostart()
    {
        try
        {
            if (Autostart.IsEnabled)
            {
                Autostart.Disable();
            }
            else
            {
                Autostart.Enable();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            ErrorLog.Write($"Autostart nicht geändert: {ex.Message}");
            MessageBox.Show("Der Autostart konnte nicht geändert werden. Details stehen in netwatch_errors.log.", "NetWatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Letzter Aussetzer aus dem Ereignisprotokoll, damit der Hover ihn auch nach einem Neustart zeigt.</summary>
    private static Outage? ReadLastOutage()
    {
        string path = Path.Combine(AppPaths.DataDirectory, EventCsv.FileName);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            Outage? last = null;
            while (reader.ReadLine() is { } line)
            {
                if (EventCsv.TryParseLine(line, out Outage? outage))
                {
                    last = outage;
                }
            }

            return last;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Z. B. in Excel exklusiv geöffnet: dann eben ohne letzten Aussetzer im Hover.
            return null;
        }
    }

    protected override void ExitThreadCore()
    {
        _textTimer.Stop();

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
            _textTimer.Dispose();
            _notifyIcon.Dispose();
            _menu.Dispose();
            foreach (Icon icon in _icons.Values)
            {
                icon.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
