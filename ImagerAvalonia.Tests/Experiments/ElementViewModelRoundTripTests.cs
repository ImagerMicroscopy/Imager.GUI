using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Experiments;

/// <summary>
/// Model -> view model (MeasurementElementViewModelFactory.Build, as used by project load)
/// -> model (Traverse, as used by save/run) must not lose anything.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class ElementViewModelRoundTripTests : IDisposable
{
    private readonly TestContainer _container = new();
    private readonly IReadOnlyDictionary<string, AcquisitionSettingsViewModel> _nameMap;

    public ElementViewModelRoundTripTests()
    {
        _nameMap = _container.AddAcquisitions("Acq1", "Acq2");
    }

    public void Dispose() => _container.Dispose();

    private LoadContext Context(IReadOnlyDictionary<string, AcquisitionSettingsViewModel>? nameMap = null) => new()
    {
        Settings = _container.Settings,
        StageControl = _container.StageControl.Object,
        EquipmentWorkspace = EquipmentFixtures.Workspace(),
        ExperimentManager = null!,
        AcquisitionNameMap = nameMap ?? _nameMap,
    };

    private static string Id() => Guid.NewGuid().ToString();

    private MeasurementElementBase RoundTrip(MeasurementElementBase model) =>
        MeasurementElementViewModelFactory.Build(model, Context()).Traverse();

    private static void AssertSameJson(MeasurementElementBase expected, MeasurementElementBase actual)
    {
        var e = JToken.Parse(MeasurementSerializer.Serialize(expected));
        var a = JToken.Parse(MeasurementSerializer.Serialize(actual));
        Assert.True(JToken.DeepEquals(e, a), $"expected:\n{e}\nactual:\n{a}");
    }

    [Fact]
    public void Detection_round_trips()
    {
        var model = new DetectionElement { ElementId = Id(), DetectionNames = { "Acq2" } };
        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Detection_with_unknown_acquisition_enables_nothing()
    {
        var result = (DetectionElement)RoundTrip(new DetectionElement { ElementId = Id(), DetectionNames = { "Gone" } });

        Assert.Empty(result.DetectionNames);
    }

    [Fact]
    public void Do_times_round_trips()
    {
        var model = new DoTimesElement { ElementId = Id(), NTotal = 7 };
        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Time_lapse_round_trips()
    {
        var model = new TimeLapseElement { ElementId = Id(), NTotal = 12, TimeDelta = 0.25 };
        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Wait_round_trips()
    {
        var model = new WaitElement { ElementId = Id(), Duration = 3.5 };
        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Relative_stage_loop_round_trips()
    {
        var model = new RelativeStageLoopElement
        {
            ElementId = Id(),
            StageName = "Dummy stage",
            Params = new RelativeStageLoopParams
            {
                AdditionalPlanesX = new() { 1, 2 },
                AdditionalPlanesY = new() { 3, 0 },
                AdditionalPlanesZ = new() { 0, 4 },
                DeltaX = 0.5, DeltaY = 1.5, DeltaZ = 2.25,
                ReturnToStartingPosition = true,
            }
        };

        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Relative_stage_loop_round_trips_through_a_saved_file()
    {
        // The project-load path: JSON -> model -> view model -> model.
        var model = new RelativeStageLoopElement
        {
            ElementId = Id(),
            StageName = "Dummy stage",
            Params = new RelativeStageLoopParams { AdditionalPlanesX = new() { 2, 5 } }
        };
        var loaded = MeasurementSerializer.Deserialize<MeasurementElementBase>(MeasurementSerializer.Serialize(model));

        var result = (RelativeStageLoopElement)RoundTrip(loaded);

        Assert.Equal(new[] { 2, 5 }, result.Params.AdditionalPlanesX);
    }

    [Fact]
    public void Stage_loop_round_trips()
    {
        var model = new StageLoopElement
        {
            ElementId = Id(),
            StageName = "Dummy stage",
            Positions = { new XYStagePosition(0, 1, 2, 3, false, "p1"), new XYStagePosition(0.5, 4, 5, 6, true, "p2") }
        };

        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Irradiation_round_trips()
    {
        var model = new IrradiationElement
        {
            ElementId = Id(),
            Duration = 0.75,
            Irradiation =
            {
                new IrradiationConfig
                {
                    EquipmentName = "hello", LightSourceName = "ls",
                    LightSourceChannel = { "ch2" }, LightSourcePower = { 60 },
                }
            }
        };

        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Update_acquisition_round_trips_its_selection()
    {
        // Used to throw from SmartProgramBindings[0] and had no LoadFromModel, so the
        // saved selection came back as the first acquisition.
        var model = new UpdateAcquisition { ElementId = Id(), AcquisitionTypeName = "Acq2", DetectionName = "Acq2" };

        AssertSameJson(model, RoundTrip(model));
    }

    [Fact]
    public void Update_acquisition_follows_a_renamed_acquisition()
    {
        var renamed = _container.AddAcquisitions("Acq2"); // becomes "Acq2 (2)"
        var model = new UpdateAcquisition { ElementId = Id(), AcquisitionTypeName = "Acq2", DetectionName = "Acq2" };

        var result = (UpdateAcquisition)MeasurementElementViewModelFactory.Build(model, Context(renamed)).Traverse();

        Assert.Equal("Acq2 (2)", result.AcquisitionTypeName);
    }

    [Fact]
    public void Nested_tree_round_trips()
    {
        var tree = new DoTimesElement { ElementId = Id(), NTotal = 2 };
        var timeLapse = new TimeLapseElement { ElementId = Id(), NTotal = 3, TimeDelta = 1 };
        timeLapse.AddChild(new DetectionElement { ElementId = Id(), DetectionNames = { "Acq1", "Acq2" } });
        timeLapse.AddChild(new WaitElement { ElementId = Id(), Duration = 1 });
        tree.AddChild(timeLapse);
        tree.AddChild(new DetectionElement { ElementId = Id(), DetectionNames = { "Acq1" } });

        var vm = MeasurementElementViewModelFactory.Build(tree, Context());

        Assert.Same(vm, vm.Children[0].Parent);
        AssertSameJson(tree, vm.Traverse());
    }

    [Fact]
    public void Build_rejects_a_model_of_the_wrong_type_for_the_view_model()
    {
        var vm = new DoTimesViewModel(_container.Settings);

        Assert.Throws<ArgumentException>(() => vm.LoadFromModel(new WaitElement(), Context()));
    }

    [Fact]
    public void Find_by_element_id_searches_the_subtree()
    {
        var childId = Guid.NewGuid();
        var tree = new DoTimesElement { ElementId = Id(), NTotal = 1 };
        tree.AddChild(new WaitElement { ElementId = childId.ToString() });

        var vm = MeasurementElementViewModelFactory.Build(tree, Context());

        Assert.IsType<WaitViewModel>(vm.FindByElementId(childId));
        Assert.Null(vm.FindByElementId(Guid.NewGuid()));
    }

    [Fact]
    public void Unknown_smart_program_binding_stays_pending_and_is_reported()
    {
        var programId = Guid.NewGuid();
        var model = new DoTimesElement { ElementId = Id(), NTotal = 1, SmartProgramId = programId.ToString() };

        var vm = MeasurementElementViewModelFactory.Build(model, Context());

        Assert.Equal(programId, vm.PendingSmartProgramId);
        Assert.Equal(programId, Assert.Single(vm.GetUnresolvedSmartProgramBindings()).SmartProgramId);
    }
}

/// <summary>Individual view model behaviour not covered by the round trips.</summary>
[Collection(AppContainerCollection.Name)]
public sealed class ElementViewModelBehaviourTests : IDisposable
{
    private readonly TestContainer _container = new();

    public void Dispose() => _container.Dispose();

    [Fact]
    public void Irradiation_and_update_acquisition_can_be_created_without_acquisitions()
    {
        // Used to throw from .First() / [0].
        var irradiation = new IrradiationPanelViewModel(_container.Settings);
        var update = new UpdateAcquisitionViewModel(_container.Settings);

        Assert.Empty(irradiation.SourcesViewModels);
        Assert.Empty(update.ToUpdateAcquisitions);
        Assert.Equal("", ((UpdateAcquisition)update.ToModel()).AcquisitionTypeName);
    }

    [Fact]
    public void Update_acquisition_selects_the_first_acquisition_by_default()
    {
        _container.AddAcquisitions("A", "B");

        var model = (UpdateAcquisition)new UpdateAcquisitionViewModel(_container.Settings).ToModel();

        Assert.Equal("A", model.AcquisitionTypeName);
        Assert.Null(model.SmartProgramID);
    }

    [Fact]
    public void Update_acquisition_stops_tracking_acquisitions_after_dispose()
    {
        _container.AddAcquisitions("A");
        var vm = new UpdateAcquisitionViewModel(_container.Settings);

        _container.AddAcquisitions("B");
        Assert.Equal(new[] { "A", "B" }, vm.ToUpdateAcquisitions.Select(a => a.Name));

        vm.Dispose();
        _container.AddAcquisitions("C");
        Assert.Equal(new[] { "A", "B" }, vm.ToUpdateAcquisitions.Select(a => a.Name));
    }

    [Fact]
    public void Irradiation_dispose_raises_node_deleted()
    {
        _container.AddAcquisitions("A");
        var vm = new IrradiationPanelViewModel(_container.Settings);
        MeasurementElementViewModel? deleted = null;
        vm.OnNodeDeleted += (_, n) => deleted = n;

        vm.Dispose();

        Assert.Same(vm, deleted);
    }

    [Fact]
    public void Detection_follows_acquisitions_being_added_and_removed()
    {
        _container.AddAcquisitions("A");
        var vm = new DetectionElementViewModel(_container.Settings);

        _container.AddAcquisitions("B");
        Assert.Equal(new[] { "A", "B" }, ((DetectionElement)vm.ToModel()).DetectionNames);

        _container.Settings.Acquisitions.RemoveAt(0);
        Assert.Equal(new[] { "B" }, ((DetectionElement)vm.ToModel()).DetectionNames);
        Assert.Equal("(B)", vm.DisplayedInfo);
    }

    [Fact]
    public void Detection_with_an_empty_smart_program_slot_still_builds()
    {
        var vm = new DetectionElementViewModel(_container.Settings);
        vm.SmartProgramBindings.Add(null);

        Assert.Empty(((DetectionElement)vm.ToModel()).SmartProgramIds);
    }

    private StageLoopViewModel StageLoopWith(int positions)
    {
        var vm = new StageLoopViewModel(_container.StageControl.Object);
        for (int i = 0; i < positions; i++)
            vm.AppendStagePosition(i, i, i, false, 0, $"p{i}");
        return vm;
    }

    [Fact]
    public void Stage_loop_delete_keeps_the_selection_in_range()
    {
        var vm = StageLoopWith(3);

        vm.CurrentSelectedIndex = 2;
        vm.DeleteSelectedItem();
        Assert.Equal(new[] { "p0", "p1" }, vm.XYPositions.Select(p => p.Name));
        Assert.Equal(1, vm.CurrentSelectedIndex);

        vm.CurrentSelectedIndex = 0;
        vm.DeleteSelectedItem();
        Assert.Equal(new[] { "p1" }, vm.XYPositions.Select(p => p.Name));
        Assert.Equal(0, vm.CurrentSelectedIndex);

        vm.DeleteSelectedItem();
        Assert.Empty(vm.XYPositions);
        Assert.Equal(-1, vm.CurrentSelectedIndex);

        vm.DeleteSelectedItem(); // nothing left, nothing happens
        Assert.Equal("(0 positions)", vm.DisplayedInfo);
    }

    [Fact]
    public void Stage_loop_ignores_an_out_of_range_selection()
    {
        var vm = StageLoopWith(2);
        vm.CurrentSelectedIndex = 2;

        vm.SetStagePosition();
        vm.SetToCurrentStagePosition();

        _container.StageControl.Verify(s => s.SetStagePosition(It.IsAny<XYStagePosition>()), Times.Never);
        Assert.Equal(new[] { "p0", "p1" }, vm.XYPositions.Select(p => p.Name));
    }

    [Fact]
    public void Stage_loop_moves_positions_up_and_down()
    {
        var vm = StageLoopWith(3);

        vm.CurrentSelectedIndex = 2;
        vm.MoveUp();
        Assert.Equal(new[] { "p0", "p2", "p1" }, vm.XYPositions.Select(p => p.Name));
        Assert.Equal(1, vm.CurrentSelectedIndex);

        vm.CurrentSelectedIndex = 0;
        vm.MoveUp(); // already at the top
        vm.MoveDown();
        Assert.Equal(new[] { "p2", "p0", "p1" }, vm.XYPositions.Select(p => p.Name));
        Assert.Equal(1, vm.CurrentSelectedIndex);
    }

    [Fact]
    public void Stage_loop_moves_the_stage_to_the_selected_position()
    {
        var vm = StageLoopWith(2);
        vm.CurrentSelectedIndex = 1;

        vm.SetStagePosition();

        _container.StageControl.Verify(s => s.SetStagePosition(It.Is<XYStagePosition>(p => p.Name == "p1")));
    }

    [Fact]
    public void Stage_loop_reads_new_positions_from_the_stage()
    {
        _container.StageControl.Setup(s => s.ReadStagePosition()).Returns(() => new XYStagePosition(0, 7, 8, 9, false, ""));
        var vm = StageLoopWith(0);

        vm.GetStagePosition();
        vm.GetStagePosition();

        Assert.Equal(new[] { "Pos1", "Pos2" }, vm.XYPositions.Select(p => p.Name));
        Assert.Equal(9, vm.XYPositions[0].Coordinates.z);
    }

    [Fact]
    public void Relative_stage_loop_display_counts_planes()
    {
        var vm = new RelStageViewModel(_container.Settings, _container.StageControl.Object)
        {
            TileNegativeX = 1, TilePositiveX = 2, TilePositiveZ = 3,
        };

        Assert.Equal("(X=4,Y=1,Z=4) ", vm.DisplayedInfo);
        Assert.Equal(3, vm.NumStepsX);
        Assert.Equal(3, vm.NumStepsZ);
    }
}
