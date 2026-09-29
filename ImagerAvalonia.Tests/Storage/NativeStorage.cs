using System.Runtime.InteropServices;
using Xunit;

namespace ImagerAvalonia.Tests.Storage;

/// <summary>
/// Locates the native MeasurementImageStorage library (built from Imager.TiffLib) for the
/// integration tests. Set IMAGER_MIS_LIBRARY to the built file, e.g.
///   IMAGER_MIS_LIBRARY=/path/to/builddir/libMeasurementImageStorageDLL.dll dotnet test
/// It is copied next to the test assembly under the name MISStorageProvider's resolver
/// expects on this platform. Without it, these tests are skipped.
/// </summary>
internal static class NativeStorage
{
    public const string EnvironmentVariable = "IMAGER_MIS_LIBRARY";

    private static readonly Lazy<string?> _unavailableReason = new(Prepare);

    public static string? UnavailableReason => _unavailableReason.Value;

    private static string ExpectedFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "MeasurementImageStorageDLL.dll"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libMeasurementImageStorageDLL.dylib"
        : "libMeasurementImageStorageDLL.so";

    private static string? Prepare()
    {
        var target = Path.Combine(AppContext.BaseDirectory, ExpectedFileName);
        var source = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (!string.IsNullOrEmpty(source))
        {
            if (!File.Exists(source))
                return $"{EnvironmentVariable} points to a missing file: {source}";
            if (!File.Exists(target) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(target))
                File.Copy(source, target, overwrite: true);
        }

        return File.Exists(target)
            ? null
            : $"Native storage library not found. Set {EnvironmentVariable} to the Imager.TiffLib build output.";
    }
}

/// <summary>A [Fact] that is skipped when the native storage library is not available.</summary>
public sealed class NativeStorageFactAttribute : FactAttribute
{
    public NativeStorageFactAttribute()
    {
        if (NativeStorage.UnavailableReason is { } reason)
            Skip = reason;
    }
}
