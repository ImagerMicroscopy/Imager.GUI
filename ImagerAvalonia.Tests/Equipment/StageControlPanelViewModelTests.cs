using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using Xunit;

namespace ImagerAvalonia.Tests.Equipment;

/// <summary>
/// Stage control panel movement math: each arrow applies the configured step
/// to the current position returned by the stage backend, negative steps are
/// clamped to zero, and stage availability is pushed after initialization
/// rather than only read at construction time. The backend is a fake
/// IStageControl with no test container needed.
/// </summary>
public sealed class StageControlPanelViewModelTests
{
    private sealed class FakeStageControl : IStageControl
    {
        public XYStagePosition? PositionToRead { get; set; }
        public List<XYStagePosition> SetPositions { get; } = new();
        public bool StageAvailable { get; set; }

        public bool IsStageAvailable => StageAvailable;
        public XYStagePosition PinnedPosition { get; set; } = IStageControl.DefaultStagePosition;
        public Stages AvailableStages { get; } = new();
        public string StageName { get; set; } = string.Empty;

        public Task InitializeStageInfo() => Task.CompletedTask;
        public XYStagePosition? ReadStagePosition() => PositionToRead;
        public void SetStagePosition(XYStagePosition stagePosition) => SetPositions.Add(stagePosition);
    }

    private static XYStagePosition Position(double x, double y, double z) =>
        new(hardwareautofocusoffset: 0, x, y, z, usinghardwareaf: false, name: "test");

    private static StageControlPanelViewModel CreateViewModel(FakeStageControl stage) =>
        new(stage);

    [Fact]
    public void MoveNorth_AddsPositiveYStep()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveNorth();

        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(1, moved.Coordinates.x);
        Assert.Equal(7, moved.Coordinates.y);
        Assert.Equal(3, moved.Coordinates.z);
    }

    [Fact]
    public void MoveSouth_SubtractsYStep()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveSouth();

        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(-3, moved.Coordinates.y);
    }

    [Fact]
    public void MoveEast_AddsXStep()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveEast();

        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(6, moved.Coordinates.x);
    }

    [Fact]
    public void MoveWest_SubtractsXStep()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveWest();

        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(-4, moved.Coordinates.x);
    }

    [Fact]
    public void MoveUpDown_AppliesZStepWithSign()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);
        vm.ZStep = 0.5;

        vm.MoveUp();

        var up = Assert.Single(stage.SetPositions);
        Assert.Equal(3.5, up.Coordinates.z);

        stage.SetPositions.Clear();
        vm.MoveDown();

        var down = Assert.Single(stage.SetPositions);
        Assert.Equal(2.5, down.Coordinates.z);
    }

    [Fact]
    public void Move_WhenNoStagePosition_DoesNotSetPosition()
    {
        var stage = new FakeStageControl { PositionToRead = null };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveNorth();
        vm.MoveUp();

        Assert.Empty(stage.SetPositions);
    }

    [Fact]
    public void NegativeStep_IsClampedToZero()
    {
        var stage = new FakeStageControl { PositionToRead = Position(1, 2, 3) };
        var vm = CreateViewModel(stage);

        vm.XYStep = -5;
        vm.MoveEast();

        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(1, moved.Coordinates.x);

        stage.SetPositions.Clear();
        vm.ZStep = -0.5;
        vm.MoveUp();

        var up = Assert.Single(stage.SetPositions);
        Assert.Equal(3, up.Coordinates.z);
    }

    [Fact]
    public void MoveStage_DoesNotMutateThePositionReturnedByRead()
    {
        // ReadStagePosition may hand out shared state; the panel must build a
        // new position rather than mutate the one it was given.
        var original = Position(1, 2, 3);
        var stage = new FakeStageControl { PositionToRead = original };
        var vm = CreateViewModel(stage);
        vm.XYStep = 5;

        vm.MoveNorth();

        Assert.Equal(2, original.Coordinates.y);
        var moved = Assert.Single(stage.SetPositions);
        Assert.Equal(7, moved.Coordinates.y);
    }

    [Fact]
    public void RefreshStageAvailability_UpdatesFromController()
    {
        var stage = new FakeStageControl { StageAvailable = false };
        var vm = CreateViewModel(stage);
        Assert.False(vm.IsStageAvailable);

        stage.StageAvailable = true;
        vm.RefreshStageAvailability();

        Assert.True(vm.IsStageAvailable);
    }

    [Fact]
    public void Constructor_ReflectsInitialAvailability()
    {
        var stage = new FakeStageControl { StageAvailable = true };
        var vm = CreateViewModel(stage);

        Assert.True(vm.IsStageAvailable);
    }
}
