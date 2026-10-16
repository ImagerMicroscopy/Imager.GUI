using ImagerAvalonia.Views.MultiChannelView;
using Xunit;

namespace ImagerAvalonia.Tests.Views;

/// <summary>
/// The multi-channel frame buffer: the latest frame per channel, handed out for
/// compositing only once every enabled channel has one. Buffers are per instance
/// on purpose — the static buffer this replaces blended frames across
/// simultaneously active multi-channel views (e.g. the live tab and a running
/// measurement tab).
/// </summary>
public sealed class ChannelFrameBufferTests
{
    private static byte[] Frame(byte marker) => new[] { marker };

    [Fact]
    public void Waits_until_every_enabled_channel_has_a_frame()
    {
        var buffer = new ChannelFrameBuffer();
        var enabled = new[] { 0, 1, 2 };

        buffer.Add(0, Frame(1));
        buffer.Add(2, Frame(3));

        Assert.False(buffer.TryTakeCompleted(enabled, out _));

        buffer.Add(1, Frame(2));

        Assert.True(buffer.TryTakeCompleted(enabled, out var frames));
        Assert.Equal(new byte[] { 1 }, frames[0]);
        Assert.Equal(new byte[] { 2 }, frames[1]);
        Assert.Equal(new byte[] { 3 }, frames[2]);
    }

    [Fact]
    public void The_latest_frame_per_channel_wins()
    {
        var buffer = new ChannelFrameBuffer();

        buffer.Add(0, Frame(1));
        buffer.Add(0, Frame(2)); // a newer frame replaces the pending older one

        Assert.True(buffer.TryTakeCompleted(new[] { 0 }, out var frames));
        Assert.Equal(new byte[] { 2 }, frames[0]);
    }

    [Fact]
    public void Taking_a_completed_composite_clears_the_buffer()
    {
        var buffer = new ChannelFrameBuffer();

        buffer.Add(0, Frame(1));
        Assert.True(buffer.TryTakeCompleted(new[] { 0 }, out _));

        // The next composite waits for fresh frames of every channel again, so a
        // completed composite can never be built partly from the previous one's.
        buffer.Add(1, Frame(9));
        Assert.False(buffer.TryTakeCompleted(new[] { 0, 1 }, out _));
    }

    [Fact]
    public void Disabled_channels_do_not_hold_up_the_composite()
    {
        var buffer = new ChannelFrameBuffer();

        // Channel 4 has no frame, but it is not enabled either.
        buffer.Add(0, Frame(1));

        Assert.True(buffer.TryTakeCompleted(new[] { 0 }, out var frames));
        Assert.Null(frames[4]);
    }

    /// <summary>The regression the instance buffers exist for: two simultaneously
    /// active views interleave their channels' frames without ever seeing each
    /// other's.</summary>
    [Fact]
    public void Two_buffers_never_share_frames()
    {
        var viewA = new ChannelFrameBuffer();
        var viewB = new ChannelFrameBuffer();

        viewA.Add(0, Frame(0xA1)); // A's red channel
        viewB.Add(1, Frame(0xB1)); // B's green channel

        // A's buffer holds only A's red frame, B's only B's green frame.
        Assert.Equal(new byte[] { 0xA1 }, viewA.Frames[0]);
        Assert.Null(viewA.Frames[1]);
        Assert.Null(viewB.Frames[0]);
        Assert.Equal(new byte[] { 0xB1 }, viewB.Frames[1]);

        // Each view completes its own composite from its own frames only.
        Assert.False(viewA.TryTakeCompleted(new[] { 0, 1 }, out _));
        viewA.Add(1, Frame(0xA2));
        Assert.True(viewA.TryTakeCompleted(new[] { 0, 1 }, out var compositeA));
        Assert.Equal(new byte[] { 0xA1 }, compositeA[0]);
        Assert.Equal(new byte[] { 0xA2 }, compositeA[1]);

        Assert.False(viewB.TryTakeCompleted(new[] { 0, 1 }, out _));
        viewB.Add(0, Frame(0xB2));
        Assert.True(viewB.TryTakeCompleted(new[] { 0, 1 }, out var compositeB));
        Assert.Equal(new byte[] { 0xB2 }, compositeB[0]);
        Assert.Equal(new byte[] { 0xB1 }, compositeB[1]);
    }
}
