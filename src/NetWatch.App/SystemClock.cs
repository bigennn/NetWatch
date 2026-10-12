using System.Diagnostics;
using NetWatch.Core;

namespace NetWatch.App;

/// <summary>Echte Uhr: Systemzeit für Zeitpunkte, <see cref="Stopwatch"/> für Dauern.</summary>
internal sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long MonotonicMs => _stopwatch.ElapsedMilliseconds;
}
