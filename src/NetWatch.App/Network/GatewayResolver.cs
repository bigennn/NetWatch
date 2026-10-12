using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace NetWatch.App.Network;

internal sealed record GatewayInfo(
    IPAddress Gateway,
    string AdapterName,
    string AdapterDescription,
    NetworkInterfaceType AdapterType,
    OperationalStatus AdapterStatus);

/// <summary>
/// Ermittelt das Standard-Gateway des Adapters, über den Windows ins Internet routet.
/// Bei VPN oder mehreren Adaptern entscheidet damit die Routing-Tabelle, nicht die Reihenfolge der Adapter.
/// </summary>
internal static class GatewayResolver
{
    private static readonly IPAddress RouteProbeAddress = IPAddress.Parse("1.1.1.1");

    /// <returns>Gateway und Adapter, oder <c>null</c>, wenn es keine Route ins Internet gibt.</returns>
    public static GatewayInfo? Resolve()
    {
        uint destination = BitConverter.ToUInt32(RouteProbeAddress.GetAddressBytes(), 0);
        if (GetBestInterface(destination, out uint bestIndex) != 0)
        {
            return null;
        }

        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!adapter.Supports(NetworkInterfaceComponent.IPv4))
            {
                continue;
            }

            IPInterfaceProperties properties = adapter.GetIPProperties();
            if (properties.GetIPv4Properties().Index != bestIndex)
            {
                continue;
            }

            IPAddress? gateway = properties.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

            return gateway is null
                ? null
                : new GatewayInfo(gateway, adapter.Name, adapter.Description, adapter.NetworkInterfaceType, adapter.OperationalStatus);
        }

        return null;
    }

    // Windows-Funktion aus iphlpapi.dll: liefert den Adapter mit der besten Route zu einer IPv4-Adresse.
    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);
}
