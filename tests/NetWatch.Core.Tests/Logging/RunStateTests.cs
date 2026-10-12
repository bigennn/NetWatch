using NetWatch.Core.Detection;
using NetWatch.Core.Logging;

namespace NetWatch.Core.Tests.Logging;

public class RunStateTests
{
    [Fact]
    public void State_survives_saving_and_loading_as_json()
    {
        var noon = new DateTimeOffset(2026, 10, 13, 12, 0, 0, 123, TimeSpan.Zero);
        var state = new RunState
        {
            Session = new RunningSession(noon, SessionReasons.Resume, [SessionNotes.GatewayIgnoresPing]),
            LastHeartbeatUtc = noon.AddMinutes(5),
            OpenOutage = new OutageStart(noon.AddMinutes(4), OutageCause.PcLan, null, ["1.1.1.1", "8.8.8.8"]),
            PendingEventLines = ["line 1"],
            PendingSessionLines = ["line 2"],
        };

        RunState loaded = RunState.FromJson(state.ToJson())!;

        Assert.Equal(state.LastHeartbeatUtc, loaded.LastHeartbeatUtc);
        Assert.Equal(state.Session.StartUtc, loaded.Session!.StartUtc);
        Assert.Equal(state.Session.StartReason, loaded.Session.StartReason);
        Assert.Equal(state.Session.Notes, loaded.Session.Notes);
        Assert.Equal(state.OpenOutage.StartUtc, loaded.OpenOutage!.StartUtc);
        Assert.Equal(OutageCause.PcLan, loaded.OpenOutage.Cause);
        Assert.Null(loaded.OpenOutage.Gateway);
        Assert.Equal(state.OpenOutage.FailedTargets, loaded.OpenOutage.FailedTargets);
        Assert.Equal(state.PendingEventLines, loaded.PendingEventLines);
        Assert.Equal(state.PendingSessionLines, loaded.PendingSessionLines);
    }

    [Fact]
    public void Cause_is_stored_as_readable_text()
    {
        var state = new RunState
        {
            LastHeartbeatUtc = DateTimeOffset.UnixEpoch,
            OpenOutage = new OutageStart(DateTimeOffset.UnixEpoch, OutageCause.Router, "192.168.178.1", []),
        };

        Assert.Contains("\"Router\"", state.ToJson());
    }
}
