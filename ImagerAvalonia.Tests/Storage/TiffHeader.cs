namespace ImagerAvalonia.Tests.Storage;

/// <summary>
/// Minimal, independent TIFF reader (classic and BigTIFF, little-endian) used to check what
/// other programs (Fiji, Igor) will see in a written file, without going through the library.
/// </summary>
internal static class TiffHeader
{
    public readonly record struct ImageSize(ulong Width, ulong Length);

    public static List<ImageSize> ReadImageSizes(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));

        if (reader.ReadUInt16() != 0x4949)
            throw new NotSupportedException("Only little-endian TIFF is supported.");

        ushort version = reader.ReadUInt16();
        bool big = version == 43;
        if (!big && version != 42)
            throw new InvalidDataException($"Not a TIFF file (version {version}).");
        if (big)
        {
            reader.ReadUInt16(); // offset byte size (8)
            reader.ReadUInt16(); // reserved
        }

        ulong ifdOffset = big ? reader.ReadUInt64() : reader.ReadUInt32();
        var sizes = new List<ImageSize>();

        while (ifdOffset != 0)
        {
            reader.BaseStream.Position = (long)ifdOffset;
            ulong entries = big ? reader.ReadUInt64() : reader.ReadUInt16();
            ulong width = 0, length = 0;

            for (ulong i = 0; i < entries; i++)
            {
                ushort tag = reader.ReadUInt16();
                ushort type = reader.ReadUInt16();
                if (big) reader.ReadUInt64(); else reader.ReadUInt32(); // count
                long valueStart = reader.BaseStream.Position;

                ulong value = type switch
                {
                    3 => reader.ReadUInt16(),   // SHORT
                    4 => reader.ReadUInt32(),   // LONG
                    16 => reader.ReadUInt64(),  // LONG8
                    _ => 0,
                };

                if (tag == 256) width = value;
                if (tag == 257) length = value;

                reader.BaseStream.Position = valueStart + (big ? 8 : 4);
            }

            sizes.Add(new ImageSize(width, length));
            ifdOffset = big ? reader.ReadUInt64() : reader.ReadUInt32();
        }

        return sizes;
    }
}
