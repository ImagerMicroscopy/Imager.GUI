using System.Threading.Channels;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImagerAvalonia;
using ImagerAvalonia.Services.GenAI;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.ExperimentWorkspace;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.GenAIViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using ImagerAvalonia.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests.Views;

/// <summary>
/// Every measurement runs in its own closable tab (regression: runs used to overwrite the
/// previous one in the live view). The backend is a scripted IImagerCommunicationManager,
/// storage a mock IStorageProvider that "writes" a marker file on close, so each run gets
/// its own file name exactly like the real storage would.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class MeasurementTabTests : IDisposable
{
    private readonly TestContainer _container = new(builder =>
        builder.RegisterType<ImageDisplayViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired());
    private readonly Mock<IImagerCommunicationManager> _comm = new();
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    private readonly ImageControlPanelViewModel _vm;
    private readonly string _tempDir = Directory.CreateTempSubdirectory("tab-tests-").FullName;
    private string? _storagePath;

    /// <summary>What the fake backend does when a program is started.</summary>
    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> _backend =
        async (w, _) => { await w.WriteAsync(new MeasurementCompletedEvent()); w.TryComplete(); };

    public MeasurementTabTests()
    {
        _comm.Setup(c => c.ExecuteMeasurementProgram(
                It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>(
                (request, writer, token) => _ = Task.Run(() => _backend(writer, token)));

        // Emulate the real storage writing the file on close, so a second run picks the
        // next file name (measurement000001) instead of overwriting measurement000000.
        _container.StorageProvider.Setup(s => s.SetStoragePath(It.IsAny<string>()))
            .Callback<string>(p => _storagePath = p);
        _container.StorageProvider.Setup(s => s.CloseReadWriteStream())
            .Callback(() => { if (_storagePath != null) File.WriteAllText(_storagePath, "tif"); });

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        _workspace = new ImagerWorkspace(
            _container.Container,
            loggerFactory.Object,
            Mock.Of<IImagerConnectionHandler>(),
            _comm.Object,
            new SmartProgramRegistry())
        {
            StorageReleaseDelay = TimeSpan.Zero
        };

        _experimentManager = new ExperimentManager(
            _container.Settings,
            _container.StageControl.Object,
            EquipmentFixtures.Workspace(),
            _container.Resolve<SmartProcessingRegisterViewModel>(),
            _container.Factory,
            new ExperimentBuilderFactory(_container.Factory));

        _vm = new ImageControlPanelViewModel(
            loggerFactory.Object,
            new ImageDisplayViewModel(),
            new FieldViewerViewModel(),
            _container.Resolve<SmartProcessingRegisterViewModel>(),
            Mock.Of<IImagerConnectionHandler>(),
            new ImageDisplayViewModelFactory(),
            _workspace,
            _experimentManager);

        // One experiment with a single detection, saved into a temp folder.
        _container.AddAcquisitions("Acq1");
        var experiment = _experimentManager.SelectedExperiment!;
        experiment.Root.StorageService!.OutputFolder = _tempDir;
        experiment.Root.StorageService.FileName = "measurement";
        experiment.Root.Builder!.AddNode(ExperimentElementType.Detection, experiment.Root);
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _experimentManager.Dispose();
        _container.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    [Fact]
    public void The_three_fixed_tabs_are_present_and_cannot_be_closed()
    {
        Assert.Equal(new[] { "Live view", "Field view", "Smart Program" },
            _vm.Tabs.Select(t => t.Header).ToArray());
        Assert.All(_vm.Tabs, tab =>
        {
            Assert.True(tab.IsFixed);
            Assert.False(tab.IsClosable);
            Assert.False(_vm.CloseImageTab(tab));
        });
        Assert.Equal(3, _vm.Tabs.Count);
        Assert.Same(_vm.Tabs[0], _vm.SelectedTab); // the Live view is selected initially
    }

    [Fact]
    public async Task A_new_measurement_tab_is_selected_and_becomes_the_selected_view()
    {
        Assert.Null(_vm.SelectedView); // the Live view tab is fixed: SelectedView stays unset

        await _vm.StartExperiment();
        var tab = _vm.Tabs.Single(t => !t.IsFixed);

        Assert.Same(tab, _vm.SelectedTab);
        Assert.Same(tab.ViewModel, _vm.SelectedView);

        // Switching to a fixed tab keeps the measurement as the selected view.
        _vm.SelectedTab = _vm.Tabs[0];
        Assert.Same(tab.ViewModel, _vm.SelectedView);
    }

    [Fact]
    public async Task Starting_live_switches_to_the_live_tab()
    {
        // A measurement tab is selected...
        await _vm.StartExperiment();
        var measurementTab = _vm.Tabs.Single(t => !t.IsFixed);
        Assert.Same(measurementTab, _vm.SelectedTab);

        // ...then live starts and the Live view tab takes over.
        _experimentManager.SelectedDetection = new DefinedDetection
        {
            Name = "Acq1",
            Settings = EquipmentFixtures.Detection()
        };
        _backend = async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };

        var liveTask = _vm.EnableLive();
        Assert.True(_vm.IsLiveEnabled);
        Assert.Same(_vm.Tabs[0], _vm.SelectedTab);

        await _vm.EnableLive(); // stop live
        await liveTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(_vm.IsLiveEnabled);
    }

    [Fact]
    public async Task A_measurement_runs_in_its_own_tab_and_stays_open_afterwards()
    {
        Assert.Equal(3, _vm.Tabs.Count); // only the fixed tabs

        await _vm.StartExperiment();

        var tab = _vm.Tabs.Single(t => !t.IsFixed);
        Assert.Equal("measurement000000", tab.Header);
        Assert.NotNull(tab.Session);
        Assert.Equal(AcquisitionSessionKind.Experiment, tab.Session!.Kind);

        // The run finished: the tab is no longer running and can be closed, and the
        // slider now browses the saved frames instead of following the acquisition.
        Assert.False(tab.IsRunning);
        Assert.True(tab.IsClosable);
        Assert.False(tab.ViewModel!.IsExperimentRunning);
        Assert.False(tab.ViewModel!.IsDataStreaming);
        Assert.False(tab.ViewModel!.ShowLiveView);
        Assert.False(_vm.IsExperimentEnabled);

        Assert.True(_vm.CloseImageTab(tab));
        Assert.Equal(3, _vm.Tabs.Count); // only the fixed tabs remain
        Assert.True(tab.Session.IsDisposed);
    }

    [Fact]
    public async Task Every_measurement_gets_its_own_tab_and_session()
    {
        await _vm.StartExperiment();
        await _vm.StartExperiment();

        Assert.Equal(5, _vm.Tabs.Count); // 3 fixed + 2 measurements
        var (first, second) = (_vm.Tabs[3], _vm.Tabs[4]);
        Assert.NotSame(first, second);
        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.Equal("measurement000000", first.Header);
        Assert.Equal("measurement000001", second.Header);

        // The first run's session is untouched by the second run.
        Assert.NotSame(first.Session, second.Session);
        Assert.False(first.Session!.IsDisposed);
        Assert.False(second.Session!.IsDisposed);
    }

    [Fact]
    public async Task A_running_measurement_cannot_be_closed_and_the_button_stops_it()
    {
        _backend = async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };

        var run = _vm.StartExperiment();
        await WaitUntilAsync(() => _vm.Tabs.Count(t => !t.IsFixed) == 1 &&
                                   _vm.Tabs.Single(t => !t.IsFixed).IsRunning);

        // While the measurement runs, closing is refused...
        var tab = _vm.Tabs.Single(t => !t.IsFixed);
        Assert.False(tab.IsClosable);
        Assert.False(_vm.CloseImageTab(tab));
        Assert.Single(_vm.Tabs.Where(t => !t.IsFixed));

        // ...and the start button doubles as the stop button (no second tab is created).
        await _vm.StartExperiment();
        await run;

        Assert.Single(_vm.Tabs.Where(t => !t.IsFixed));
        Assert.False(_vm.IsExperimentEnabled);
        Assert.False(tab.IsRunning);
        Assert.True(tab.IsClosable);
        // A stopped measurement stays browsable: the slider follows the saved file.
        Assert.False(tab.ViewModel!.ShowLiveView);

        Assert.True(_vm.CloseImageTab(tab));
        Assert.Equal(3, _vm.Tabs.Count);
        Assert.True(tab.Session!.IsDisposed);
    }
}

/// <summary>
/// The tab control side of measurement tabs: the TabControl is fully data-driven over
/// the view model's Tabs collection (three fixed tabs + one closable tab per opened
/// file), the tab's close button removes the tab and disposes its session, and switching
/// tabs does not recreate the content views. Storage is a mock returning a canned
/// program, so neither the native library nor a real file is needed.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class ImageTabViewTests : IDisposable
{
    private readonly TestContainer _container;
    private readonly Mock<IImagerCommunicationManager> _comm = new();
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    private readonly ImageControlPanelViewModel _vm;

    /// <summary>What the fake backend does when a program (live or measurement) is started.</summary>
    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> _backend =
        async (w, _) => { await w.WriteAsync(new MeasurementCompletedEvent()); w.TryComplete(); };

    public ImageTabViewTests()
    {
        _container = new TestContainer(builder =>
        {
            builder.RegisterType<ImageDisplayViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
            builder.RegisterType<ImageRegionDisplayViewModel>().AsSelf().InstancePerLifetimeScope();
            // The Smart Program tab's XAML embeds a GenAIChatView, which resolves its view
            // model from App.Container when the ImageControlPanelView is constructed.
            builder.RegisterType<EquipmentWorkspace>().AsSelf().InstancePerDependency();
            builder.RegisterInstance(Mock.Of<IAnthropicChatService>()).As<IAnthropicChatService>();
            builder.Register(_ => _experimentManager).AsSelf().SingleInstance();
            builder.RegisterType<GenAIChatViewModel>().AsSelf().SingleInstance();
        });

        _comm.Setup(c => c.ExecuteMeasurementProgram(
                It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>(
                (request, writer, token) => _ = Task.Run(() => _backend(writer, token)));

        // A saved measurement file, as the real MISStorageProvider would serve it.
        _container.StorageProvider.Setup(s => s.GetImagerProgram()).Returns(SavedProgramJson());
        _container.StorageProvider.Setup(s => s.LoadMaxFrameNumber()).Returns(1);
        // The schema live acquisition reports for its single detection/detector.
        _container.StorageProvider.Setup(s => s.GetStorageSchema())
            .Returns(new List<Tuple<string, string>> { new("Acq1", "DummyCam1") });

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        _workspace = new ImagerWorkspace(
            _container.Container,
            loggerFactory.Object,
            Mock.Of<IImagerConnectionHandler>(),
            _comm.Object,
            new SmartProgramRegistry())
        {
            StorageReleaseDelay = TimeSpan.Zero
        };

        _experimentManager = new ExperimentManager(
            _container.Settings,
            _container.StageControl.Object,
            EquipmentFixtures.Workspace(),
            _container.Resolve<SmartProcessingRegisterViewModel>(),
            _container.Factory,
            new ExperimentBuilderFactory(_container.Factory));

        _vm = new ImageControlPanelViewModel(
            loggerFactory.Object,
            new ImageDisplayViewModel(),
            new FieldViewerViewModel(),
            _container.Resolve<SmartProcessingRegisterViewModel>(),
            Mock.Of<IImagerConnectionHandler>(),
            new ImageDisplayViewModelFactory(),
            _workspace,
            _experimentManager);
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _experimentManager.Dispose();
        _container.Dispose();
    }

    /// <summary>The .imag document a measurement file stores, as JSON.</summary>
    private static string SavedProgramJson()
    {
        var tree = new DoTimesElement { ElementId = "root", NTotal = 1 };
        tree.AddChild(new DetectionElement { ElementId = "d", DetectionNames = { "Acq1" } });
        var state = new FullEquipmentState
        {
            CurrentEquipment = EquipmentFixtures.Workspace(),
            CurrentProgram = new MeasurementProgram(tree,
                new Dictionary<string, DetectionParams> { ["Acq1"] = EquipmentFixtures.Detection() }),
            SmartPrograms = new List<SmartProgramModel>(),
        };
        return FullEquipmentStateSerializer.Serialize(state);
    }

    private ImageControlPanelView ShowInWindow(double width = 1280)
    {
        ScreenHelper.ScreenSize = new PixelRect(0, 0, 1920, 1080);
        ScreenHelper.Scaling = 1;
        var view = new ImageControlPanelView { DataContext = _vm };
        var window = new Window { Content = view, Width = width, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    /// <summary>A single 4x4 Mono16 frame from Acq1/DummyCam1, as the backend would send it.</summary>
    private static ChannelMessage FrameMessage() => new(
        Index: 0,
        Message: new AsyncMeasurementMessage(
            Type: "imagemessage",
            Data: new AcquiredData(
                DetectorName: "DummyCam1",
                ImageData: new byte[4 * 4 * 2],
                NCols: 4,
                NRows: 4,
                PixelFormat: ImagePixelFormat.Mono16,
                TimeStamp: 0f),
            MetaData: new AcquisitionMetaData(
                AcquisitionType: "Acq1",
                DetectionIndex: 0,
                DetectionElementId: "d",
                NImagesWithDetectionIndex: 1,
                StagePosition: new StagePosition(0, false, 0, 0, 0),
                StagePositionName: ""),
            Decision: ""));

    [AvaloniaFact]
    public void Opening_a_file_adds_a_closable_tab_next_to_the_fixed_tabs()
    {
        var view = ShowInWindow();

        _vm.OpenFileInNewTab("file:///tmp/measurements/opened.tif");
        Dispatcher.UIThread.RunJobs();

        // VM side: one closable tab, named after the file, holding a file session.
        var tab = _vm.Tabs.Single(t => !t.IsFixed);
        Assert.Equal("opened.tif", tab.Header);
        Assert.NotNull(tab.Session);
        Assert.Equal(AcquisitionSessionKind.File, tab.Session!.Kind);
        Assert.False(tab.IsRunning);
        Assert.True(tab.IsClosable);

        // View side: the three fixed tabs plus the new one, which is selected.
        var tabControl = view.FindControl<TabControl>("TabLiveFocus");
        Assert.NotNull(tabControl);
        var items = tabControl.GetVisualDescendants().OfType<TabItem>().ToList();
        Assert.Equal(4, items.Count);
        Assert.Equal(new[] { "Live view", "Field view", "Smart Program", "opened.tif" },
            items.Select(t => ((ImageTabViewModel)t.DataContext!).Header).ToArray());
        Assert.Same(tab, tabControl.SelectedItem);

        // The selected tab shows its image display view.
        var imageView = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().Single();
        Assert.Same(tab.ViewModel, imageView.DataContext);

        // The close button is visible exactly on the closable tab.
        foreach (var item in items)
        {
            var closeButton = item.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Name == "CloseButton");
            Assert.NotNull(closeButton);
            var isFixed = ((ImageTabViewModel)item.DataContext!).IsFixed;
            Assert.Equal(!isFixed, closeButton.IsVisible);
            Assert.Equal(!isFixed, closeButton.IsEnabled);
        }

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public void Close_button_removes_the_tab_and_disposes_its_session()
    {
        var view = ShowInWindow();

        _vm.OpenFileInNewTab("file:///tmp/measurements/opened.tif");
        Dispatcher.UIThread.RunJobs();
        var tab = _vm.Tabs.Single(t => !t.IsFixed);
        var session = tab.Session!;

        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;
        var newTabItem = tabControl.GetVisualDescendants().OfType<TabItem>()
            .Single(t => t.DataContext is ImageTabViewModel vm && vm.Header == "opened.tif");

        var closeButton = newTabItem.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "CloseButton");
        Assert.NotNull(closeButton);
        Assert.True(closeButton.IsVisible);

        closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(3, _vm.Tabs.Count);
        Assert.Equal(3, tabControl.GetVisualDescendants().OfType<TabItem>().Count());
        Assert.True(session.IsDisposed);

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public void The_live_tab_is_selected_at_startup_with_its_view_built()
    {
        var view = ShowInWindow();
        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;
        var liveTab = _vm.Tabs[0];

        Assert.Same(liveTab, tabControl.SelectedItem);
        Assert.Same(liveTab, _vm.SelectedTab);

        var liveView = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().SingleOrDefault();
        Assert.NotNull(liveView);
        Assert.Same(_vm.LiveView, liveView.DataContext);

        // The view subscribed to the live view model (OnDataContextChanged ran).
        var field = typeof(ImageDisplayView).GetField("imageDisplayViewM",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.Same(_vm.LiveView, field!.GetValue(liveView));

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public async Task Live_acquisition_switches_to_the_live_tab_and_displays_frames()
    {
        var view = ShowInWindow();

        // Start from a non-live tab: opening a file selects it.
        _vm.OpenFileInNewTab("file:///tmp/measurements/opened.tif");
        Dispatcher.UIThread.RunJobs();
        var fileTab = _vm.Tabs.Single(t => !t.IsFixed);
        Assert.Same(fileTab, _vm.SelectedTab);

        _experimentManager.SelectedDetection = new DefinedDetection
        {
            Name = "Acq1",
            Settings = EquipmentFixtures.Detection()
        };
        _backend = async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            await w.WriteAsync(new MeasurementDataEvent(new[] { FrameMessage() }));
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };

        var liveTask = _vm.EnableLive();
        Assert.True(_vm.IsLiveEnabled);

        // The tab strip switched to the Live view tab...
        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;
        Assert.Same(_vm.Tabs[0], _vm.SelectedTab);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(_vm.Tabs[0], tabControl.SelectedItem);

        // ...and the live frame is rendered in the live tab's view.
        var liveView = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().Single();
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return liveView.GetVisualDescendants().OfType<Image>()
                .Any(i => i.Source is WriteableBitmap bmp && bmp.Size.Width == 4 && bmp.Size.Height == 4);
        });

        await _vm.EnableLive(); // stop live
        await liveTask.WaitAsync(TimeSpan.FromSeconds(5));

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public void The_fixed_tabs_views_are_built_at_startup_even_when_not_selected()
    {
        var view = ShowInWindow();
        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;

        // Only the Live tab is selected; the Field view and Smart Program tabs are not.
        Assert.Same(_vm.Tabs[0], tabControl.SelectedItem);

        var template = (ImageTabContentTemplate)view.FindResource("ImageTabContentTemplate");
        foreach (var tab in _vm.Tabs.Where(t => t.IsFixed))
        {
            Assert.True(template.TryGetBuiltView(tab.Content, out var built),
                $"the view for the fixed tab '{tab.Header}' was not built");
            Assert.NotNull(built);
            Assert.Same(tab.Content, built!.DataContext);
        }

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public void Switching_tabs_does_not_recreate_the_content_view()
    {
        var view = ShowInWindow();

        _vm.OpenFileInNewTab("file:///tmp/measurements/opened.tif");
        Dispatcher.UIThread.RunJobs();
        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;

        var imageView = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().Single();

        // Switch to the Live view tab and back to the file tab.
        tabControl.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        tabControl.SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();

        var imageViewAfter = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().Single();
        Assert.Same(imageView, imageViewAfter);

        view.FindAncestorOfType<Window>()?.Close();
    }

    [AvaloniaFact]
    public void Clicking_any_point_of_a_tab_header_selects_it()
    {
        var failures = ClickProbeAcrossWidths(new[] { 1280.0, 1000, 900, 800, 700 });
        Assert.True(failures.Count == 0,
            "Clicks that did not select their tab:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Opens three measurement tabs on top of the three fixed ones and clicks every
    /// tab at a range of points for several window widths, so the strip is examined
    /// exactly fitting, overflowing, and wrapped to multiple rows.
    /// </summary>
    private List<string> ClickProbeAcrossWidths(IEnumerable<double> widths)
    {
        var failures = new List<string>();
        foreach (var width in widths)
        {
            var view = ShowInWindow(width);
            if (_vm.Tabs.Count == 3)
            {
                _vm.OpenFileInNewTab("file:///tmp/measurements/opened1.tif");
                _vm.OpenFileInNewTab("file:///tmp/measurements/opened2.tif");
                _vm.OpenFileInNewTab("file:///tmp/measurements/opened3.tif");
                Dispatcher.UIThread.RunJobs();
            }
            Assert.Equal(6, _vm.Tabs.Count);
            var window = view.FindAncestorOfType<Window>()!;
            var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;

            foreach (var tab in _vm.Tabs.ToList())
            {
                var item = Assert.IsType<TabItem>(tabControl.ContainerFromItem(tab));
                var topLeft = item.TranslatePoint(new Point(), window)!.Value;
                var size = item.Bounds.Size;

                var xs = new List<double> { 1, size.Width / 2, tab.IsFixed ? size.Width - 1 : size.Width - 30 };
                foreach (var x in xs)
                foreach (var y in new[] { 0.5, size.Height / 2, size.Height - 0.5 })
                {
                    // Start from a different tab so every probe is a real selection change.
                    _vm.SelectedTab = tab == _vm.Tabs[0] ? _vm.Tabs[1] : _vm.Tabs[0];
                    Dispatcher.UIThread.RunJobs();

                    var point = topLeft + new Point(x, y);
                    var topmost = window.GetVisualAt(point)?.GetType().Name ?? "null";
                    window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                    window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    if (!ReferenceEquals(_vm.SelectedTab, tab))
                        failures.Add($"window {width}: tab '{tab.Header}' at ({x:0.#},{y:0.#}) topmost={topmost}");
                }
            }

            window.Close();
        }
        return failures;
    }

    [AvaloniaFact]
    public async Task Clicking_tabs_while_live_frames_are_streaming_selects_them()
    {
        var view = ShowInWindow();
        var window = view.FindAncestorOfType<Window>()!;
        var tabControl = view.FindControl<TabControl>("TabLiveFocus")!;

        _experimentManager.SelectedDetection = new DefinedDetection
        {
            Name = "Acq1",
            Settings = EquipmentFixtures.Detection()
        };
        _backend = async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            while (!token.IsCancellationRequested)
            {
                await w.WriteAsync(new MeasurementDataEvent(new[] { FrameMessage() }));
                await Task.Delay(10, token);
            }
            w.TryComplete();
        };

        var liveTask = _vm.EnableLive();
        Dispatcher.UIThread.RunJobs();

        var liveView = tabControl.GetVisualDescendants().OfType<ImageDisplayView>().Single();
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return liveView.GetVisualDescendants().OfType<Image>()
                .Any(i => i.Source is WriteableBitmap bmp && bmp.Size.Width == 4 && bmp.Size.Height == 4);
        });

        // Zoom into the image with the mouse wheel, the way a user inspecting a
        // cell would: the image grid has no zoom/pan clamp, so the frame content
        // grows past its panel and can cover the tab strip above it.
        var imageCenter = liveView.TranslatePoint(new Point(200, 200), window)!.Value;
        for (var i = 0; i < 15; i++)
            window.MouseWheel(imageCenter, new Vector(0, 120), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        var failures = new List<string>();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            // Alternate between the Field view and Smart Program tabs.
            var target = _vm.Tabs[attempt % 2 == 0 ? 1 : 2];
            var item = Assert.IsType<TabItem>(tabControl.ContainerFromItem(target));
            var point = item.TranslatePoint(new Point(75, 10), window)!.Value;

            var topmost = window.GetVisualAt(point)?.GetType().Name ?? "null";
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            if (!ReferenceEquals(_vm.SelectedTab, target))
                failures.Add($"attempt {attempt}: '{target.Header}' not selected after click, topmost={topmost}");
        }

        await _vm.EnableLive(); // stop live
        await liveTask;

        Assert.True(failures.Count == 0,
            "Clicks that did not select their tab:\n" + string.Join("\n", failures));

        window.Close();
    }
}

/// <summary>
/// Regression test for the live button failing with "Sequence contains no matching
/// element": CompositionRoot must not install PropertiesAutowired on the panel, or
/// Autofac's property injection overwrites the constructor-set LiveView/FieldView
/// with fresh instances that no longer match the fixed tabs built from them.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class ImageControlPanelCompositionTests : IDisposable
{
    private readonly TestContainer _container;

    public ImageControlPanelCompositionTests()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        TestContainer container = null!;
        _container = container = new TestContainer(builder =>
        {
            // The registrations the real CompositionRoot uses for the panel's dependencies.
            builder.RegisterType<ImageDisplayViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
            builder.RegisterType<FieldViewerViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
            builder.RegisterInstance(loggerFactory.Object).As<ILoggerFactory>();
            builder.RegisterInstance(Mock.Of<IImagerConnectionHandler>()).As<IImagerConnectionHandler>();
            builder.RegisterInstance(Mock.Of<IImagerCommunicationManager>()).As<IImagerCommunicationManager>();
            builder.RegisterType<ImageDisplayViewModelFactory>().As<IImageDisplayViewModelFactory>().SingleInstance();
            builder.RegisterType<ImagerWorkspace>().AsSelf().SingleInstance();
            builder.Register(_ => new ExperimentManager(
                container.Settings,
                container.StageControl.Object,
                EquipmentFixtures.Workspace(),
                container.Resolve<SmartProcessingRegisterViewModel>(),
                container.Factory,
                new ExperimentBuilderFactory(container.Factory))).AsSelf().SingleInstance();

            // As fixed in CompositionRoot: no PropertiesAutowired on the panel itself.
            builder.RegisterType<ImageControlPanelViewModel>().AsSelf();
        });
    }

    public void Dispose() => _container.Dispose();

    [Fact]
    public void ResolvedPanelMatchesItsFixedTabs()
    {
        var panel = _container.Resolve<ImageControlPanelViewModel>();

        // The fixed tabs must wrap the same instances the panel talks to, or
        // Workspace_SessionStarted's Tabs.First(t => t.Content == LiveView) throws.
        Assert.Same(panel.LiveView, panel.Tabs[0].Content);
        Assert.Same(panel.FieldView, panel.Tabs[1].Content);
        Assert.Same(panel.Tabs[0], panel.Tabs.First(t => t.Content == panel.LiveView));
    }
}

/// <summary>
/// Loads the real MainView with the full composition root and clicks the center
/// panel's tab headers with real pointer input, mapping which clicks fail to
/// select their tab. In isolation the panel's strip selects everywhere; this
/// test detects elements of the surrounding MainView that swallow the clicks.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class MainViewTabClickTests : IDisposable
{
    private readonly IContainer? _previousContainer;

    public MainViewTabClickTests()
    {
        _previousContainer = App.Container;
        App.Container = CompositionRoot.BuildContainer();
    }

    public void Dispose()
    {
        App.Container = _previousContainer;
    }

    [AvaloniaFact]
    public void Clicking_tabs_of_the_center_panel_selects_them()
    {
        var failures = new List<string>();
        foreach (var width in new[] { 1920.0, 1600, 1200, 1000 })
            failures.AddRange(MainViewClickProbe(width));
        Assert.True(failures.Count == 0,
            "Clicks that did not select their tab:\n" + string.Join("\n", failures));
    }

    /// <summary>Shows the full MainView with six tabs and clicks each tab across its
    /// header at the given window width.</summary>
    private List<string> MainViewClickProbe(double width)
    {
        var failures = new List<string>();
        var view = new MainView();
        var window = new Window { Content = view, Width = width, Height = 1080 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var panelView = view.FindControl<ImageControlPanelView>("ImageControlView");
        Assert.NotNull(panelView);
        var tabControl = panelView.FindControl<TabControl>("TabLiveFocus");
        Assert.NotNull(tabControl);
        var panelVm = Assert.IsType<ImageControlPanelViewModel>(tabControl.DataContext);

        // Three measurement tabs on top of the fixed three.
        if (panelVm.Tabs.Count == 3)
        {
            for (var i = 1; i <= 3; i++)
                panelVm.Tabs.Add(new ImageTabViewModel($"measurement{i}", new ImageDisplayViewModel()));
            Dispatcher.UIThread.RunJobs();
        }

        foreach (var tab in panelVm.Tabs.ToList())
        {
            var item = Assert.IsType<TabItem>(tabControl.ContainerFromItem(tab));
            var topLeft = item.TranslatePoint(new Point(), window)!.Value;
            var size = item.Bounds.Size;

            var xs = new List<double> { 1, size.Width / 2, tab.IsFixed ? size.Width - 1 : size.Width - 30 };
            foreach (var x in xs)
            foreach (var y in new[] { 0.5, size.Height / 2, size.Height - 0.5 })
            {
                // Start from a different tab so every probe is a real selection change.
                panelVm.SelectedTab = tab == panelVm.Tabs[0] ? panelVm.Tabs[1] : panelVm.Tabs[0];
                Dispatcher.UIThread.RunJobs();

                var point = topLeft + new Point(x, y);
                var topmost = window.GetVisualAt(point)?.GetType().Name ?? "null";
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                if (!ReferenceEquals(panelVm.SelectedTab, tab))
                    failures.Add($"window {width}: '{tab.Header}' at ({x:0.#},{y:0.#}) topmost={topmost}");
            }
        }

        window.Close();
        return failures;
    }
}
