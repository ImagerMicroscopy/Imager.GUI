using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

public class ImageHandlerDisplayTests
{
    private static ImageHandler Handler() =>
        new(Mock.Of<IStorageProvider>(), Mock.Of<ILogger>(), Mock.Of<IImagerConnectionHandler>());

    private static ImageData Batch() => new()
    {
        Images = new List<byte[]> { new byte[] { 1, 2, 3, 4 } }
    };

    [Fact]
    public void Every_batch_is_forwarded_to_the_display_when_live_view_is_enabled()
    {
        var handler = Handler();
        var displayUpdates = 0;
        handler.UpdateImageDisplay += (_, _) => displayUpdates++;

        handler.NewImageDataAvailable(Batch(), true);
        handler.NewImageDataAvailable(Batch(), true);
        handler.NewImageDataAvailable(Batch(), true);

        Assert.Equal(3, displayUpdates);
    }

    [Fact]
    public void No_display_update_when_live_view_is_disabled()
    {
        var handler = Handler();
        var displayUpdates = 0;
        handler.UpdateImageDisplay += (_, _) => displayUpdates++;

        handler.NewImageDataAvailable(Batch(), false);

        Assert.Equal(0, displayUpdates);
    }

    [Fact]
    public void Empty_batches_are_ignored()
    {
        var handler = Handler();
        var displayUpdates = 0;
        handler.UpdateImageDisplay += (_, _) => displayUpdates++;

        handler.NewImageDataAvailable(new ImageData(), true);

        Assert.Equal(0, displayUpdates);
    }
}
