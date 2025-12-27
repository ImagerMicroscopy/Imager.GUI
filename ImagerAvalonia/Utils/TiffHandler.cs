using Avalonia;
using Avalonia.Media.Imaging;
using BitMiracle.LibTiff.Classic;
using DynamicData.Diagnostics;
using ImagerAvalonia.Views;
using System;
using System.IO;
using System.Linq;
using System.Threading;



namespace ImagerAvalonia.Utils
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



            unsafe
            {
                fixed (byte* ptr = image_array)
                {
                    for (int i = 0; i < image_array.Length; i++)
                    {



                        if (ptr[i] < max_val && ptr[i] > min_val)
                        {
                            ptr[i] = (byte)((float)((ptr[i] - min_val)) / (max_val - min_val) * byte.MaxValue);
                        }
                        else if (ptr[i] < min_val)
                        {
                            ptr[i] = 0;
                        }
                        else if (ptr[i] > max_val)
                        {
                            ptr[i] = byte.MaxValue;
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

            unsafe
            {
                fixed (ushort* ptr = converted_array)
                {
                    for (int i = 0; i < converted_array.Length; i++)
                    {



                        if (ptr[i] < max_val && ptr[i] > min_val)
                        {
                            ptr[i] = (ushort)((float)((ptr[i] - min_val)) / ((ushort)max_val - (ushort)min_val) * ushort.MaxValue);
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

        public static void UpdateHistogramValues(byte[] image_array, ref double[] histogram_vals_y, ref double[] histogram_vals_y_low, ref double[] histogram_vals_x  )
        {
            ushort[] converted_array = new ushort[image_array.Length / 2];

            Buffer.BlockCopy(image_array, 0, converted_array, 0, image_array.Length);


            for (int hist_val = 0; hist_val < histogram_vals_y.Length; hist_val++)
            {
                histogram_vals_y[hist_val] = 0;
            }

            for (int hist_val = 0; hist_val < histogram_vals_y.Length; hist_val++)
            {
                histogram_vals_y[hist_val] = 0;
            }
            double min_image_val = converted_array.Min();
            double max_image_val = converted_array.Max();
           

            double bin_size = (max_image_val - min_image_val) / 256;

            for (int hist_val = 0; hist_val < histogram_vals_x.Length; hist_val++)
            {
                histogram_vals_x[hist_val] = min_image_val + hist_val * bin_size;
            }

            unsafe
            {
                fixed (ushort* ptr = converted_array)
                {
                    for (int i = 0; i < converted_array.Length; i++)
                    {
                        double bin_value = (double)ptr[i] - min_image_val;
                        int bin_position = 0;

                        if (bin_value != 0)
                        {
                            bin_position = (int)(bin_value / bin_size);
                        }

                        histogram_vals_y[bin_position] += 1;


                    }
                }
            }
        }

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

            double range = max_val - min_val;
            double scale = range > 0 ? (double)ushort.MaxValue / range : 0.0;

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


        public static WriteableBitmap UpdateContrastAndReturnBitmap(
            byte[] image_array,
            int width,
            int height,
            ushort min_val,
            ushort max_val,
            bool autocontrast,
            WriteableBitmap? existingBitmap = null)
        {
            int pixelCount = image_array.Length / 2;

            // Auto-contrast: compute min/max directly from byte array
            if (autocontrast)
            {
                ushort localMin = ushort.MaxValue;
                ushort localMax = ushort.MinValue;

                unsafe
                {
                    fixed (byte* ptr = image_array)
                    {
                        ushort* src = (ushort*)ptr;
                        for (int i = 0; i < pixelCount; i++)
                        {
                            ushort val = src[i];
                            if (val < localMin) localMin = val;
                            if (val > localMax) localMax = val;
                        }
                    }
                }

                min_val = localMin;
                max_val = localMax;
            }

            double range = max_val - min_val;
            double scale = range > 0 ? (double)ushort.MaxValue / range : 0.0;

            if (existingBitmap == null || existingBitmap.Size != new Size(width, height))
            {
                existingBitmap = new WriteableBitmap(
                    new Avalonia.PixelSize(width, height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormats.Rgba8888,
                    Avalonia.Platform.AlphaFormat.Premul);
            }

            unsafe
            {
                fixed (byte* ptr = image_array)
                {
                    ushort* src = (ushort*)ptr;

                    using (var lockedBitmap = existingBitmap.Lock())
                    {
                        byte* dest = (byte*)lockedBitmap.Address.ToPointer();

                        for (int i = 0; i < pixelCount; i++)
                        {
                            ushort val = src[i];

                            if (val <= min_val)
                                val = 0;
                            else if (val >= max_val)
                                val = ushort.MaxValue;
                            else
                                val = (ushort)((val - min_val) * scale);

                            byte gray = (byte)(val >> 8);

                            uint packed = (uint)(gray | (gray << 8) | (gray << 16) | (255 << 24));

                            *(uint*)(dest + (i * 4)) = packed;
                        }
                    }
                }
            }

            return existingBitmap!;
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
