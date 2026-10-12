using System.Text.Json;
using System.Text.Json.Serialization;
using NetWatch.Core.Detection;

namespace NetWatch.Core.Logging;

/// <summary>Eine laufende, noch nicht abgeschlossene Messphase.</summary>
public sealed record RunningSession(DateTimeOffset StartUtc, string StartReason, IReadOnlyList<string> Notes);

/// <summary>
/// Inhalt der Zustandsdatei state.json. Existiert sie beim Start, ist der letzte Lauf abgestürzt
/// (oder hatte beim Beenden noch ungeschriebene Zeilen).
/// </summary>
public sealed record RunState
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public const string FileName = "state.json";

    /// <summary>Laufende Messphase; <c>null</c> im Standby oder nach sauberem Beenden.</summary>
    public RunningSession? Session { get; init; }

    /// <summary>Letztes Lebenszeichen; nach einem Absturz die Schätzung für das Ende.</summary>
    public required DateTimeOffset LastHeartbeatUtc { get; init; }

    /// <summary>Gerade laufender Aussetzer, falls vorhanden.</summary>
    public OutageStart? OpenOutage { get; init; }

    public IReadOnlyList<string> PendingEventLines { get; init; } = [];

    public IReadOnlyList<string> PendingSessionLines { get; init; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static RunState? FromJson(string json) => JsonSerializer.Deserialize<RunState>(json, JsonOptions);
}
