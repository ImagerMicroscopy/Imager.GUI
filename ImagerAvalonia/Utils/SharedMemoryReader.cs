using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ImagerAvalonia.Utils;

public class SharedMemoryReader : IDisposable
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private string? _mapName;

    /// <summary>
    /// Gets the name of the currently connected shared memory region.
    /// </summary>
    public string? GetMapName() => _mapName;

    /// <summary>
    /// Connects to a shared memory region initialized by an external server.
    /// </summary>
    public void Connect(string mapName)
    {
        Dispose(); // Clean up existing connections if any
        _mapName = mapName;

        try {
            // Standard approach - Works for Windows and often Linux if naming conventions align
            _mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
        }
        catch (PlatformNotSupportedException) {
            // Fallback for some Linux/macOS scenarios where named MMF fails
            _mmf = OpenLinuxSharedMemory(mapName);
        }
        catch (FileNotFoundException) {
            // Fallback for Linux where the name mapping isn't directly resolved by OpenExisting
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                _mmf = OpenLinuxSharedMemory(mapName);
            }
            else {
                throw;
            }
        }

        // Pass 0 to map the entire file (allows determining size dynamically based on the mapping)
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
    }

    private MemoryMappedFile OpenLinuxSharedMemory(string mapName) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            throw new PlatformNotSupportedException("Named shared memory is not supported on this platform.");
        }
        
        string shmPath = mapName.StartsWith("/") ? $"/dev/shm{mapName}" : $"/dev/shm/{mapName}";
        return MemoryMappedFile.CreateFromFile(shmPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
    }

    /// <summary>
    /// Gets the total size in bytes of the connected shared memory region.
    /// </summary>
    public long GetSizeInBytes() {
        if (_accessor == null) throw new InvalidOperationException("Not connected to shared memory.");
        return _accessor.Capacity;
    }

    private void EnsureBounds(long offset, int length) {
        if (_accessor == null) throw new InvalidOperationException("Not connected to shared memory.");
        if (offset < 0 || length < 0 || offset + length > _accessor.Capacity) {
            throw new ArgumentOutOfRangeException(nameof(offset), $"Attempted to read out of bounds. Capacity: {_accessor.Capacity}, Offset: {offset}, Length: {length}");
        }
    }

    /// <summary>
    /// Standard array read. Suitable for smaller chunks of data.
    /// </summary>
    public byte[] ReadData(long offset, int length) {
        if (_accessor == null) throw new InvalidOperationException("Not connected to shared memory.");
        EnsureBounds(offset, length);

        byte[] buffer = new byte[length];
        _accessor.ReadArray(offset, buffer, 0, length);
        return buffer;
    }

    /// <summary>
    /// High performance, zero-allocation read using Span.
    /// Best for images or large data arrays. Requires 'unsafe' context.
    /// </summary>
    public unsafe void ReadDataFast(long offset, Span<byte> destination) {
        if (_accessor == null) throw new InvalidOperationException("Not connected to shared memory.");
        EnsureBounds(offset, destination.Length);

        byte* pointer = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try {
            var sourceSpan = new ReadOnlySpan<byte>(pointer + offset, destination.Length);
            sourceSpan.CopyTo(destination);
        } finally {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public unsafe TResult WithPayloadMemory<TResult>(Func<ReadOnlyMemory<byte>, TResult> consume)
    {

        if (_accessor == null) throw new InvalidOperationException("Not connected to shared memory.");

        byte* pointer = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try
        {
            long dataLength = *(long*)pointer;
            int totalLen = (int)dataLength;
            EnsureBounds(0, totalLen);

            using var manager = new UnmanagedMemoryManager(pointer, totalLen);
            ReadOnlyMemory<byte> payload = manager.Memory.Slice(8); // skip 8-byte length prefix
            return consume(payload); // must run synchronously — memory is invalid after ReleasePointer
        }
        finally
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public void Dispose() {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _accessor = null;
        _mmf = null;
        _mapName = null;
    }
}


public sealed unsafe class UnmanagedMemoryManager : MemoryManager<byte>
{
    private readonly byte* _pointer;
    private readonly int _length;

    public UnmanagedMemoryManager(byte* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    public override Span<byte> GetSpan() => new(_pointer, _length);

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if ((uint)elementIndex >= (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        return new MemoryHandle(_pointer + elementIndex);
    }

    public override void Unpin() { } // pointer lifetime is owned by the caller's AcquirePointer/ReleasePointer scope

    protected override void Dispose(bool disposing) { }
}