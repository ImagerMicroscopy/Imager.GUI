using ImagerAvalonia.Views.ViewUtils;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

public class TiffHandlerTests
{
    private static byte[] To16Bit(params ushort[] pixels)
    {
        var bytes = new byte[pixels.Length * 2];
        Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static ushort[] From16Bit(byte[] bytes)
    {
        var pixels = new ushort[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, pixels, 0, bytes.Length);
        return pixels;
    }

    [Fact]
    public void Auto_contrast_8bit_stretches_to_full_range_including_the_extremes()
    {
        var result = TiffHandler.UpdateAutoContrast8Bit(new byte[] { 50, 100, 150 });

        Assert.Equal(new byte[] { 0, 127, 255 }, result);
    }

    [Fact]
    public void Auto_contrast_8bit_on_a_flat_image_does_not_throw()
    {
        var result = TiffHandler.UpdateAutoContrast8Bit(new byte[] { 7, 7, 7 });

        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void Contrast_min_max_16bit_clips_and_stretches()
    {
        var result = From16Bit(TiffHandler.UpdateContrastMinMaxIn16Bit(To16Bit(0, 100, 150, 200, 1000), 100, 200));

        Assert.Equal(new ushort[] { 0, 0, 32767, ushort.MaxValue, ushort.MaxValue }, result);
    }

    [Fact]
    public void Contrast_min_max_16bit_does_not_modify_the_input()
    {
        var input = To16Bit(10, 20, 30);
        var copy = (byte[])input.Clone();

        TiffHandler.UpdateContrastMinMaxIn16Bit(input, 10, 30);

        Assert.Equal(copy, input);
    }

    [Fact]
    public void Contrast_min_max_16bit_with_equal_min_and_max_is_a_threshold()
    {
        var result = From16Bit(TiffHandler.UpdateContrastMinMaxIn16Bit(To16Bit(5, 10, 15), 10, 10));

        Assert.Equal(new ushort[] { 0, 0, ushort.MaxValue }, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Contrast_and_histogram_values_maps_the_live_image(bool autocontrast)
    {
        var result = From16Bit(TiffHandler.UpdateContrastAndHistogramValues(To16Bit(100, 150, 200), 100, 200, autocontrast));

        Assert.Equal(new ushort[] { 0, 32767, ushort.MaxValue }, result);
    }

    [Fact]
    public void Contrast_and_histogram_values_on_a_flat_image_does_not_divide_by_zero()
    {
        var result = From16Bit(TiffHandler.UpdateContrastAndHistogramValues(To16Bit(42, 42), 0, 0, autocontrast: true));

        Assert.Equal(new ushort[] { 0, 0 }, result);
    }

    [Fact]
    public void Histogram_bins_every_pixel_including_the_maximum()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(0, 0, 128, 256, 256, 256), ref y, ref yLow, ref x);

        Assert.Equal(6, y.Sum());
        Assert.Equal(2, y[0]);
        Assert.Equal(1, y[128]);
        Assert.Equal(3, y[256]);
        Assert.Equal(0, x[0]);
        Assert.Equal(255, x[255]);
    }

    [Fact]
    public void Histogram_is_reset_between_calls()
    {
        var y = Enumerable.Repeat(99.0, 257).ToArray();
        var yLow = Enumerable.Repeat(99.0, 257).ToArray();
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(1, 2), ref y, ref yLow, ref x);

        Assert.Equal(2, y.Sum());
        Assert.All(yLow, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Histogram_of_an_empty_image_is_empty()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(Array.Empty<byte>(), ref y, ref yLow, ref x);

        Assert.Equal(0, y.Sum());
    }

    [Fact]
    public void Histogram_of_a_flat_image_puts_everything_in_the_first_bin()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(500, 500, 500), ref y, ref yLow, ref x);

        Assert.Equal(3, y[0]);
    }

    [Fact]
    public void Max_intensity_projection_takes_the_brighter_pixel()
    {
        Assert.Equal(new byte[] { 5, 9, 3 },
            TiffHandler.MaxIntensityProject8Bit(new byte[] { 5, 1, 3 }, new byte[] { 2, 9, 3 }));
    }

    [Fact]
    public void Subsampling_takes_every_eighth_pixel_high_byte()
    {
        const int width = 16, height = 16;
        var pixels = new ushort[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = (ushort)((y * width + x) << 8);

        var result = TiffHandler.GetSubsampledImage(To16Bit(pixels), width, height);

        Assert.Equal(new byte[] { 0, 8, 128, 136 }, result);
    }

    [Fact]
    public void Convert_16bit_to_8bit_keeps_the_high_byte_and_rejects_odd_lengths()
    {
        Assert.Equal(new byte[] { 0x12, 0xFF }, TiffHandler.Convert16BitTo8BitFast(To16Bit(0x12AB, 0xFF00)));
        Assert.Throws<ArgumentException>(() => TiffHandler.Convert16BitTo8BitFast(new byte[3]));
    }
}
