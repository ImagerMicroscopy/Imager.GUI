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

    [Fact]
    public void Moving_a_node_inside_a_container_appends_it_at_the_end()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var dotimes = panel.Root.Children[0];
        var detection = panel.Root.Children[1];

        panel.Root.Builder!.MoveNode(detection, dotimes, dotimes.Children.Count);

        Assert.Same(detection, Assert.Single(dotimes.Children));
        Assert.Same(dotimes, detection.Parent);
        Assert.Same(dotimes, Assert.Single(panel.Root.Children));
    }

    [Fact]
    public void Moving_a_node_before_a_sibling_places_it_immediately_before_the_target()
    {
        var panel = Panel();
        for (var i = 0; i < 3; i++)
            panel.Root.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var d1 = panel.Root.Children[0];
        var d2 = panel.Root.Children[1];
        var d3 = panel.Root.Children[2];

        // The index is computed against the list after the dragged node is
        // removed, so "before d1" inserts at index 0.
        panel.Root.Builder!.MoveNode(d3, panel.Root, panel.Root.Children.IndexOf(d1));

        Assert.Equal(new[] { d3, d1, d2 }, panel.Root.Children);
        Assert.Same(panel.Root, d3.Parent);
    }

    [Fact]
    public void Moving_a_node_before_a_sibling_that_follows_it_shifts_the_index_down()
    {
        var panel = Panel();
        for (var i = 0; i < 3; i++)
            panel.Root.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var d1 = panel.Root.Children[0];
        var d2 = panel.Root.Children[1];
        var d3 = panel.Root.Children[2];

        // d1 sits before d3; inserting "before d3" against the post-removal
        // list means index 1, landing between d2 and d3.
        panel.Root.Builder!.MoveNode(d1, panel.Root, 1);

        Assert.Equal(new[] { d2, d1, d3 }, panel.Root.Children);
    }

    [Fact]
    public void Moving_a_node_that_already_lives_in_the_target_container_moves_it_to_the_end()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);
        var dotimes = panel.Root.Children[0];
        dotimes.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        dotimes.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var first = dotimes.Children[0];
        var second = dotimes.Children[1];

        // Append-at-end drop onto a container that already holds the dragged
        // node: the post-removal end index is Count - 1.
        panel.Root.Builder!.MoveNode(first, dotimes, dotimes.Children.Count - 1);

        Assert.Equal(new[] { second, first }, dotimes.Children);
        Assert.Same(dotimes, first.Parent);
    }

    [Fact]
    public void Moving_a_node_into_its_own_descendant_is_rejected()
    {
        var panel = Panel();
        panel.Root.AddChildElementCommand.Execute(ExperimentElementType.DoTimes);
        var dotimes = panel.Root.Children[0];
        dotimes.AddChildElementCommand.Execute(ExperimentElementType.Detection);
        var detection = Assert.Single(dotimes.Children);

        Assert.Throws<InvalidOperationException>(
            () => panel.Root.Builder!.MoveNode(dotimes, detection, 0));
    }
}
