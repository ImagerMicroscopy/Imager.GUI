using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.ExperimentWorkspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using ImagerAvalonia.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TextMateSharp.Internal.Parser;

namespace ImagerAvalonia.ViewModels;

public partial class ImageControlPanelViewModel : ViewModelBase
{
    [ObservableProperty] private ImageDisplayViewModel _liveView;
    [ObservableProperty] private FieldViewerViewModel _fieldView;
    [ObservableProperty] private ImageDisplayViewModel? _selectedView;
    [ObservableProperty] private bool _isExperimentEnabled = false;
    [ObservableProperty] private bool _isStageAvailable = true;
    [ObservableProperty] private bool _isLiveEnabled = false;

    [ObservableProperty] private string _tifDataPath = String.Empty;

    private readonly ILogger _logger;
    private readonly IImagerConnectionHandler _connectionHandler;
    private readonly IImageDisplayViewModelFactory _imageVmFactory;
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    public GlobalDefinedSettingsViewModel? DefinedAcquisitions;

    // The ImageHandler currently wired up to LiveView/FieldView events.
    // This is the ONLY source of truth for what we're subscribed to — never
    // rely on _workspace.ActiveImageHandler at unsubscribe time, since by
    // then it may already point to a different (or null) instance.
    private ImageHandler? _subscribedHandler;

    public ImageHandler? LiveImageHandler => _workspace.ActiveImageHandler;

    public event EventHandler<ILifetimeScope>? OnInitializeExperiment;
    public event EventHandler<ILifetimeScope>? OnInitializeLive;
    public event EventHandler<ILifetimeScope>? OnInitializeTifReader;

    public event EventHandler? OnFinishExperiment;

    public ImageControlPanelViewModel(
        ILoggerFactory loggerFactory,
        ImageDisplayViewModel liveView,
        FieldViewerViewModel fieldView,
        IImagerConnectionHandler connectionHandler,
        IImageDisplayViewModelFactory imageVmFactory,
        ImagerWorkspace workspace,
        ExperimentManager experimentManager)
    {
        _logger = loggerFactory.CreateLogger("Imager");
        _liveView = liveView;
        _fieldView = fieldView;
        _connectionHandler = connectionHandler;
        _imageVmFactory = imageVmFactory;
        _workspace = workspace;
        _experimentManager = experimentManager;



        // Wire up to workspace events
        _workspace.LiveScopeCreated += Workspace_LiveHandlerCreated;
        _workspace.ExperimentScopeCreated += Workspace_ExperimentHandlerCreated;
        _workspace.HandlerDestroyed += Workspace_HandlerDestroyed;
        _workspace.ExperimentFinished += Workspace_ExperimentFinished;

        IsLiveEnabled = _workspace.IsLiveEnabled;
        IsExperimentEnabled = _workspace.IsExperimentEnabled;
    }

    private async void OnEndLiveRequested(object? sender, EventArgs e)
    {
        await EnableLive();
    }
    private async void OnStartLiveRequested(object? sender, EventArgs e)
    {
        await EnableLive();
    }

    protected virtual void InitializeExperiment(ILifetimeScope scope) =>
        OnInitializeExperiment?.Invoke(this, scope);

    protected virtual void InitializeLive(ILifetimeScope scope) =>
        OnInitializeLive?.Invoke(this, scope);

    protected virtual void InitializeTifReader(ILifetimeScope scope) =>
        OnInitializeTifReader?.Invoke(this, scope);

    public void LoadTifData(ImageDisplayViewModel tifViewer, string path)
    {
        var scope = App.Container.BeginLifetimeScope();
        var storageProvider = scope.Resolve<IStorageProvider>();

        storageProvider.SetStoragePath(path);
        storageProvider.OpenReadStream();
        string program = storageProvider.GetImagerProgram();
        var loader   = new ExperimentLoaderService();
        var programTree = loader.GetProgramTree(program);


        var detections = programTree.Detections;
        var acqdetpairs = detections.Select(x => x.Value.Detectors.Select(y => new Tuple<string,string>(x.Key, y.Detectorname)))
            .SelectMany(x => x)
            .ToList();


        var stagePositions = new List<XYStagePosition>();
        ExperimentManager.ReturnStagePositions(programTree.Program, ref stagePositions);    

        InitializeTifReader(scope);

        var tifHandler = new ImageHandler(storageProvider, _logger, _connectionHandler);



        storageProvider.SetAcqDetPairs(acqdetpairs);
        tifViewer.SetOpenID(storageProvider.GetOpenID());
        tifViewer.SetGridData(acqdetpairs);
        tifViewer.SetAvailableXYPositions(stagePositions);


        tifViewer.OnDetectionRequested += tifHandler.LoadImage;
        tifViewer.MaxFrameCount = storageProvider.LoadMaxFrameNumber();
        tifHandler.UpdateImageDisplay += tifViewer.ProcessImages;
        tifHandler.UpdateCurrentPositions += tifViewer.ProcessPositions;
        tifViewer.LoadFirstImage();
    }

    [RelayCommand]
    public async Task EnableLive()
    {
        try
        {
            await _workspace.ToggleLiveAsync(_experimentManager.SelectedDetection);
            IsLiveEnabled = _workspace.IsLiveEnabled;
        }
        catch (Exception ex)
        {
            IsLiveEnabled = _workspace.IsLiveEnabled;
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    [RelayCommand]
    public async Task StartExperiment()
    {
        try
        {

            var experiment = _experimentManager.ReturnMeasurementTree();
            var storagepath = _experimentManager.GetStoragePath();
            var isstorageenabeld = _experimentManager.IsStorageEnabled();
            if (storagepath == null) throw new InvalidOperationException("Storage path is not set.");
            var detections = _experimentManager.ReturnUsedDetections();
            var stagePositions = ExperimentManager.ReturnUsedStagePositions(experiment);
            LiveView.SetAvailableXYPositions(stagePositions);
            await _workspace.ToggleExperimentAsync(
                experiment,
                storagepath,
                isstorageenabeld,
                detections
                );
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
        }
        catch (Exception ex)
        {
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    /// <summary>
    /// Wires up all LiveView/FieldView <-> ImageHandler event subscriptions for the given
    /// handler instance, and records that instance so it can be correctly unsubscribed later
    /// regardless of what _workspace.ActiveImageHandler points to at that time.
    /// </summary>
    /// <param name="handler">The handler instance to subscribe to.</param>
    /// <param name="isExperiment">
    /// True to also wire the experiment-only subscriptions (progress reporting and
    /// detection-request routing from LiveView into the handler).
    /// </param>
    private void SubscribeToHandler(ImageHandler handler, bool isExperiment)
    {
        // Defensive: if something is already wired (shouldn't normally happen), tear it
        // down first so we never end up with duplicate subscriptions.
        UnsubscribeFromHandler();

        _subscribedHandler = handler;

        handler.UpdateImageDisplay += LiveView.ProcessImages;
        handler.UpdateImageElements += LiveView.ProcessImageElements;
        handler.UpdateFieldViewDisplay += FieldView.ProcessImages;
        handler.UpdateCurrentPositions += LiveView.ProcessPositions;
        LiveView.LiveViewDisabled += handler.EnableDisableLiveView;

        if (isExperiment)
        {
            LiveView.OnDetectionRequested += handler.LoadImage;
            handler.UpdateAsyncProgress += LiveView.ProcessProgress;
        }
    }

    /// <summary>
    /// Tears down all event subscriptions previously wired by <see cref="SubscribeToHandler"/>,
    /// using the captured <see cref="_subscribedHandler"/> instance rather than the (possibly
    /// stale or null) <see cref="LiveImageHandler"/> property. Safe to call multiple times.
    /// </summary>
    private void UnsubscribeFromHandler()
    {
        if (_subscribedHandler is null) return;

        var handler = _subscribedHandler;

        handler.UpdateImageDisplay -= LiveView.ProcessImages;
        handler.UpdateImageElements -= LiveView.ProcessImageElements;
        handler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
        handler.UpdateCurrentPositions -= LiveView.ProcessPositions;
        LiveView.LiveViewDisabled -= handler.EnableDisableLiveView;

        // These are no-ops if they were never added (experiment-only subscriptions),
        // which is fine — event -= is safe against handlers that were never attached.
        LiveView.OnDetectionRequested -= handler.LoadImage;
        handler.UpdateAsyncProgress -= LiveView.ProcessProgress;

        _subscribedHandler = null;
    }

    private void Workspace_LiveHandlerCreated(object? sender, ILifetimeScope scope)
    {
        IsLiveEnabled = true;
        InitializeLive(scope);

        if (LiveImageHandler != null && _workspace.ActiveStorageProvider != null)
        {
            SubscribeToHandler(LiveImageHandler, isExperiment: false);
            LiveView.SetAvailableXYPositions(new List<XYStagePosition>() { });

            //LiveView.SetExperimentSerializer(_workspace.ActiveExperimentSerializer);
            LiveView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());
            FieldView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());

            LiveView.IsExperimentRunning = false;
            LiveView.IsDataStreaming = true;
            LiveView.MaxFrameCount = _workspace.ActiveStorageProvider.GetMaxNumberOfFrames();
        }
    }

    private void Workspace_ExperimentHandlerCreated(object? sender, ILifetimeScope scope)
    {
        IsExperimentEnabled = true;
        InitializeExperiment(scope);

        if (LiveImageHandler != null && _workspace.ActiveStorageProvider != null)
        {
            SubscribeToHandler(LiveImageHandler, isExperiment: true);

            //LiveView.SetAvailableXYitions(_workspace.ActiveExperimentSerializer.ExperimentPositions);
            LiveView.ShowLiveView = true;
            //LiveView.SetExperimentSerializer(_workspace.ActiveExperimentSerializer);
            LiveView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());
            FieldView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());

            LiveView.IsDataStreaming = true;
            LiveView.IsExperimentRunning = true;

            LiveView.MaxFrameCount = _workspace.ActiveStorageProvider.GetMaxNumberOfFrames();
        }
    }

    private void Workspace_HandlerDestroyed(object? sender, EventArgs e)
    {
        IsLiveEnabled = false;
        IsExperimentEnabled = false;

        if (LiveView != null)
        {
            LiveView.IsExperimentRunning = false;
            LiveView.IsDataStreaming = false;
        }

        // Uses the captured _subscribedHandler internally, so this correctly detaches
        // from the handler we actually subscribed to — even if _workspace.ActiveImageHandler
        // has already changed to null or a new instance by the time this fires.
        UnsubscribeFromHandler();
    }

    private void Workspace_ExperimentFinished(object? sender, EventArgs e)
    {
        IsExperimentEnabled = false;
        LiveView.IsDataStreaming = false;

        if (_subscribedHandler != null)
        {
            _subscribedHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
        }

        OnFinishExperiment?.Invoke(this, EventArgs.Empty);
    }

    internal void SetAvailableAcquisitions(GlobalDefinedSettingsViewModel SystemDefinedSettings)
    {
        DefinedAcquisitions = SystemDefinedSettings;
    }
}