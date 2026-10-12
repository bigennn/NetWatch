using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NetWatch.Core;

namespace NetWatch.App.Network;

/// <summary>
/// Pingt alle Ziele einer Prüfrunde gleichzeitig. Braucht keine Administratorrechte.
/// </summary>
internal sealed class IcmpProber : IDisposable
{
    private readonly Stopwatch _clock;
    private readonly int _timeoutMs;
    private readonly Dictionary<IPAddress, Ping> _pings = [];

    /// <param name="clock">Monotone Uhr, aus der die Sendezeitpunkte für Abstände und Dauern stammen.</param>
    /// <param name="timeoutMs">Zeitlimit je Ping; muss kleiner als der Prüftakt sein.</param>
    public IcmpProber(Stopwatch clock, int timeoutMs)
    {
        _clock = clock;
        _timeoutMs = timeoutMs;
    }

    public Task<ProbeResult[]> ProbeRoundAsync(long round, IReadOnlyList<IPAddress> targets) =>
        Task.WhenAll(targets.Select(target => ProbeAsync(round, target)));

    private async Task<ProbeResult> ProbeAsync(long round, IPAddress target)
    {
        // Je Ziel eine eigene Ping-Instanz: eine Instanz kann nur eine Anfrage gleichzeitig senden.
        if (!_pings.TryGetValue(target, out Ping? ping))
        {
            ping = new Ping();
            _pings[target] = ping;
        }

        DateTimeOffset sentAtUtc = DateTimeOffset.UtcNow;
        long sentAtMs = _clock.ElapsedMilliseconds;
        try
        {
            PingReply reply = await ping.SendPingAsync(target, _timeoutMs);
            long? rttMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            return new ProbeResult(round, target.ToString(), sentAtUtc, sentAtMs, rttMs, reply.Status.ToString());
        }
        catch (PingException ex)
        {
            // Tritt z. B. auf, wenn kein Netzwerkadapter verbunden ist.
            string reason = ex.InnerException?.Message ?? ex.Message;
            return new ProbeResult(round, target.ToString(), sentAtUtc, sentAtMs, null, $"PingException: {reason}");
        }
    }

    public void Dispose()
    {
        foreach (Ping ping in _pings.Values)
        {
            ping.Dispose();
        }

        _pings.Clear();
    }
}
