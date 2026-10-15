using Autofac;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using System;
using System.Threading;

namespace ImagerAvalonia.Services.Workspace;

public enum AcquisitionSessionKind
{
    /// <summary>Live acquisition; the data is not saved.</summary>
    Live,
    /// <summary>Measurement; the data is saved to a file.</summary>
    Experiment,
    /// <summary>A previously saved file opened for browsing.</summary>
    File
}

/// <summary>
/// The lifetime scope, storage provider, image handler and cancellation token that make up
/// one acquisition run. Who disposes the session depends on the kind: the workspace disposes
/// Live sessions when live mode stops; Experiment and File sessions outlive their run so the
/// UI tab showing them can keep browsing the saved data, and are disposed when that tab is
/// closed.
/// </summary>
public sealed class AcquisitionSession : IDisposable
{
    public AcquisitionSession(
        AcquisitionSessionKind kind,
        ILifetimeScope scope,
        IStorageProvider storageProvider,
        ImageHandler handler,
        CancellationTokenSource cancellation)
    {
        Kind = kind;
        Scope = scope;
        StorageProvider = storageProvider;
        Handler = handler;
        Cancellation = cancellation;
    }

    public AcquisitionSessionKind Kind { get; }
    public ILifetimeScope Scope { get; }
    public IStorageProvider StorageProvider { get; }
    public ImageHandler Handler { get; }
    public CancellationTokenSource Cancellation { get; }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Cancellation.Cancel();
        Cancellation.Dispose();
        Scope.Dispose();
    }
}
