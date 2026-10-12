using NetWatch.Core.Logging;

namespace NetWatch.Core.Tests.Logging;

/// <summary>Simulierte Uhr: Zeit läuft nur, wenn der Test sie vorstellt.</summary>
internal sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public long MonotonicMs { get; private set; } = 5_000_000;

    public void Advance(TimeSpan by)
    {
        UtcNow += by;
        MonotonicMs += (long)by.TotalMilliseconds;
    }
}

/// <summary>Simulierte Datei: kann gesperrt werden wie eine in Excel geöffnete CSV.</summary>
internal sealed class FakeSink : ILineSink
{
    public bool Locked { get; set; }

    public List<string> Lines { get; } = [];

    public int FailedAttempts { get; private set; }

    public bool TryAppend(IReadOnlyList<string> lines)
    {
        if (Locked)
        {
            FailedAttempts++;
            return false;
        }

        Lines.AddRange(lines);
        return true;
    }
}
