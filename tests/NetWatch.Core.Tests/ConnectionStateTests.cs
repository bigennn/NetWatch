using NetWatch.Core;

namespace NetWatch.Core.Tests;

public class ConnectionStateTests
{
    [Fact]
    public void Default_value_is_Paused_so_an_uninitialized_state_shows_grey()
    {
        ConnectionState state = default;

        Assert.Equal(ConnectionState.Paused, state);
    }
}
