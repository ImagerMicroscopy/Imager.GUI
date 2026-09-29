using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests.Workspace;

public class AcquisitionHandlerTests
{
    private static MessagePackAcquisitionHandler Handler() =>
        new(Mock.Of<IStorageProvider>(), Mock.Of<IImagerConnectionHandler>());

    private static ChannelMessage Image(ulong index, string detector = "DummyCam1", string? positionName = "p1") =>
        new(index, new AsyncMeasurementMessage("acquireddatamessage",
            new AcquiredData(detector, new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 1, 0, 2.5f),
            new AcquisitionMetaData("Acq1", 4, "elem-1", 1, new StagePosition(0.25, true, 1, 2, 3), positionName!),
            null!));

    private static ChannelMessage Decision(ulong index, string? payload) =>
        new(index, new AsyncMeasurementMessage("smartprogramdecisionmessage", null!, null!, payload!));

    [Fact]
    public void Image_messages_become_planes_with_metadata()
    {
        var data = Handler().ProcessMessages(new[] { Image(0), Image(1, "DummyCam2") });

        Assert.Equal(2, data.Images.Count);
        Assert.Equal(new[] { new List<uint> { 3, 1 }, new List<uint> { 3, 1 } }, data.Sizes);

        var meta = data.Metadata[0];
        Assert.Equal("Acq1", meta.AcquisitionName);
        Assert.Equal("DummyCam1", meta.DetectorName);
        Assert.Equal(3u, meta.Width);
        Assert.Equal(1u, meta.Height);
        Assert.Equal(2.5f, meta.TimePoint);
        Assert.Equal(4, meta.DetectionIndex);
        Assert.Equal("elem-1", meta.ElementID);
        Assert.Equal("p1", meta.PositionName);
        Assert.Equal((1.0, 2.0, 3.0), (meta.PositionX, meta.PositionY, meta.PositionZ));
        Assert.True(meta.CurrentStagePosition.Coordinates.usinghardwareautofocus);
        Assert.Equal(0.25, meta.CurrentStagePosition.Coordinates.hardwareautofocusoffset);

        Assert.Equal("DummyCam2", data.Metadata[1].DetectorName);
    }

    [Fact]
    public void Missing_position_name_becomes_empty()
    {
        var data = Handler().ProcessMessages(new[] { Image(0, positionName: null) });

        Assert.Equal("", data.Metadata[0].PositionName);
    }

    [Fact]
    public void Decision_messages_are_collected_without_producing_planes()
    {
        var data = Handler().ProcessMessages(new[] { Image(0), Decision(1, "{\"decision\":\"stop\"}"), Decision(2, null) });

        Assert.Single(data.Images);
        Assert.Equal(new[] { "{\"decision\":\"stop\"}", "" }, data.Decisions);
        Assert.Equal(new[] { "acquireddatamessage", "smartprogramdecisionmessage", "smartprogramdecisionmessage" },
            data.ImageResponseType);
    }

    [Fact]
    public void Empty_batch_produces_nothing()
    {
        var data = Handler().ProcessMessages(Array.Empty<ChannelMessage>());

        Assert.Empty(data.Images);
        Assert.Empty(data.Metadata);
    }
}
