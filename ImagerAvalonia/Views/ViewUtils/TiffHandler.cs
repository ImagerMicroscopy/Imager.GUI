using Avalonia;
using Avalonia.Media.Imaging;
using ImagerAvalonia.Utils;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;



namespace ImagerAvalonia.Views.ViewUtils
{



    public class TiffPlane
    {
        public byte[]? bytearray;
        public int Width;
        public int Height;
        public int Stride;
        
         
        public TiffPlane()
        {
        }
        public TiffPlane(byte[]? bytearray, int width, int height, int stride)
        {
            this.bytearray = bytearray;
            Width = width;
            Height = height;
            Stride = stride;
        }   
    }



    internal class TiffHandler
    {

        public static byte[] Convert16BitTo8BitFast(byte[] byteArray16Bit)
        {
            if (byteArray16Bit.Length % 2 != 0)
                throw new ArgumentException("The byte array length must be even.");

            int length = byteArray16Bit.Length / 2;
            byte[] byteArray8Bit = new byte[length];

            unsafe
            {
                fixed (byte* source = byteArray16Bit, dest = byteArray8Bit)
                {
                    for (int i = 0; i < length; i++)
                    {
                        dest[i] = source[i * 2 + 1]; // Copy the most significant byte
                    }
                }
            }

            return byteArray8Bit;
        }


        public static byte[] UpdateAutoContrast8Bit(byte[] image_array)
        {
            double min_image_val = image_array.Min();
            double max_image_val = image_array.Max();

            byte min_val = Convert.ToByte(min_image_val);
            byte max_val = Convert.ToByte(max_image_val);

            if (max_val == min_val)
            {
                byte flatValue = max_val == byte.MaxValue ? byte.MaxValue : (byte)0;

                for (int i = 0; i < image_array.Length; i++)
                {
                    image_array[i] = flatValue;
                }

                return image_array;
            }

            unsafe
            {
                fixed (byte* ptr = image_array)
                {
                    for (int i = 0; i < image_array.Length; i++)
                    {
                        if (ptr[i] <= min_val)
                        {
                            ptr[i] = 0;
                        }
                        else if (ptr[i] >= max_val)
                        {
                            ptr[i] = byte.MaxValue;
                        }
                        else
                        {
                            ptr[i] = (byte)((float)((ptr[i] - min_val)) / (max_val - min_val) * byte.MaxValue);
                        }
                    }
                }
            }
            return image_array;
        }


        public static byte[] UpdateContrastMinMaxIn16Bit(byte[] image_array, int min_val, int max_val)
        {
            ushort[] converted_array = new ushort[image_array.Length / 2];
            byte[] contrast_adjusted = new byte[image_array.Length];
            Buffer.BlockCopy(image_array, 0, converted_array, 0, image_array.Length);

            if (min_val == max_val)
            {
                ushort flatValue = max_val == ushort.MaxValue ? ushort.MaxValue : (ushort)0;

                for (int i = 0; i < converted_array.Length; i++)
                {
                    converted_array[i] = flatValue;
                }

                Buffer.BlockCopy(converted_array, 0, contrast_adjusted, 0, converted_array.Length * 2);
                return contrast_adjusted;
            }

            unsafe
            {
                fixed (ushort* ptr = converted_array)
                {
                    for (int i = 0; i < converted_array.Length; i++)
                    {
                        if (ptr[i] <= min_val)
                        {
                            ptr[i] = 0;
                        }
                        else if (ptr[i] >= max_val)
                        {
                            ptr[i] = ushort.MaxValue;
                        }
                        else
                        {
                            ptr[i] = (ushort)((float)((ptr[i] - min_val)) / (max_val - min_val) * ushort.MaxValue);
                        }
                    }
                }
            }
            Buffer.BlockCopy(converted_array, 0, contrast_adjusted, 0, converted_array.Length * 2);
            return contrast_adjusted;
        }


        public static byte[] UpdateAutoContrast16Bit(byte[] image_array)
        {
            ushort[] converted_array = new ushort[image_array.Length / 2];
            Buffer.BlockCopy(image_array, 0, converted_array, 0, image_array.Length);

            double min_image_val = converted_array.Min();
            double max_image_val = converted_array.Max();

            ushort min_val = Convert.ToUInt16(min_image_val);
            ushort max_val = Convert.ToUInt16(max_image_val);

            if (min_val == max_val)
            {
                ushort flatValue = max_val == ushort.MaxValue ? ushort.MaxValue : (ushort)0;

                for (int i = 0; i < converted_array.Length; i++)
                {
                    converted_array[i] = flatValue;
                }

                Buffer.BlockCopy(converted_array, 0, image_array, 0, converted_array.Length * 2);
                return image_array;
            }

            unsafe
            {
                fixed (ushort* ptr = converted_array)
                {
                    for (int i = 0; i < converted_array.Length; i++)
                    {
                        if (ptr[i] < max_val && ptr[i] > min_val)
                        {
                            ptr[i] = (ushort)((float)((ptr[i] - min_val)) / (max_val - min_val) * ushort.MaxValue);
                        }
                        else if (ptr[i] < min_val)
                        {
                            ptr[i] = 0;
                        }
                        else if (ptr[i] > max_val)
                        {
                            ptr[i] = ushort.MaxValue;
                        }
                    }
                }
            }
            Buffer.BlockCopy(converted_array, 0, image_array, 0, converted_array.Length * 2);
            return image_array; 
        }

        public static byte[] MaxIntensityProject8Bit(byte[] first_img, byte[] second_img)
        {
            byte[] max_projected = new byte[first_img.Length];
            unsafe
            {
                fixed (byte* first_img_ptr = first_img, second_img_ptr = second_img, max_img_ptr = max_projected)
                {
                    for (int i = 0; i < max_projected.Length; i++)
                    {
                        max_projected[i] = Math.Max(first_img_ptr[i], second_img_ptr[i]);
                    }
                }
            }
            return max_projected;   
        }


        public static byte[] GetSubsampledImage(byte[] image, int width, int height)
        {
            int newWidth = width / 8;
            int newHeight = height / 8;
            byte[] result = new byte[newWidth * newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int index = (y * 8 * width + x * 8) * 2 + 1;
                    result[y * newWidth + x] = image[index];
                }
            }

            return result;
        }

        public static void UpdateHistogramValues(byte[] image_array, ImagePixelFormat format, ref double[] histogram_vals_y, ref double[] histogram_vals_y_low, ref double[] histogram_vals_x  )
        {
            Array.Clear(histogram_vals_y);
            Array.Clear(histogram_vals_y_low);

            // Pixels are read straight from the raw bytes in their native format.
            // Binning is relative to the image's own min/max, so byte, ushort and
            // double pixels are all binned by the same generic pass: value minus
            // min, divided by bin size.
            var bytes = image_array.AsSpan();
            int pixelCount = bytes.Length / format.BytesPerPixel();
            if (pixelCount == 0)
                return;

            double min_image_val;
            double max_image_val;

            switch (format)
            {
                case ImagePixelFormat.Mono8:
                    (min_image_val, max_image_val) = MinMaxOver(bytes);
                    break;
                case ImagePixelFormat.Mono16:
                    (min_image_val, max_image_val) = MinMaxOver(MemoryMarshal.Cast<byte, ushort>(bytes.Slice(0, pixelCount * 2)));
                    break;
                case ImagePixelFormat.Float64:
                    (min_image_val, max_image_val) = MinMaxOver(MemoryMarshal.Cast<byte, double>(bytes.Slice(0, pixelCount * 8)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format.");
            }

            if (max_image_val == min_image_val)
            {
                // Flat frame: mirror the display's flat policy. A saturated
                // frame renders white and spikes at the bright end; anything
                // else renders black and spikes at the first bin.
                bool saturated = ToDisplayScale(min_image_val, format) == ushort.MaxValue;
                int bin_position = saturated ? histogram_vals_x.Length - 1 : 0;
                histogram_vals_y[bin_position] = pixelCount;

                for (int hist_val = 0; hist_val < histogram_vals_x.Length; hist_val++)
                {
                    histogram_vals_x[hist_val] = hist_val;
                }

                return;
            }

            double bin_size = (max_image_val - min_image_val) / 256;

            for (int hist_val = 0; hist_val < histogram_vals_x.Length; hist_val++)
            {
                histogram_vals_x[hist_val] = min_image_val + hist_val * bin_size;
            }

            switch (format)
            {
                case ImagePixelFormat.Mono8:
                    BinPixels(bytes, histogram_vals_y, min_image_val, bin_size);
                    break;
                case ImagePixelFormat.Mono16:
                    BinPixels(MemoryMarshal.Cast<byte, ushort>(bytes.Slice(0, pixelCount * 2)), histogram_vals_y, min_image_val, bin_size);
                    break;
                case ImagePixelFormat.Float64:
                    BinPixels(MemoryMarshal.Cast<byte, double>(bytes.Slice(0, pixelCount * 8)), histogram_vals_y, min_image_val, bin_size);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format.");
            }
        }

        /// <summary>
        /// Min and max of the pixel values, in the pixels' native numeric range.
        /// </summary>
        private static (double Min, double Max) MinMaxOver<T>(ReadOnlySpan<T> pixels)
            where T : unmanaged, System.Numerics.INumber<T>
        {
            T min = pixels[0];
            T max = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                T val = pixels[i];
                if (val < min) min = val;
                if (val > max) max = val;
            }
            return (double.CreateChecked(min), double.CreateChecked(max));
        }

        private static void BinPixels<T>(ReadOnlySpan<T> pixels, double[] histogram_vals_y, double min_image_val, double bin_size)
            where T : unmanaged, System.Numerics.INumber<T>
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                double bin_value = double.CreateChecked(pixels[i]) - min_image_val;
                int bin_position = bin_value != 0 ? (int)(bin_value / bin_size) : 0;
                histogram_vals_y[bin_position] += 1;
            }
        }

        /// <summary>
        /// Converts a pixel value from its native format range onto the 16-bit
        /// display scale: Mono8 is bit-replicated (byte * 257), Float64 saturates
        /// at the scale's endpoints so a fully saturated frame reports 65535 and
        /// renders white rather than black in the flat-image branch.
        /// </summary>
        private static int ToDisplayScale(double value, ImagePixelFormat format) => format switch
        {
            ImagePixelFormat.Mono8 => (int)(value * 257),
            ImagePixelFormat.Mono16 => (int)value,
            ImagePixelFormat.Float64 => value <= 0 ? 0 : value >= ushort.MaxValue ? ushort.MaxValue : (ushort)value,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format."),
        };

        public static byte[] UpdateContrastAndHistogramValues(byte[] image_array, ushort min_val, ushort max_val, bool autocontrast)
        {
            int pixelCount = image_array.Length / 2;
            ushort[] converted_array = new ushort[pixelCount];

            Buffer.BlockCopy(image_array, 0, converted_array, 0, image_array.Length);

            if (autocontrast)
            {
                ushort localMin = ushort.MaxValue;
                ushort localMax = ushort.MinValue;

                for (int i = 0; i < pixelCount; i++)
                {
                    ushort val = converted_array[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }

                min_val = localMin;
                max_val = localMax;
            }

            if (min_val == max_val)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    converted_array[i] = converted_array[i] == 0 ? (ushort)0 : ushort.MaxValue;
                }

                Buffer.BlockCopy(converted_array, 0, image_array, 0, image_array.Length);
                return image_array;
            }

            double range = max_val - min_val;
            double scale = (double)ushort.MaxValue / range;

            unsafe
            {
                fixed (ushort* ptr = converted_array)
                {
                    ushort* p = ptr;
                    for (int i = 0; i < pixelCount; i++, p++)
                    {
                        ushort val = *p;

                        if (val <= min_val)
                        {
                            *p = 0;
                        }
                        else if (val >= max_val)
                        {
                            *p = ushort.MaxValue;
                        }
                        else
                        {
                            *p = (ushort)((val - min_val) * scale);
                        }
                    }
                }
            }

            Buffer.BlockCopy(converted_array, 0, image_array, 0, image_array.Length);
            return image_array;
        }


        /// <summary>
        /// Normalizes raw image bytes of any supported pixel format to a 16-bit pixel
        /// array. Mono8 values are bit-replicated so 255 maps to
        /// 65535; Float64 values are clamped to the 16-bit range. Unknown formats throw.
        /// </summary>
        public static ushort[] NormalizeToUInt16(byte[] image, ImagePixelFormat format)
        {
            switch (format)
            {
                case ImagePixelFormat.Mono8:
                {
                    var pixels = new ushort[image.Length];
                    for (int i = 0; i < image.Length; i++)
                        pixels[i] = (ushort)(image[i] << 8 | image[i]);
                    return pixels;
                }
                case ImagePixelFormat.Mono16:
                {
                    var pixels = new ushort[image.Length / 2];
                    Buffer.BlockCopy(image, 0, pixels, 0, pixels.Length * 2);
                    return pixels;
                }
                case ImagePixelFormat.Float64:
                {
                    var doubles = MemoryMarshal.Cast<byte, double>(image);
                    var pixels = new ushort[doubles.Length];
                    for (int i = 0; i < doubles.Length; i++)
                    {
                        double v = doubles[i];
                        pixels[i] = v <= 0 ? (ushort)0 : v >= ushort.MaxValue ? ushort.MaxValue : (ushort)v;
                    }
                    return pixels;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format.");
            }
        }

        /// <summary>
        /// Normalizes raw image bytes of any supported pixel format to a Mono16 byte
        /// array (two bytes per pixel, little-endian), e.g. for the field viewer path.
        /// </summary>
        public static byte[] NormalizeToMono16Bytes(byte[] image, ImagePixelFormat format)
        {
            var pixels = NormalizeToUInt16(image, format);
            var bytes = new byte[pixels.Length * 2];
            Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static WriteableBitmap UpdateContrastAndReturnBitmap(
            byte[] image_array,
            int width,
            int height,
            ImagePixelFormat format,
            int min_val,
            int max_val,
            bool autocontrast,
            WriteableBitmap? existingBitmap = null)
        {
            // Contrast limits are expressed on the 16-bit display scale; Mono8
            // slider values (0..255) are bit-replicated onto it.
            if (format == ImagePixelFormat.Mono8)
            {
                min_val = Math.Clamp(min_val, 0, 255) * 257;
                max_val = Math.Clamp(max_val, 0, 255) * 257;
            }

            var bytes = image_array.AsSpan();
            int pixelCount = bytes.Length / format.BytesPerPixel();

            // Auto-contrast: min/max over the raw pixels in their native range
            // (shared with the histogram), converted once onto the 16-bit
            // display scale.
            if (autocontrast)
            {
                (double rawMin, double rawMax) = format switch
                {
                    ImagePixelFormat.Mono8 => MinMaxOver(bytes),
                    ImagePixelFormat.Mono16 => MinMaxOver(MemoryMarshal.Cast<byte, ushort>(bytes.Slice(0, pixelCount * 2))),
                    ImagePixelFormat.Float64 => MinMaxOver(MemoryMarshal.Cast<byte, double>(bytes.Slice(0, pixelCount * 8))),
                    _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format."),
                };

                min_val = ToDisplayScale(rawMin, format);
                max_val = ToDisplayScale(rawMax, format);
            }

            if (existingBitmap == null || existingBitmap.Size != new Size(width, height))
            {
                existingBitmap = new WriteableBitmap(
                    new Avalonia.PixelSize(width, height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormats.Rgba8888,
                    Avalonia.Platform.AlphaFormat.Premul);
            }

            if (min_val == max_val)
            {
                ushort flatValue = max_val == ushort.MaxValue ? ushort.MaxValue : (ushort)0;

                unsafe
                {
                    using (var lockedBitmap = existingBitmap.Lock())
                    {
                        byte* dest = (byte*)lockedBitmap.Address.ToPointer();

                        for (int i = 0; i < pixelCount; i++)
                        {
                            byte gray = (byte)(flatValue >> 8);
                            uint packed = (uint)(gray | (gray << 8) | (gray << 16) | (255 << 24));
                            *(uint*)(dest + (i * 4)) = packed;
                        }
                    }
                }

                return existingBitmap!;
            }

            double range = max_val - min_val;
            double scale = (double)ushort.MaxValue / range;

            // Pixels are decoded straight from the raw bytes: Mono8 on the
            // bit-replicated 16-bit display scale, Float64 compared and stretched
            // as raw values (the limits always sit inside 0..65535, so
            // out-of-range values fall into the black/white branches naturally).
            unsafe
            {
                using (var lockedBitmap = existingBitmap.Lock())
                {
                    byte* dest = (byte*)lockedBitmap.Address.ToPointer();

                    switch (format)
                    {
                        case ImagePixelFormat.Mono8:
                            for (int i = 0; i < pixelCount; i++)
                            {
                                ushort val = (ushort)(bytes[i] << 8 | bytes[i]);
                                *(uint*)(dest + (i * 4)) = StretchedGray(val, min_val, max_val, scale);
                            }
                            break;

                        case ImagePixelFormat.Mono16:
                        {
                            var pixels = MemoryMarshal.Cast<byte, ushort>(bytes.Slice(0, pixelCount * 2));
                            for (int i = 0; i < pixels.Length; i++)
                            {
                                *(uint*)(dest + (i * 4)) = StretchedGray(pixels[i], min_val, max_val, scale);
                            }
                            break;
                        }

                        case ImagePixelFormat.Float64:
                        {
                            var pixels = MemoryMarshal.Cast<byte, double>(bytes.Slice(0, pixelCount * 8));
                            for (int i = 0; i < pixels.Length; i++)
                            {
                                *(uint*)(dest + (i * 4)) = StretchedGray(pixels[i], min_val, max_val, scale);
                            }
                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format.");
                    }
                }
            }

            return existingBitmap!;
        }

        /// <summary>
        /// Applies the contrast stretch to a 16-bit display-scale pixel and packs
        /// it as an opaque RGBA value.
        /// </summary>
        private static uint StretchedGray(ushort val, int min_val, int max_val, double scale)
        {
            byte gray = (byte)(StretchedValue(val, min_val, max_val, scale) >> 8);

            return (uint)(gray | (gray << 8) | (gray << 16) | (255 << 24));
        }

        /// <summary>
        /// Contrast stretch of a 16-bit display-scale pixel: values at or below
        /// min go to 0, values at or above max saturate to 65535, everything in
        /// between is stretched linearly.
        /// </summary>
        private static ushort StretchedValue(ushort val, int min_val, int max_val, double scale)
        {
            if (val <= min_val)
                return 0;
            if (val >= max_val)
                return ushort.MaxValue;
            return (ushort)((val - min_val) * scale);
        }

        /// <summary>
        /// Builds the field-viewer overview tile for an image: samples every 8th
        /// pixel of the raw data, stretches the sampled pixels against the 16-bit
        /// display-scale contrast limits, and returns the result as a Gray8
        /// bitmap. Sampling commutes with the per-pixel stretch, so only 1/64th
        /// of the image is decoded and stretched and no full-resolution
        /// intermediate copy is made.
        /// </summary>
        public static WriteableBitmap SubsampleContrastToGray8(
            byte[] image,
            int width,
            int height,
            ImagePixelFormat format,
            int min_val,
            int max_val)
        {
            int newWidth = width / 8;
            int newHeight = height / 8;

            var bitmap = new WriteableBitmap(
                new Avalonia.PixelSize(newWidth, newHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormats.Gray8,
                Avalonia.Platform.AlphaFormat.Premul);

            var bytes = image.AsSpan();
            int pixelCount = bytes.Length / format.BytesPerPixel();
            ReadOnlySpan<ushort> pixels16 = format == ImagePixelFormat.Mono16
                ? MemoryMarshal.Cast<byte, ushort>(bytes.Slice(0, pixelCount * 2)) : default;
            ReadOnlySpan<double> doubles = format == ImagePixelFormat.Float64
                ? MemoryMarshal.Cast<byte, double>(bytes.Slice(0, pixelCount * 8)) : default;

            unsafe
            {
                using (var fb = bitmap.Lock())
                {
                    byte* dst = (byte*)fb.Address.ToPointer();

                    if (min_val == max_val)
                    {
                        // Flat image: saturated renders white, otherwise black —
                        // same policy as the main display path.
                        ushort flatValue = max_val == ushort.MaxValue ? ushort.MaxValue : (ushort)0;
                        new Span<byte>(dst, newWidth * newHeight).Fill((byte)(flatValue >> 8));
                        return bitmap;
                    }

                    double scale = (double)ushort.MaxValue / (max_val - min_val);

                    for (int y = 0; y < newHeight; y++)
                    {
                        int row = (y * 8) * width;
                        for (int x = 0; x < newWidth; x++)
                        {
                            int si = row + x * 8;
                            ushort v16 = format switch
                            {
                                ImagePixelFormat.Mono8 => (ushort)(bytes[si] << 8 | bytes[si]),
                                ImagePixelFormat.Mono16 => pixels16[si],
                                ImagePixelFormat.Float64 => (ushort)ToDisplayScale(doubles[si], format),
                                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format."),
                            };

                            dst[y * newWidth + x] = (byte)(StretchedValue(v16, min_val, max_val, scale) >> 8);
                        }
                    }
                }
            }

            return bitmap;
        }

        /// <summary>
        /// Applies the contrast stretch to a floating-point pixel against limits
        /// expressed on the 16-bit display scale.
        /// </summary>
        private static uint StretchedGray(double val, int min_val, int max_val, double scale)
        {
            if (val <= min_val)
                return 0xFF000000u; // opaque black

            if (val >= max_val)
                return uint.MaxValue;

            int stretched = (int)((val - min_val) * scale);
            byte gray = (byte)(stretched >> 8);

            return (uint)(gray | (gray << 8) | (gray << 16) | (255 << 24));
        }





        public static WriteableBitmap ReturnRGBABitmapFrom16BitByteArray(byte[] data, WriteableBitmap im_source)//, AlphaMask alpha)
        {
            int length = data.Length ;
            Span<byte> rgba = new byte[length*2];

            Avalonia.PixelSize im_size = im_source.PixelSize;
            int width = im_size.Width;
            int height = im_size.Height;
            int pixelCount = data.Length / 2;


            unsafe
            {
                using (var lockedBitmap = im_source.Lock())
                {
                    byte* dest = (byte*)lockedBitmap.Address.ToPointer();

                    for (int p = 0; p < pixelCount; p++)
                    {
                        byte gray = data[(p << 1) | 1]; 

                        uint packed = (uint)(gray | (gray << 8) | (gray << 16) | (255 << 24));

                        *(uint*)(dest + (p * 4)) = packed;
                    }
                }

                return im_source;
            }
        }


        public static WriteableBitmap ReturnBitmapFromByteArray(byte[] data, WriteableBitmap im_source)
        {

            unsafe
            {

                using (var lockedBitmap = im_source.Lock())
                {

                    var destSpan = new Span<byte>(lockedBitmap.Address.ToPointer(), data.Length);
                    data.AsSpan().CopyTo(destSpan);


                    return im_source;

                }
            }
        }

        public static byte[] CopyBytes(byte[] data, byte[] destination)
        {
            data.AsSpan().CopyTo(destination);
            return destination;
        }
    }
}
