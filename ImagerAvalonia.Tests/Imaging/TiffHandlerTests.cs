using ImagerAvalonia.Utils;
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
    public void Histogram_bins_every_pixel_including_the_maximum()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(0, 0, 128, 256, 256, 256), ImagePixelFormat.Mono16, ref y, ref yLow, ref x);

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

        TiffHandler.UpdateHistogramValues(To16Bit(1, 2), ImagePixelFormat.Mono16, ref y, ref yLow, ref x);

        Assert.Equal(2, y.Sum());
        Assert.All(yLow, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Histogram_of_an_empty_image_is_empty()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(Array.Empty<byte>(), ImagePixelFormat.Mono16, ref y, ref yLow, ref x);

        Assert.Equal(0, y.Sum());
    }

    [Fact]
    public void Histogram_of_a_flat_image_puts_everything_in_the_first_bin()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(500, 500, 500), ImagePixelFormat.Mono16, ref y, ref yLow, ref x);

        Assert.Equal(3, y[0]);
    }

    [Fact]
    public void Histogram_of_a_saturated_flat_image_puts_everything_in_the_last_bin()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(To16Bit(65535, 65535, 65535), ImagePixelFormat.Mono16, ref y, ref yLow, ref x);

        Assert.Equal(3, y[256]);
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
    public void Bytes_per_pixel_matches_the_wire_format()
    {
        Assert.Equal(1, ImagePixelFormat.Mono8.BytesPerPixel());
        Assert.Equal(2, ImagePixelFormat.Mono16.BytesPerPixel());
        Assert.Equal(8, ImagePixelFormat.Float64.BytesPerPixel());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ImagePixelFormat)99).BytesPerPixel());
    }

    [Fact]
    public void Normalize_mono8_replicates_bits_so_255_maps_to_65535()
    {
        Assert.Equal(new ushort[] { 0x0000, 0x8080, 0xFFFF },
            TiffHandler.NormalizeToUInt16(new byte[] { 0, 128, 255 }, ImagePixelFormat.Mono8));
    }

    [Fact]
    public void Normalize_mono16_passes_pixels_through()
    {
        Assert.Equal(new ushort[] { 1, 256, 65535 },
            TiffHandler.NormalizeToUInt16(To16Bit(1, 256, 65535), ImagePixelFormat.Mono16));
    }

    [Fact]
    public void Normalize_float64_clamps_to_the_16bit_range()
    {
        var bytes = new byte[3 * 8];
        Buffer.BlockCopy(new[] { -1.5, 0.5, 70000.0 }, 0, bytes, 0, bytes.Length);

        Assert.Equal(new ushort[] { 0, 0, 65535 },
            TiffHandler.NormalizeToUInt16(bytes, ImagePixelFormat.Float64));
    }

    [Fact]
    public void Normalize_unknown_format_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TiffHandler.NormalizeToUInt16(new byte[] { 1, 2 }, (ImagePixelFormat)99));
    }

    [Fact]
    public void Normalize_to_mono16_bytes_converts_mono8_to_two_bytes_per_pixel()
    {
        var result = TiffHandler.NormalizeToMono16Bytes(new byte[] { 0, 255 }, ImagePixelFormat.Mono8);

        Assert.Equal(new ushort[] { 0x0000, 0xFFFF }, From16Bit(result));
    }

    [Fact]
    public void Histogram_bins_mono8_pixels_over_the_full_16bit_range()
    {
        var y = new double[257];
        var yLow = new double[257];
        var x = new double[257];

        TiffHandler.UpdateHistogramValues(new byte[] { 0, 0, 128, 255, 255, 255 }, ImagePixelFormat.Mono8, ref y, ref yLow, ref x);

        Assert.Equal(6, y.Sum());
        Assert.Equal(2, y[0]);
        Assert.Equal(1, y[128]);
        Assert.Equal(3, y[256]);
    }

    [Fact]
    public void Stretched_value_saturates_at_the_limits_of_a_normal_range()
    {
        // min=12850, max=64250 on the 16-bit display scale
        double scale = (double)ushort.MaxValue / (64250 - 12850);

        Assert.Equal((ushort)0, TiffHandler.StretchedValue(12850, 12850, 64250, scale));
        Assert.Equal(ushort.MaxValue, TiffHandler.StretchedValue(64250, 12850, 64250, scale));
        Assert.Equal((ushort)32767, TiffHandler.StretchedValue(38550, 12850, 64250, scale));
    }

    [Fact]
    public void Stretched_value_inverts_the_lut_when_min_is_above_max()
    {
        // Inverted range: min=64250, max=12850, negative scale
        double scale = (double)ushort.MaxValue / (12850 - 64250);

        Assert.Equal(ushort.MaxValue, TiffHandler.StretchedValue(12850, 64250, 12850, scale));
        Assert.Equal((ushort)0, TiffHandler.StretchedValue(64250, 64250, 12850, scale));
        Assert.Equal((ushort)32767, TiffHandler.StretchedValue(38550, 64250, 12850, scale));
    }

    [Fact]
    public void Stretched_gray_inverts_the_lut_when_min_is_above_max()
    {
        // Inverted range: min=64250, max=12850, negative scale. Float64 pixels
        // are stretched as raw values against the display-scale limits.
        double scale = (double)ushort.MaxValue / (12850 - 64250);

        Assert.Equal(uint.MaxValue, TiffHandler.StretchedGray(12850.0, 64250, 12850, scale));
        Assert.Equal(0xFF000000u, TiffHandler.StretchedGray(64250.0, 64250, 12850, scale));
        Assert.Equal(0xFF7F7F7Fu, TiffHandler.StretchedGray(38550.0, 64250, 12850, scale));
    }

    [Fact]
    public void Display_scale_limits_bit_replicate_mono8_limits_and_clamp_to_the_byte_range()
    {
        Assert.Equal((0, 65535), TiffHandler.ToDisplayScaleLimits(0, 255, ImagePixelFormat.Mono8));
        Assert.Equal((25700, 51400), TiffHandler.ToDisplayScaleLimits(100, 200, ImagePixelFormat.Mono8));
        Assert.Equal((65535, 65535), TiffHandler.ToDisplayScaleLimits(300, 1000, ImagePixelFormat.Mono8));
    }

    [Fact]
    public void Display_scale_limits_pass_non_mono8_limits_through_unchanged()
    {
        Assert.Equal((12850, 64250), TiffHandler.ToDisplayScaleLimits(12850, 64250, ImagePixelFormat.Mono16));
        Assert.Equal((-5, 70000), TiffHandler.ToDisplayScaleLimits(-5, 70000, ImagePixelFormat.Float64));
    }
}
