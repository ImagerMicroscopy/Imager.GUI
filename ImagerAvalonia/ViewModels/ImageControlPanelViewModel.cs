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

    private CancellationTokenSource _cancelLive = new();
    private readonly ILogger _logger;
    private readonly ComUtils _comUtils;
    private readonly IImageDisplayViewModelFactory _imageVmFactory;
    private readonly AcquisitionStateService _acquisitionState;
    public SystemDefinedSettingsViewModel? DefinedAcquisitions;


    public ImageHandler? LiveImageHandler { get; private set; }

    public event EventHandler<ILifetimeScope>? OnInitializeExperiment;
    public event EventHandler<ILifetimeScope>? OnInitializeLive;
    public event EventHandler<ILifetimeScope>? OnInitializeTifReader;

    public event EventHandler? OnFinishExperiment;
    

    public ImageControlPanelViewModel(
        ILoggerFactory loggerFactory,
        ImageDisplayViewModel liveView,
        FieldViewerViewModel fieldView,
        ComUtils comUtils,
        IImageDisplayViewModelFactory imageVmFactory,
        AcquisitionStateService acquisitionStateService)
    {
        _logger = loggerFactory.CreateLogger("Imager");
        _liveView = liveView;
        _fieldView = fieldView;
        _comUtils = comUtils;
        _imageVmFactory = imageVmFactory;
        _acquisitionState = acquisitionStateService;

        _acquisitionState.EndLive += OnEndLiveRequested;
        _acquisitionState.StartLive += OnStartLiveRequested;

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

        var tifHandler = new ImageHandler(storageProvider, _logger, _comUtils);

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
        IsLiveEnabled = !IsLiveEnabled;

        if (IsLiveEnabled)
        {
            _acquisitionState.SetLiveState();
            var scope = App.Container.BeginLifetimeScope();
            var expSerializer = scope.Resolve<IExperimentSerialization>();
            var storageProvider = scope.Resolve<IStorageProvider>();
            if (LiveImageHandler != null)
            {
                LiveView.OnDetectionRequested -= LiveImageHandler.LoadImage;
                LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
                LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
                LiveImageHandler.UpdateAsyncProgress -= LiveView.ProcessProgress;
                LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
            }
            try
            {
                _cancelLive = new CancellationTokenSource();
                _comUtils.SendDataRequest(ComUtils.cancelacquisition, "", _ => { }, _ => { });

                InitializeLive(scope);
                LiveImageHandler = new ImageHandler(storageProvider, _logger, _comUtils);
                LiveImageHandler.UpdateImageDisplay += LiveView.ProcessImages;
                LiveImageHandler.UpdateImageElements += LiveView.ProcessImageElements;
                LiveImageHandler.UpdateFieldViewDisplay  += FieldView.ProcessImages;
                LiveImageHandler.UpdateCurrentPositions += LiveView.ProcessPositions;


                LiveView.SetExperimentSerializer(expSerializer);
                LiveView.SetGridData(storageProvider.GetStorageSchema());
                FieldView.SetGridData(storageProvider.GetStorageSchema());

                LiveView.IsExperimentRunning = false;
                LiveView.IsDataStreaming = true;
                LiveView.LiveViewDisabled += LiveImageHandler.EnableDisableLiveView;
                LiveView.MaxFrameCount = storageProvider.GetMaxNumberOfFrames();

                await LiveImageHandler.ParseProgramAndShowData(_cancelLive);
            }
            catch (Exception ex)
            {
                IsLiveEnabled = false;
                if (LiveImageHandler != null)
                {
                    LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
                    LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
                    LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
                    LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
                }
                _acquisitionState.SetIdleState();

                await ExceptionWindowHandler.ShowExceptionAsync(ex);


            }
        }
        else
        {
            if (LiveImageHandler is not null)
            {
                _cancelLive?.Cancel();
                LiveView.IsDataStreaming = false;
                LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
                LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
                LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
                LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
                _acquisitionState.SetIdleState();
            }
        }
    }

    [RelayCommand]
    public async Task StartExperiment()
    {
        IsExperimentEnabled = !IsExperimentEnabled;

        if (IsExperimentEnabled)
        {// Unsubscribe to avoid duplicate event handlers when reusing the view.
         // The handler will be re-attached immediately after, so we only remove it here.
         // This allows the detection feature to work correctly across multiple experiment runs.
            _acquisitionState.SetExperimentState();

            if (LiveImageHandler != null)
            {
                LiveView.OnDetectionRequested -= LiveImageHandler.LoadImage;
                LiveImageHandler.UpdateImageDisplay -= LiveView.ProcessImages;
                LiveImageHandler.UpdateImageElements -= LiveView.ProcessImageElements;
                LiveImageHandler.UpdateAsyncProgress -= LiveView.ProcessProgress;
                LiveImageHandler.UpdateCurrentPositions -= LiveView.ProcessPositions;
            }
            var scope = App.Container.BeginLifetimeScope();
            var experimentSerializer = scope.Resolve<IExperimentSerialization>();
            var storageProvider = scope.Resolve<IStorageProvider>();
            _cancelLive = new CancellationTokenSource();
            try
            {
                _comUtils.SendDataRequest(ComUtils.cancelacquisition, "", _ => { }, _ => { });
                InitializeExperiment(scope);

                LiveImageHandler = new ImageHandler(storageProvider, _logger, _comUtils);
                LiveView.SetAvailableXYPositions(experimentSerializer.ExperimentPositions);
                LiveView.ShowLiveView = true;
                LiveView.SetExperimentSerializer(experimentSerializer);
                LiveView.SetGridData(storageProvider.GetStorageSchema());
                FieldView.SetGridData(storageProvider.GetStorageSchema());

                LiveView.IsDataStreaming = true;
                LiveView.IsExperimentRunning = true;
                LiveView.OnDetectionRequested += LiveImageHandler.LoadImage;
                LiveView.MaxFrameCount = storageProvider.GetMaxNumberOfFrames();
                LiveView.LiveViewDisabled += LiveImageHandler.EnableDisableLiveView;

                
                LiveImageHandler.UpdateImageElements += LiveView.ProcessImageElements;
                LiveImageHandler.UpdateImageDisplay += LiveView.ProcessImages;
                LiveImageHandler.UpdateFieldViewDisplay += FieldView.ProcessImages;
                LiveImageHandler.UpdateAsyncProgress += LiveView.ProcessProgress;
                LiveImageHandler.UpdateCurrentPositions += LiveView.ProcessPositions;

                bool success = await LiveImageHandler.ParseProgramAndShowData(_cancelLive);

                if (success)
                {
                    LiveView.IsDataStreaming = false;
                    LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
                    OnFinishExperiment?.Invoke(this, EventArgs.Empty);
                    IsExperimentEnabled = false;
                    _acquisitionState.SetIdleState();


                }
            }
            catch (Exception ex)
            {
                LiveView.IsDataStreaming = false;
                IsExperimentEnabled = false;
                _comUtils.SendDataRequest(ComUtils.cancelacquisition, "", _ => { }, _ => { });
                await ExceptionWindowHandler.ShowExceptionAsync(ex);
                OnFinishExperiment?.Invoke(this, EventArgs.Empty);
                if (LiveImageHandler != null)
                {
                    LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
                }
                _acquisitionState.SetIdleState();

            }
            finally
            {
                LiveView.IsDataStreaming = false;
                storageProvider.CloseReadWriteStream();
                await Task.Delay(2000);
                storageProvider.OpenReadStream();
                _acquisitionState.SetIdleState();

            }
        }
        else
        {
            if (LiveImageHandler is not null)
            {
                IsExperimentEnabled = false;
                LiveView.IsDataStreaming = false;
                LiveImageHandler.UpdateFieldViewDisplay -= FieldView.ProcessImages;
                _cancelLive?.Cancel();
                _acquisitionState.SetIdleState();
            }
        }
    }




    //private async Task ShowExceptionAsync(Exception ex)
    //{
    //    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    //    {
    //        await ExceptionWindowHandler.ShowDialogAsync(
    //            "Error", ex.Message, ex.StackTrace, desktop.MainWindow);
    //    }
    //}

    internal void SetAvailableAcquisitions(SystemDefinedSettingsViewModel SystemDefinedSettings)
    {
        DefinedAcquisitions = SystemDefinedSettings;
    }
}


