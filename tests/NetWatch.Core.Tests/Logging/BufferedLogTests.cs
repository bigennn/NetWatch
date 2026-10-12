using NetWatch.Core.Logging;

namespace NetWatch.Core.Tests.Logging;

public class BufferedLogTests
{
    [Fact]
    public void Lines_are_written_immediately_when_the_file_is_free()
    {
        var sink = new FakeSink();
        var log = new BufferedLog(sink);

        log.Add("a");
        log.Add("b");

        Assert.Equal(new[] { "a", "b" }, sink.Lines);
        Assert.Empty(log.Pending);
    }

    [Fact]
    public void Locked_file_buffers_lines_and_writes_each_exactly_once_in_order_after_unlock()
    {
        var sink = new FakeSink { Locked = true };
        var log = new BufferedLog(sink);

        log.Add("a");
        log.Add("b");
        Assert.False(log.Flush());
        log.Add("c");

        Assert.Empty(sink.Lines);
        Assert.Equal(new[] { "a", "b", "c" }, log.Pending);
        Assert.True(sink.FailedAttempts >= 3);

        sink.Locked = false;
        Assert.True(log.Flush());
        Assert.True(log.Flush());

        Assert.Equal(new[] { "a", "b", "c" }, sink.Lines);
        Assert.Empty(log.Pending);
    }
}
