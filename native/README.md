# Native measurement image storage library (Imager.TiffLib)

The GUI P/Invokes into a native library built from the Imager.TiffLib repository.
The compiled binaries are checked in here, one folder per runtime identifier (RID):

```
native/win-x64/MeasurementImageStorage.dll
native/linux-x64/libMeasurementImageStorage.so
native/osx-x64/libMeasurementImageStorage.dylib
native/osx-arm64/libMeasurementImageStorage.dylib    (if built)
native/linux-arm64/libMeasurementImageStorage.so      (if built)
```

`ImagerAvalonia.csproj` copies the file for the RID being built (or the build
host's RID when no RID is specified) next to the app, and `MISStorageProvider`'s
DllImportResolver loads it from there. The build fails if the file for the RID
being built is missing, so every RID you build for needs its folder populated.

When replacing the binaries with a new Imager.TiffLib build, keep the library's
API version in sync with `MISStorageProvider.ExpectedApiVersion` (currently 2) —
the GUI checks it on first storage use and refuses a mismatched library.
