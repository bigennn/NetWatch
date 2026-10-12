using System.Net.NetworkInformation;

namespace NetWatch.App.Network;

/// <summary>
/// Hält Gateway und Adapterstatus aktuell: beim Start, alle 60 s und bei jeder Netzwerkänderung.
/// Die Ermittlung läuft außerhalb des Takt-Pfads; der Prober liest nur den zuletzt bekannten Stand.
/// </summary>
internal sealed class NetworkMonitor : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly object _refreshLock = new();
    private readonly System.Threading.Timer _timer;
    private volatile GatewayInfo? _gateway;

    public NetworkMonitor()
    {
        Refresh();
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        _timer = new System.Threading.Timer(_ => Refresh(), null, RefreshInterval, RefreshInterval);
    }

    /// <summary>Zuletzt ermitteltes Gateway samt Adapter; <c>null</c> ohne Route ins Internet.</summary>
    public GatewayInfo? Gateway => _gateway;

    /// <summary>Windows meldet den Adapter mit der Route ins Internet als nicht verbunden.</summary>
    public bool AdapterDown => _gateway is { AdapterStatus: not OperationalStatus.Up };

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        lock (_refreshLock)
        {
            try
            {
                _gateway = GatewayResolver.Resolve();
            }
            catch (NetworkInformationException)
            {
                _gateway = null;
            }
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _timer.Dispose();
    }
}
