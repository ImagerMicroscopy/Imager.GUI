using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

public class ImageUpdateCoalescerTests
{
    private static ImageUpdateArguments Update(string acq, string det, string elementId, byte marker) =>
        new(2, 2, acq, det, elementId, new byte[] { marker }, ImagePixelFormat.Mono8,
            new XYStagePosition(0, 0, 0, 0, false, "pos"), true, 0.0);

    [Fact]
    public void Only_the_newest_update_per_display_slot_survives()
    {
        var updates = new[]
        {
            Update("Acq1", "Cam1", "elem-1", 1),
            Update("Acq1", "Cam1", "elem-1", 2),
            Update("Acq1", "Cam2", "elem-1", 3),
            Update("Acq1", "Cam1", "elem-1", 4),
        };

        var kept = ImageUpdateCoalescer.KeepLatestPerSlot(updates);

        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, u => u.Detector == "Cam1" && u.ImageData[0] == 4);
        Assert.Contains(kept, u => u.Detector == "Cam2" && u.ImageData[0] == 3);
    }

    [Fact]
    public void Same_detector_with_different_elements_is_not_coalesced()
    {
        var kept = ImageUpdateCoalescer.KeepLatestPerSlot(new[]
        {
            Update("Acq1", "Cam1", "elem-1", 1),
            Update("Acq1", "Cam1", "elem-2", 2),
        });

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void Different_acquisitions_are_separate_slots()
    {
        var kept = ImageUpdateCoalescer.KeepLatestPerSlot(new[]
        {
            Update("Acq1", "Cam1", "elem-1", 1),
            Update("Acq2", "Cam1", "elem-1", 2),
        });

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void Empty_input_produces_no_updates()
    {
        Assert.Empty(ImageUpdateCoalescer.KeepLatestPerSlot(Array.Empty<ImageUpdateArguments>()));
    }
}
