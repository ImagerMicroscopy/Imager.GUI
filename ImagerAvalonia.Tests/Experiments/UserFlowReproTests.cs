using Autofac;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using ImagerAvalonia.Views;
using Xunit;

namespace ImagerAvalonia.Tests.Experiments;

/// <summary>
/// User-flow tests for composing measurement programs and specifying an output
/// folder, running against the real composition root.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class UserFlowReproTests : IDisposable
{
    private readonly IContainer? _previous;
    private readonly IContainer _container;

    public UserFlowReproTests()
    {
        _container = CompositionRoot.BuildContainer();
        _previous = App.Container;
        App.SetTestContainer(_container);
    }

    public void Dispose()
    {
        App.SetTestContainer(_previous!);
        // Note: disposing the container throws NotImplementedException from
        // ObservableLoggerProvider.Dispose - left undisposed here on purpose.
    }

    private static T ShowInWindow<T>(T control) where T : Control
    {
        var window = new Window { Content = control, Width = 500, Height = 700 };
        window.Show();
        return control;
    }

    [Fact]
    public void App_starts_with_a_default_selected_experiment()
    {
        var mainVM = _container.Resolve<MainViewModel>();

        var exp = Assert.Single(mainVM.Experiments);
        Assert.Same(exp, mainVM.SelectedExperiment);
    }

    [Fact]
    public void Adding_an_experiment_selects_it_immediately()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var first = mainVM.SelectedExperiment!;

        mainVM.AddExperiment();

        Assert.Equal(2, mainVM.Experiments.Count);
        Assert.Same(mainVM.Experiments[1], mainVM.SelectedExperiment);
        Assert.NotSame(first, mainVM.SelectedExperiment);
    }

    [Fact]
    public void User_can_compose_a_program_from_primitives()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        exp.AddElementCommand(ExperimentElementType.DoTimes);
        exp.AddElementCommand(ExperimentElementType.Detection);
        exp.AddElementCommand(ExperimentElementType.WaitForTime);

        Assert.Equal(3, exp.Root.Children.Count);

        var tree = exp.GetMeasurementElement();
        Assert.Equal("dotimes", tree.ElementType);
        Assert.Equal(3, tree.Elements.Count);
        Assert.Equal("dotimes", tree.Elements[0].ElementType);
        Assert.Equal("detection", tree.Elements[1].ElementType);
        Assert.Equal("wait", tree.Elements[2].ElementType);
    }

    [Fact]
    public void User_can_specify_an_output_folder_and_get_a_storage_path()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "imager-repro-" + Guid.NewGuid().ToString("N"))).FullName;

        exp.Root.OutputFolder = dir;
        exp.Root.FileName = "exp";

        var path = exp.GetStoragePath();

        Assert.StartsWith(dir, path);
        Assert.EndsWith(".tif", path);
    }

    [Fact]
    public void Storage_path_is_not_validated_when_storage_is_disabled()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;
        var manager = _container.Resolve<ExperimentManager>();

        // No output folder or file name set - with saving on, this must throw.
        exp.Root.IsExperimentStorageEnabled = false;
        Assert.Equal("Unsaved experiment", manager.GetStoragePath());

        // A configured base name is still used to title the run's tab.
        exp.Root.FileName = "exp";
        Assert.Equal("exp", manager.GetStoragePath());

        exp.Root.IsExperimentStorageEnabled = true;
        Assert.Throws<Exception>(() => manager.GetStoragePath());
    }

    [AvaloniaFact]
    public void Open_button_on_root_panel_has_a_working_command()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var view = ShowInWindow(new RootPanelView { DataContext = exp.Root });

        var openButton = view.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content as string == "Open");

        Assert.NotNull(openButton.Command);
        Assert.True(openButton.Command!.CanExecute(null));
    }

    [Fact]
    public void RootNode_notifies_when_the_storage_service_output_folder_changes()
    {
        var service = new ExperimentStorageService();
        var root = new RootNode { StorageService = service };

        var changed = new List<string?>();
        root.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.OutputFolder = "/some/dir";

        Assert.Contains(nameof(RootNode.OutputFolder), changed);
    }

    [AvaloniaFact]
    public void Picking_an_output_folder_updates_the_save_path_display()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var view = ShowInWindow(new RootPanelView { DataContext = exp.Root });

        var savePathBox = view.GetVisualDescendants()
            .OfType<TextBox>()
            .First(t => t.Watermark == "Save path");

        // Simulate what the folder picker does: set the folder on the storage service.
        exp.Root.StorageService!.OutputFolder = "/some/dir";

        Assert.Equal("/some/dir", savePathBox.Text);
    }

    [AvaloniaFact]
    public void Added_elements_appear_in_the_experiment_tree()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var view = ShowInWindow(new ExperimentalPanelViewControl { DataContext = exp });

        exp.AddElementCommand(ExperimentElementType.Detection);
        exp.AddElementCommand(ExperimentElementType.WaitForTime);

        var tree = view.FindControl<TreeView>("ExperimentTreeView");
        Assert.NotNull(tree);
        Assert.Equal(2, exp.Root.Children.Count);

        // The tree must have realized a container for the root and its two children.
        var treeItems = view.GetVisualDescendants().OfType<TreeViewItem>().ToList();
        Assert.Equal(3, treeItems.Count);
    }

    [AvaloniaFact]
    public void Resizing_the_divider_changes_the_program_tree_height()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var view = ShowInWindow(new ExperimentalPanelViewControl { DataContext = exp });

        var tree = view.FindControl<TreeView>("ExperimentTreeView");
        Assert.NotNull(tree);
        // Layout rounding can shave a pixel off the row height, hence the tolerance.
        AssertHeight(tree.Bounds.Height, 400);

        // Shrinking and growing via the divider's resize path both take effect.
        view.ResizeTree(250);
        Dispatcher.UIThread.RunJobs();
        AssertHeight(tree.Bounds.Height, 250);

        view.ResizeTree(600);
        Dispatcher.UIThread.RunJobs();
        AssertHeight(tree.Bounds.Height, 600);

        // The resize is clamped so the tree can never disappear (one pixel
        // of layout rounding is tolerated around the 120px minimum).
        view.ResizeTree(10);
        Dispatcher.UIThread.RunJobs();
        Assert.True(tree.Bounds.Height >= 118,
            $"Tree height should be clamped to a minimum, got {tree.Bounds.Height}.");
    }

    private static void AssertHeight(double actual, double expected)
    {
        Assert.True(Math.Abs(actual - expected) <= 2,
            $"Expected tree height near {expected}, got {actual}.");
    }

    [AvaloniaFact]
    public void Storage_settings_stay_available_whichever_element_is_selected()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        // The storage panel is pinned at the bottom of the Experiments pane,
        // outside the scrollable element tree and settings area.
        var view = ShowInWindow(new MainView { DataContext = mainVM });

        // The root is selected initially: the storage panel must already be visible.
        AssertSavePathBoxVisible(view);

        exp.AddElementCommand(ExperimentElementType.Detection);
        exp.SelectedTreeItem = exp.Root.Children[0];

        // Selecting another element must not hide the storage panel.
        AssertSavePathBoxVisible(view);
    }

    private static void AssertSavePathBoxVisible(MainView view)
    {
        _ = view.GetVisualDescendants()
            .OfType<TextBox>()
            .First(t => t.Watermark == "Save path");
    }

    [AvaloniaFact]
    public void Selecting_the_root_shows_no_editable_element_settings()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        var exp = mainVM.SelectedExperiment!;

        var view = ShowInWindow(new ExperimentalPanelViewControl { DataContext = exp });

        // The root carries no editable settings anymore; the settings area just says so.
        var hint = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(t => t.Text?.StartsWith("The root element cannot be modified") == true);

        Assert.NotNull(hint);
    }
}
