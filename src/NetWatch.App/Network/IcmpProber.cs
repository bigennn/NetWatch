using System.Net;
using System.Net.NetworkInformation;
using NetWatch.Core;

namespace NetWatch.App.Network;

/// <summary>
/// Pingt alle Ziele einer Prüfrunde gleichzeitig. Braucht keine Administratorrechte.
/// </summary>
/// <remarks>
/// <para>
/// Windows hält das Ping-Zeitlimit nicht immer ein (M1: bis 504 ms beim Adapterwechsel). Deshalb wartet
/// der Prober höchstens selbst <c>timeoutMs</c> und wertet eine spätere Antwort als Verlust; die hängende
/// Prüfung läuft im Hintergrund zu Ende, ohne die nächste Runde aufzuhalten.
/// </para>
/// <para>
/// Bewusst das synchrone <see cref="Ping.Send(IPAddress, int)"/> auf einem Thread-Pool-Thread statt
/// <see cref="Ping.SendPingAsync(IPAddress, int)"/>: Die asynchrone Variante belegt je Aufruf ein
/// Windows-Handle, das erst ein vollständiger Garbage-Collector-Lauf freigibt (M3: rund 900 Handles pro
/// Minute im Dauerbetrieb). Die synchrone Variante belegt keines.
/// </para>
/// </remarks>
internal sealed class IcmpProber(IClock clock, int timeoutMs)
{
    /// <summary>Status, wenn das eigene Zeitlimit greift, bevor Windows antwortet.</summary>
    public const string LocalTimeoutStatus = "LocalTimeout";

    public Task<ProbeResult[]> ProbeRoundAsync(long round, IReadOnlyList<IPAddress> targets) =>
        Task.WhenAll(targets.Select(target => ProbeAsync(round, target)));

    private async Task<ProbeResult> ProbeAsync(long round, IPAddress target)
    {
        DateTimeOffset sentAtUtc = clock.UtcNow;
        long sentAtMs = clock.MonotonicMs;

        Task<ProbeResult> probe = Task.Run(() => Send(round, target, sentAtUtc, sentAtMs));
        if (await Task.WhenAny(probe, Task.Delay(timeoutMs)).ConfigureAwait(false) != probe)
        {
            return new ProbeResult(round, target.ToString(), sentAtUtc, sentAtMs, null, LocalTimeoutStatus);
        }

        return await probe.ConfigureAwait(false);
    }

    /// <summary>Ein blockierender Ping. Fängt alle erwartbaren Fehler selbst, damit eine verlassene Prüfung nie unbeobachtet fehlschlägt.</summary>
    private ProbeResult Send(long round, IPAddress target, DateTimeOffset sentAtUtc, long sentAtMs)
    {
        try
        {
            using var ping = new Ping();
            PingReply reply = ping.Send(target, timeoutMs);
            long? rttMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            return new ProbeResult(round, target.ToString(), sentAtUtc, sentAtMs, rttMs, reply.Status.ToString());
        }
        catch (Exception ex) when (ex is PingException or InvalidOperationException)
        {
            // Tritt z. B. auf, wenn kein Netzwerkadapter verbunden ist.
            return new ProbeResult(round, target.ToString(), sentAtUtc, sentAtMs, null, $"PingException: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
