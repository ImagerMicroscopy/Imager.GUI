using System;

namespace ImagerAvalonia.Utils;

/// <summary>
/// Pixel format of image data sent by Imager.Core in the "numtype" field of the
/// MessagePack image messages. The numeric values match the MIS_PIXELFORMAT_*
/// defines in the Imager storage library, so they can be cast to int when calling
/// into the native storage API.
/// </summary>
public enum ImagePixelFormat
{
    Mono16 = 0,
    Float64 = 1,
    Mono8 = 2,
}

public static class ImagePixelFormatExtensions
{
    /// <summary>
    /// Bytes per pixel for the format. Unknown values are a hard error.
    /// </summary>
    public static int BytesPerPixel(this ImagePixelFormat format) => format switch
    {
        ImagePixelFormat.Mono8 => 1,
        ImagePixelFormat.Mono16 => 2,
        ImagePixelFormat.Float64 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format.")
    };
}
