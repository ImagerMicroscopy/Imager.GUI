using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Xunit;

namespace ImagerAvalonia.Tests.Experiments;

[Collection(AppContainerCollection.Name)]
public sealed class ExperimentBuilderTests : IDisposable
{
    private readonly TestContainer _container = new();
    private readonly ExperimentBuilder _builder;
    private readonly RootNode _root = new();

    public ExperimentBuilderTests()
    {
        _container.AddAcquisitions("Acq1");
        _builder = new ExperimentBuilder(_container.Factory);
    }

    public void Dispose() => _container.Dispose();

    private static string Types(MeasurementElementViewModel parent) =>
        string.Join(",", parent.Children.Select(c => c.GetType().Name.Replace("ViewModel", "")));

    // ---------- Add / remove ----------

    [Theory]
    [InlineData(ExperimentElementType.Detection, typeof(DetectionElementViewModel))]
    [InlineData(ExperimentElementType.DoTimes, typeof(DoTimesViewModel))]
    [InlineData(ExperimentElementType.RelativeStageLoop, typeof(RelStageViewModel))]
    [InlineData(ExperimentElementType.StageLoop, typeof(StageLoopViewModel))]
    [InlineData(ExperimentElementType.WaitForTime, typeof(WaitViewModel))]
    [InlineData(ExperimentElementType.TimeLapse, typeof(TimeLapseViewModel))]
    [InlineData(ExperimentElementType.Irradiation, typeof(IrradiationPanelViewModel))]
    [InlineData(ExperimentElementType.UpdateAcquisition, typeof(UpdateAcquisitionViewModel))]
    public void Add_node_creates_the_right_view_model_under_the_parent(ExperimentElementType type, Type expected)
    {
        var node = _builder.AddNode(type, _root);

        Assert.IsType(expected, node);
        Assert.Same(_root, node.Parent);
        Assert.Same(node, Assert.Single(_root.Children));
    }

    [Fact]
    public void Add_node_under_a_leaf_element_is_rejected()
    {
        var wait = _builder.AddNode(ExperimentElementType.WaitForTime, _root);

        Assert.Throws<InvalidOperationException>(() => _builder.AddNode(ExperimentElementType.Detection, wait));
        Assert.Empty(wait.Children);
    }

    [Fact]
    public void Remove_node_detaches_and_disposes_it()
    {
        var loop = _builder.AddNode(ExperimentElementType.DoTimes, _root);
        var child = _builder.AddNode(ExperimentElementType.WaitForTime, loop);
        var deleted = new List<MeasurementElementViewModel?>();
        loop.OnNodeDeleted += (_, n) => deleted.Add(n);
        child.OnNodeDeleted += (_, n) => deleted.Add(n);

        _builder.RemoveNode(loop);

        Assert.Empty(_root.Children);
        Assert.Equal(new MeasurementElementViewModel?[] { loop, child }, deleted);
    }

    // ---------- Move ----------

    private (MeasurementElementViewModel a, MeasurementElementViewModel b, MeasurementElementViewModel c) ThreeWaits()
    {
        var a = _builder.AddNode(ExperimentElementType.WaitForTime, _root);
        var b = _builder.AddNode(ExperimentElementType.WaitForTime, _root);
        var c = _builder.AddNode(ExperimentElementType.WaitForTime, _root);
        return (a, b, c);
    }

    [Fact]
    public void Move_within_the_same_parent_to_the_front()
    {
        var (a, b, c) = ThreeWaits();

        _builder.MoveNode(c, _root, 0);

        Assert.Equal(new[] { c, a, b }, _root.Children);
    }

    [Fact]
    public void Move_within_the_same_parent_to_the_end_keeps_the_node()
    {
        // Regression: the index was checked before removal, so moving to Count removed the
        // node and then Insert threw, losing it from the tree.
        var (a, b, c) = ThreeWaits();

        _builder.MoveNode(a, _root, 2);

        Assert.Equal(new[] { b, c, a }, _root.Children);
        Assert.Same(_root, a.Parent);
    }

    [Fact]
    public void Move_past_the_end_is_rejected_without_changing_the_tree()
    {
        var (a, b, c) = ThreeWaits();

        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.MoveNode(a, _root, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.MoveNode(a, _root, -1));

        Assert.Equal(new[] { a, b, c }, _root.Children);
    }

    [Fact]
    public void Move_into_another_container()
    {
        var loop = _builder.AddNode(ExperimentElementType.DoTimes, _root);
        var inner = _builder.AddNode(ExperimentElementType.Detection, loop);
        var wait = _builder.AddNode(ExperimentElementType.WaitForTime, _root);

        _builder.MoveNode(wait, loop, 0);

        Assert.Equal(new[] { loop }, _root.Children);
        Assert.Equal(new[] { wait, inner }, loop.Children);
        Assert.Same(loop, wait.Parent);
    }

    [Fact]
    public void Move_into_itself_or_a_descendant_is_rejected()
    {
        var outer = _builder.AddNode(ExperimentElementType.DoTimes, _root);
        var middle = _builder.AddNode(ExperimentElementType.TimeLapse, outer);
        var inner = _builder.AddNode(ExperimentElementType.DoTimes, middle);

        Assert.Throws<InvalidOperationException>(() => _builder.MoveNode(outer, outer, 0));
        Assert.Throws<InvalidOperationException>(() => _builder.MoveNode(outer, middle, 0));
        Assert.Throws<InvalidOperationException>(() => _builder.MoveNode(outer, inner, 0));

        Assert.Same(_root, outer.Parent);
        Assert.Same(middle, Assert.Single(outer.Children));
    }

    [Fact]
    public void Move_under_a_leaf_element_is_rejected()
    {
        var (a, b, _) = ThreeWaits();

        Assert.Throws<InvalidOperationException>(() => _builder.MoveNode(a, b, 0));
        Assert.Same(_root, a.Parent);
    }

    // ---------- Build ----------

    [Fact]
    public void Build_measurement_program_mirrors_the_tree()
    {
        var loop = (DoTimesViewModel)_builder.AddNode(ExperimentElementType.DoTimes, _root);
        loop.NumRepeats = 3;
        _builder.AddNode(ExperimentElementType.Detection, loop);
        var wait = (WaitViewModel)_builder.AddNode(ExperimentElementType.WaitForTime, _root);
        wait.WaitPeriod = 1.5;

        var program = _builder.BuildMeasurementProgram(_root);

        var root = Assert.IsType<DoTimesElement>(program);
        Assert.Equal(1, root.NTotal);
        var builtLoop = Assert.IsType<DoTimesElement>(root.Elements[0]);
        Assert.Equal(3, builtLoop.NTotal);
        Assert.Equal(new[] { "Acq1" }, Assert.IsType<DetectionElement>(Assert.Single(builtLoop.Elements)).DetectionNames);
        Assert.Equal(1.5, Assert.IsType<WaitElement>(root.Elements[1]).Duration);
        Assert.Equal(3, program.CountTotalDetections());
        Assert.Same(program, _builder.MeasurementElement);
    }

    [Fact]
    public void Build_requires_a_root() =>
        Assert.Throws<ArgumentNullException>(() => _builder.BuildMeasurementProgram(null!));
}
