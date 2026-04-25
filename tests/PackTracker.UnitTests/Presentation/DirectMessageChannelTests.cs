using PackTracker.Presentation.Services;

namespace PackTracker.UnitTests.Presentation;

public class DirectMessageChannelTests
{
    [Fact]
    public void BuildCanonical_SortsUsernamesIntoStableDmChannel()
    {
        var channel = DirectMessageChannel.BuildCanonical("Sentinel_Wolf", "zombierecon");

        Assert.Equal("dm:sentinel_wolf:zombierecon", channel);
    }

    [Fact]
    public void BuildFallback_UsesCanonicalChannel_WhenCurrentUsernameIsKnown()
    {
        var channel = DirectMessageChannel.BuildFallback("sentinel_wolf", "zombierecon");

        Assert.Equal("dm:sentinel_wolf:zombierecon", channel);
    }

    [Fact]
    public void TryGetCounterpart_HandlesLegacyDirectChannel()
    {
        var success = DirectMessageChannel.TryGetCounterpart("direct:zombierecon", "sentinel_wolf", out var counterpart);

        Assert.True(success);
        Assert.Equal("zombierecon", counterpart);
    }

    [Fact]
    public void TryGetCounterpart_HandlesCanonicalDmChannel()
    {
        var success = DirectMessageChannel.TryGetCounterpart("dm:sentinel_wolf:zombierecon", "sentinel_wolf", out var counterpart);

        Assert.True(success);
        Assert.Equal("zombierecon", counterpart);
    }
}
