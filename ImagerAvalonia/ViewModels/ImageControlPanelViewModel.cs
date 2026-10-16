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
using System.IO;
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
    [ObservableProperty] private ImageTabViewModel? _selectedTab;
    [ObservableProperty] private bool _isExperimentEnabled = false;
    [ObservableProperty] private bool _isLiveEnabled = false;

    /// <summary>
    /// All tabs of the center panel: the three fixed tabs (Live view, Field view, Smart
    /// Program) followed by one closable tab per measurement or opened file. Every
    /// measurement runs in its own tab, so its images stay visible after the run while
    /// later runs get new tabs.
    /// </summary>
    public ObservableCollection<ImageTabViewModel> Tabs { get; } = new();

    // The tab created for the measurement that is being started; claimed by
    // Workspace_SessionStarted when its session begins acquiring.
    private ImageTabViewModel? _activeMeasurementTab;

    private readonly ILogger _logger;
    private readonly IImagerConnectionHandler _connectionHandler;
    private readonly IImageDisplayViewModelFactory _imageVmFactory;
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    public GlobalDefinedSettingsViewModel? DefinedAcquisitions;

    public event EventHandler<ILifetimeScope>? OnInitializeExperiment;
    public event EventHandler? OnFinishExperiment;

    public ImageControlPanelViewModel(
        ILoggerFactory loggerFactory,
        ImageDisplayViewModel liveView,
        FieldViewerViewModel fieldView,
        SmartProcessingRegisterViewModel smartPrograms,
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

        // The fixed tabs are never closed; measurements and opened files join them.
        Tabs.Add(new ImageTabViewModel("Live view", liveView, isFixed: true));
        Tabs.Add(new ImageTabViewModel("Field view", fieldView, isFixed: true));
        Tabs.Add(new ImageTabViewModel("Smart Program", smartPrograms, isFixed: true));
        SelectedTab = Tabs[0];

        // The live view shows only unstored live acquisitions: browsing is never
        // possible there, so "show last" is locked on for the tab's lifetime.
        liveView.IsLiveView = true;

        // Wire up to workspace events
        _workspace.SessionStarted += Workspace_SessionStarted;
        _workspace.SessionStopped += Workspace_SessionStopped;
        _workspace.SessionFinished += Workspace_SessionFinished;

        IsLiveEnabled = _workspace.IsLiveEnabled;
        IsExperimentEnabled = _workspace.IsExperimentEnabled;
    }

    partial void OnSelectedTabChanged(ImageTabViewModel? value)
    {
        // Keep the last measurement/file view while a fixed tab is active; MainView
        // reads SelectedView when splitting the current file.
        if (value is { IsFixed: false, ViewModel: { } viewModel })
            SelectedView = viewModel;
    }

    protected virtual void InitializeExperiment(ILifetimeScope scope) =>
        OnInitializeExperiment?.Invoke(this, scope);

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
        // The button doubles as the stop button: when a measurement is running, this
        // call stops it and must not create a tab.
        var willStop = _workspace.IsExperimentEnabled;

        ImageTabViewModel? tab = null;
        try
        {
            var experiment = _experimentManager.ReturnMeasurementTree();
            var storagepath = _experimentManager.GetStoragePath();
            var isstorageenabeld = _experimentManager.IsStorageEnabled();
            if (storagepath == null) throw new InvalidOperationException("Storage path is not set.");
            var detections = _experimentManager.ReturnUsedDetections();
            var stagePositions = ExperimentManager.ReturnUsedStagePositions(experiment);
            var fullEquipmentStateJson = _experimentManager.BuildFullEquipmentStateJson();

            if (!willStop)
            {
                // Every measurement runs in its own tab; the session started below attaches to it.
                tab = CreateImageTab(Path.GetFileNameWithoutExtension(storagepath));
                tab.ViewModel!.SetAvailableXYPositions(stagePositions);
                _activeMeasurementTab = tab;
            }

            await _workspace.ToggleExperimentAsync(
                experiment,
                storagepath,
                isstorageenabeld,
                detections,
                fullEquipmentStateJson
                );
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
        }
        catch (Exception ex)
        {
            if (tab != null && tab.Session == null)
            {
                // The run never started - don't leave a stray tab behind.
                CloseImageTab(tab);
            }
            IsExperimentEnabled = _workspace.IsExperimentEnabled;
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    /// <summary>Opens a saved measurement file in a new closable tab.</summary>
    public void OpenFileInNewTab(string filePath)
    {
        var localPath = new Uri(filePath).LocalPath.Replace("\\\\", "/");
        var fileName = Path.GetFileName(localPath);

        var scope = App.Container.BeginLifetimeScope();
        try
        {
            var storageProvider = scope.Resolve<IStorageProvider>();
            storageProvider.SetStoragePath(localPath);
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

            var session = new AcquisitionSession(
                AcquisitionSessionKind.File,
                scope,
                storageProvider,
                new ImageHandler(storageProvider, _logger, _connectionHandler),
                new CancellationTokenSource());

            var tab = CreateImageTab(fileName);
            tab.AttachSession(session);
            tab.ViewModel!.SetOpenID(storageProvider.GetOpenID());
            tab.ViewModel!.SetGridData(acqdetpairs);
            tab.ViewModel!.SetAvailableXYPositions(stagePositions);
            tab.ViewModel!.MaxFrameCount = storageProvider.LoadMaxFrameNumber();

            // A stored file is browsed, not followed: every planned frame already
            // exists, and the first one is shown right away.
            tab.ViewModel!.AcquiredFrameCount = tab.ViewModel!.MaxFrameCount;
            tab.ViewModel!.ShowLiveView = false;
            tab.ViewModel!.LoadFirstImage();
        }
        catch (Exception ex)
        {
            scope.Dispose();
            _ = ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
    }

    /// <summary>
    /// Closes a tab. Returns false (tab kept) for the fixed tabs and while its
    /// measurement is running.
    /// </summary>
    public bool CloseImageTab(ImageTabViewModel tab)
    {
        if (!tab.IsClosable) return false;
        tab.Dispose();
        Tabs.Remove(tab);
        return true;
    }

    private ImageTabViewModel CreateImageTab(string name)
    {
        var scope = App.Container.BeginLifetimeScope();
        var viewModel = _imageVmFactory.Create(scope);
        var tab = new ImageTabViewModel(name, viewModel, viewModelScope: scope);
        Tabs.Add(tab);
        SelectedTab = tab; // showing a new tab selects it
        return tab;
    }

    private ImageTabViewModel? FindTab(AcquisitionSession session) =>
        Tabs.FirstOrDefault(t => ReferenceEquals(t.Session, session));

    private void Workspace_SessionStarted(object? sender, AcquisitionSession session)
    {
        if (session.Kind == AcquisitionSessionKind.File) return; // file tabs attach themselves

        session.Handler.UpdateFieldViewDisplay += FieldView.ProcessImages;
        FieldView.SetGridData(session.StorageProvider.GetStorageSchema());

        if (session.Kind == AcquisitionSessionKind.Live)
        {
            IsLiveEnabled = true;
            // Show the Live view tab when live starts, so the acquisition is visible.
            SelectedTab = Tabs.First(t => t.Content == LiveView);
            LiveView.AttachHandler(session.Handler, enableFrameRequests: false);
            LiveView.SetAvailableXYPositions(new List<XYStagePosition>() { });
            LiveView.SetGridData(session.StorageProvider.GetStorageSchema());
            LiveView.IsExperimentRunning = false;
            LiveView.IsDataStreaming = true;
            LiveView.MaxFrameCount = session.StorageProvider.GetMaxNumberOfFrames();

            // The live view tab persists across sessions; start each one from a
            // clean frame state.
            LiveView.AcquiredFrameCount = 0;
            LiveView.IsFramePending = false;
        }
        else
        {
            IsExperimentEnabled = true;
            var tab = _activeMeasurementTab;
            _activeMeasurementTab = null;
            if (tab == null) return;

            tab.AttachSession(session);
            tab.ViewModel!.ShowLiveView = true;
            tab.ViewModel!.SetGridData(session.StorageProvider.GetStorageSchema());
            tab.ViewModel!.IsDataStreaming = true;
            tab.ViewModel!.IsExperimentRunning = true;
            tab.ViewModel!.MaxFrameCount = session.StorageProvider.GetMaxNumberOfFrames();

            InitializeExperiment(session.Scope);
        }
    }

    private void Workspace_SessionStopped(object? sender, AcquisitionSession session)
    {
        session.Handler.UpdateFieldViewDisplay -= FieldView.ProcessImages;

        if (session.Kind == AcquisitionSessionKind.Live)
        {
            IsLiveEnabled = false;
            LiveView.DetachHandler();
            LiveView.IsExperimentRunning = false;
            LiveView.IsDataStreaming = false;
        }
        else if (session.Kind == AcquisitionSessionKind.Experiment)
        {
            IsExperimentEnabled = false;
            // The tab stays attached to the session so the partially saved measurement
            // remains browsable once SessionFinished reopens the file for reading.
            var tab = FindTab(session);
            if (tab?.ViewModel != null)
            {
                tab.ViewModel.IsExperimentRunning = false;
                tab.ViewModel.IsDataStreaming = false;
            }
        }
    }

    private void Workspace_SessionFinished(object? sender, AcquisitionSession session)
    {
        if (session.Kind != AcquisitionSessionKind.Experiment) return;

        IsExperimentEnabled = false;
        session.Handler.UpdateFieldViewDisplay -= FieldView.ProcessImages;

        FindTab(session)?.SessionFinished();

        OnFinishExperiment?.Invoke(this, EventArgs.Empty);
    }

    internal void SetAvailableAcquisitions(GlobalDefinedSettingsViewModel SystemDefinedSettings)
    {
        DefinedAcquisitions = SystemDefinedSettings;
    }
}
