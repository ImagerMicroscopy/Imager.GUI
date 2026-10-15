using Autofac;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Xunit;

namespace ImagerAvalonia.Tests.Experiments;

/// <summary>
/// Node-level tree editing: the "+" and "x" actions on the tree view cards
/// add and delete elements directly on the node they belong to.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class TreeEditingTests : IDisposable
{
    private readonly IContainer? _previous;
    private readonly IContainer _container;

    public TreeEditingTests()
    {
        _container = CompositionRoot.BuildContainer();
        _previous = App.Container;
        App.SetTestContainer(_container);
    }

    public void Dispose()
    {
        App.SetTestContainer(_previous!);
    }

    private ExperimentalPanelViewModel Panel()
    {
        var mainVM = _container.Resolve<MainViewModel>();
        return mainVM.SelectedExperiment!;
    }

    [Fact]
    public void Adding_a_child_element_puts_it_inside_the_clicked_node()
    {
        var panel = Panel();
        var root = panel.Root;

        root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);

        var dotimes = Assert.IsType<DoTimesViewModel>(Assert.Single(root.Children));
        Assert.Same(root, dotimes.Parent);
    }

    [Fact]
    public void The_newly_added_element_becomes_selected()
    {
        var panel = Panel();

        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.WaitForTime);

        var wait = Assert.Single(panel.Root.Children);
        Assert.Same(wait, panel.SelectedTreeItem);
    }

    [Fact]
    public void Adding_inside_a_loop_nests_the_element_under_the_loop()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);
        var dotimes = Assert.IsType<DoTimesViewModel>(Assert.Single(panel.Root.Children));

        dotimes.AddChildElementCommand.Execute(ExperimentElementType.Detection);

        var detection = Assert.Single(dotimes.Children);
        Assert.Same(dotimes, detection.Parent);
        Assert.Equal("detection", detection.ToModel().ElementType);
    }

    [Fact]
    public void Deleting_an_element_removes_it_and_selects_its_parent()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);
        var dotimes = Assert.IsType<DoTimesViewModel>(Assert.Single(panel.Root.Children));
        dotimes.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var detection = Assert.Single(dotimes.Children);

        detection.DeleteElementCommand.Execute(null);

        Assert.Empty(dotimes.Children);
        Assert.Same(dotimes, panel.SelectedTreeItem);
    }

    [Fact]
    public void Leaf_elements_do_not_accept_children()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var detection = Assert.Single(panel.Root.Children);

        detection.AddChildElementCommand.Execute(ExperimentElementType.WaitForTime);

        Assert.Empty(detection.Children);
    }

    [Fact]
    public void New_elements_inherit_the_equipment_availability_of_their_parent()
    {
        var panel = Panel();
        panel.Root.AreStagesAvailable = false;
        panel.Root.AreSourcesAvailable = false;

        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var detection = Assert.Single(panel.Root.Children);

        Assert.False(detection.AreStagesAvailable);
        Assert.False(detection.AreSourcesAvailable);
    }
}
