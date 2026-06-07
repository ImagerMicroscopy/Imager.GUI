using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Views;
using Microsoft.Extensions.Logging;
using ImagerAvalonia.Services;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;

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
    private readonly AcquisitionStateService _acquisitionState;
    private readonly ImagerWorkspace _workspace;
    public SystemDefinedSettingsViewModel? DefinedAcquisitions;

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
        AcquisitionStateService acquisitionStateService,
        ImagerWorkspace workspace)
    {
        _logger = loggerFactory.CreateLogger("Imager");
        _liveView = liveView;
        _fieldView = fieldView;
        _connectionHandler = connectionHandler;
        _imageVmFactory = imageVmFactory;
        _acquisitionState = acquisitionStateService;
        _workspace = workspace;

        _acquisitionState.EndLive += OnEndLiveRequested;
        _acquisitionState.StartLive += OnStartLiveRequested;

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

    public void LoadTifData(ImageDisplayView viewer, string path)
    {
        var scope = App.Container.BeginLifetimeScope();
        var storageProvider = scope.Resolve<IStorageProvider>();
        var expSerializer   = scope.Resolve<IExperimentSerialization>();
        var tifViewer = _imageVmFactory.Create(scope);

        viewer.DataContext = tifViewer;

        storageProvider.SetStoragePath(path);
        InitializeTifReader(scope);

        var tifHandler = new ImageHandler(storageProvider, _logger, _connectionHandler);

        tifViewer.SetOpenID(storageProvider.GetOpenID());
        tifViewer.SetGridData(storageProvider.GetStorageSchema());  
        tifViewer.SetAvailableXYPositions(expSerializer.ExperimentPositions);


        tifViewer.OnDetectionRequested += tifHandler.LoadImage;
        tifViewer.MaxFrameCount = storageProvider.GetMaxNumberOfFrames();
        tifHandler.UpdateImageDisplay += tifViewer.ProcessImages;
        tifHandler.UpdateCurrentPositions += tifViewer.ProcessPositions;
        tifViewer.LoadFirstImage();
    }

    [RelayCommand]
    public async Task EnableLive()
    {
        try {
            await _workspace.ToggleLiveAsync();
            IsLiveEnabled = _workspace.IsLiveEnabled;
        } catch (Exception ex) {
            IsLiveEnabled = _workspace.IsLiveEnabled;
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    [RelayCommand]
    public async Task StartExperiment()
    {
        try {
            await _workspace.ToggleExperimentAsync();
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
        } catch (Exception ex) {
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    private void Workspace_LiveHandlerCreated(object? sender, ILifetimeScope scope)
    {
        IsLiveEnabled = true;
        InitializeLive(scope);

        if (LiveImageHandler != null && _workspace.ActiveStorageProvider != null && _workspace.ActiveExperimentSerializer != null)
        {
            LiveImageHandler.UpdateImageDisplay += LiveView.ProcessImages;
            LiveImageHandler.UpdateImageElements += LiveView.ProcessImageElements;
            LiveImageHandler.UpdateFieldViewDisplay += FieldView.ProcessImages;
            LiveImageHandler.UpdateCurrentPositions += LiveView.ProcessPositions;

            LiveView.SetExperimentSerializer(_workspace.ActiveExperimentSerializer);
            LiveView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());
            FieldView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());

            LiveView.IsExperimentRunning = false;
            LiveView.IsDataStreaming = true;
            LiveView.LiveViewDisabled -= LiveImageHandler.EnableDisableLiveView; // remove old if any
            LiveView.LiveViewDisabled += LiveImageHandler.EnableDisableLiveView;
            LiveView.MaxFrameCount = _workspace.ActiveStorageProvider.GetMaxNumberOfFrames();
        }
    }

    private void Workspace_ExperimentHandlerCreated(object? sender, ILifetimeScope scope)
    {
        IsExperimentEnabled = true;
        InitializeExperiment(scope);

        if (LiveImageHandler != null && _workspace.ActiveStorageProvider != null && _workspace.ActiveExperimentSerializer != null)
        {
            LiveView.SetAvailableXYPositions(_workspace.ActiveExperimentSerializer.ExperimentPositions);
            LiveView.ShowLiveView = true;
            LiveView.SetExperimentSerializer(_workspace.ActiveExperimentSerializer);
            LiveView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());
            FieldView.SetGridData(_workspace.ActiveStorageProvider.GetStorageSchema());

            LiveView.IsDataStreaming = true;
            LiveView.IsExperimentRunning = true;
            
            LiveView.OnDetectionRequested -= LiveImageHandler.LoadImage;
            LiveView.OnDetectionRequested += LiveImageHandler.LoadImage;
            
            LiveView.MaxFrameCount = _workspace.ActiveStorageProvider.GetMaxNumberOfFrames();
            
            LiveView.LiveViewDisabled -= LiveImageHandler.EnableDisableLiveView;
            LiveView.LiveViewDisabled += LiveImageHandler.EnableDisableLiveView;

            LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
            LiveImageHandler.UpdateImageElements += LiveView.ProcessImageElements;
            
            LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
            LiveImageHandler.UpdateImageDisplay += LiveView.ProcessImages;
            
            LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
            LiveImageHandler.UpdateFieldViewDisplay += FieldView.ProcessImages;
            
            LiveImageHandler.UpdateAsyncProgress -= LiveView.ProcessProgress;
            LiveImageHandler.UpdateAsyncProgress += LiveView.ProcessProgress;
            
            LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
            LiveImageHandler.UpdateCurrentPositions += LiveView.ProcessPositions;
        }
    }

    private void Workspace_HandlerDestroyed(object? sender, EventArgs e)
    {
        IsLiveEnabled = false;
        IsExperimentEnabled = false;

        if (LiveView != null) 
        {
            LiveView.IsDataStreaming = false;
        }

        if (LiveImageHandler != null)
        {
            LiveView.OnDetectionRequested -= LiveImageHandler.LoadImage;
            LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
            LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
            LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
            LiveImageHandler.UpdateAsyncProgress -= LiveView.ProcessProgress;
            LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
            LiveView.LiveViewDisabled -= LiveImageHandler.EnableDisableLiveView;
        }
    }

    private void Workspace_ExperimentFinished(object? sender, EventArgs e)
    {
        IsExperimentEnabled = false;
        LiveView.IsDataStreaming = false;
        
        if (LiveImageHandler != null) {
            LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
        }
        
        OnFinishExperiment?.Invoke(this, EventArgs.Empty);
    }

    internal void SetAvailableAcquisitions(SystemDefinedSettingsViewModel SystemDefinedSettings)
    {
        DefinedAcquisitions = SystemDefinedSettings;
    }
}
