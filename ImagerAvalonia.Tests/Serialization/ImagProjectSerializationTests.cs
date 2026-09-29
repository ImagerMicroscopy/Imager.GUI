using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Tests.TestData;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Serialization;

/// <summary>.imag project save/load via FullEquipmentStateSerializer.</summary>
public class ImagProjectSerializationTests
{
    internal static MeasurementElementBase SampleProgram()
    {
        var root = new DoTimesElement { NTotal = 2, ElementId = "root" };

        root.AddChild(new DetectionElement { ElementId = "d1", DetectionNames = { "Acq1" } });

        var relStage = new RelativeStageLoopElement
        {
            ElementId = "rel",
            StageName = "dStage",
            Params = new RelativeStageLoopParams
            {
                AdditionalPlanesX = new() { 1, 2 },
                AdditionalPlanesY = new() { 0, 1 },
                AdditionalPlanesZ = new() { 3, 0 },
                DeltaX = 0.5,
                DeltaY = 1.25,
                DeltaZ = 2,
                ReturnToStartingPosition = true,
            }
        };
        relStage.AddChild(new DetectionElement { ElementId = "d2", DetectionNames = { "Acq1", "Acq2" } });
        root.AddChild(relStage);

        var stageLoop = new StageLoopElement
        {
            ElementId = "sl",
            StageName = "dStage",
            Positions =
            {
                new XYStagePosition(0, 1, 2, 3, false, "p1"),
                new XYStagePosition(0.5, 4, 5, 6, true, "p2"),
            }
        };
        stageLoop.AddChild(new DetectionElement { ElementId = "d3", DetectionNames = { "Acq2" } });
        root.AddChild(stageLoop);

        var timeLapse = new TimeLapseElement { ElementId = "tl", NTotal = 3, TimeDelta = 1.5 };
        timeLapse.AddChild(new WaitElement { ElementId = "w", Duration = 2 });
        root.AddChild(timeLapse);

        root.AddChild(new IrradiationElement
        {
            ElementId = "irr",
            Duration = 0.25,
            Irradiation =
            {
                new IrradiationConfig
                {
                    EquipmentName = "hello", LightSourceName = "ls",
                    LightSourceChannel = { "ch1" }, LightSourcePower = { 40 }
                }
            }
        });

        root.AddChild(new UpdateAcquisition
        {
            ElementId = "ua", AcquisitionTypeName = "Acq1", DetectionName = "Acq2",
            SmartProgramID = Guid.Empty.ToString()
        });

        return root;
    }

    private static FullEquipmentState SampleState()
    {
        var workspace = EquipmentFixtures.Workspace(
            sources: new[] { EquipmentFixtures.Laser(), EquipmentFixtures.Laser(name: "ls2") },
            detectors: new[] { EquipmentFixtures.Camera(), EquipmentFixtures.Camera("DummyCam2", enabled: false) });
        workspace.NumAcquisition = 4;

        return new FullEquipmentState
        {
            CurrentEquipment = workspace,
            CurrentProgram = new MeasurementProgram(SampleProgram(), new Dictionary<string, DetectionParams>
            {
                ["Acq1"] = EquipmentFixtures.Detection(),
                ["Acq2"] = EquipmentFixtures.Detection(
                    irradiation: new[] { EquipmentFixtures.Laser(name: "ls2", activeChannels: "ch2") },
                    detectors: new[] { EquipmentFixtures.Camera(binning: "4"), EquipmentFixtures.Camera("DummyCam2", enabled: false) }),
            }),
        };
    }

    private static FullEquipmentState RoundTrip(FullEquipmentState state) =>
        FullEquipmentStateSerializer.Deserialize(FullEquipmentStateSerializer.Serialize(state));

    [Fact]
    public void Save_load_save_is_stable()
    {
        var first = FullEquipmentStateSerializer.Serialize(SampleState());
        var second = FullEquipmentStateSerializer.Serialize(FullEquipmentStateSerializer.Deserialize(first));

        Assert.True(JToken.DeepEquals(JToken.Parse(first), JToken.Parse(second)),
            $"Round trip changed the file.\n--- first ---\n{first}\n--- second ---\n{second}");
    }

    [Fact]
    public void Default_acquisition_and_acquisition_counter_are_saved()
    {
        var loaded = RoundTrip(SampleState()).CurrentEquipment;

        Assert.Equal(4, loaded.NumAcquisition);
        Assert.NotNull(loaded.DefaultAcquisition);
        Assert.Equal(new[] { "DummyCam1", "DummyCam2" },
            loaded.DefaultAcquisition!.Settings.Detectors.Select(d => d.Detectorname));
    }

    [Fact]
    public void Files_without_default_acquisition_still_load()
    {
        var json = JObject.Parse(FullEquipmentStateSerializer.Serialize(SampleState()));
        var equipment = (JObject)json["currentequipment"]!;
        equipment.Remove(nameof(EquipmentWorkspace.DefaultAcquisition));
        equipment.Remove(nameof(EquipmentWorkspace.NumAcquisition));

        var loaded = FullEquipmentStateSerializer.Deserialize(json.ToString()).CurrentEquipment;

        Assert.Null(loaded.DefaultAcquisition);
        Assert.Equal(1, loaded.NumAcquisition);
    }

    [Fact]
    public void Equipment_lists_are_restored()
    {
        var loaded = RoundTrip(SampleState()).CurrentEquipment;

        Assert.Equal(new[] { "ls", "ls2" }, loaded.AvailableSources.Select(s => s.LightSourceName));
        Assert.Equal(new[] { "ch1", "ch2" }, loaded.AvailableSources[0].AvailableChannels);
        Assert.Equal("fw1", Assert.Single(loaded.AvailableFilterWheels).equipmentname);
        Assert.Equal(new[] { "DummyCam1", "DummyCam2" }, loaded.AvailableDetectors.Select(d => d.Detectorname));
        Assert.Equal(new[] { true, false }, loaded.AvailableDetectors.Select(d => d.IsEnabled));
    }

    [Fact]
    public void Filter_wheel_selection_and_range_are_restored()
    {
        var wheel = Assert.Single(RoundTrip(SampleState()).CurrentEquipment.AvailableFilterWheels);

        var filter = Assert.IsType<DiscreteMovableComponentPartProperties>(wheel.movablecomponents[0].movablecomponent);
        Assert.Equal("GFP", filter.desiredsetting);
        Assert.Equal(new[] { "DAPI", "GFP", "RFP" }, filter.PossibleSettings);

        var slider = Assert.IsType<ContinuousMovableComponentPartProperties>(wheel.movablecomponents[1].movablecomponent);
        Assert.Equal(12.5, slider.desiredsetting);
        Assert.Equal(0, slider.MinValue);
        Assert.Equal(100, slider.MaxValue);
        Assert.Equal(0.5, slider.increment);
    }

    [Fact]
    public void Detections_keep_disabled_detectors_and_light_source_names()
    {
        var detections = RoundTrip(SampleState()).CurrentProgram.Detections;

        Assert.Equal(new[] { "Acq1", "Acq2" }, detections.Keys);

        var acq2 = detections["Acq2"];
        Assert.Equal(new[] { true, false }, acq2.Detectors.Select(d => d.IsEnabled));
        Assert.Equal("4", ((CategoricDetectorProperty)acq2.Detectors[0].DetectorProperties[1]).current);

        // Reconciliation matches on LightSourceName, so it must survive the file.
        var source = Assert.Single(acq2.Irradiation);
        Assert.Equal("hello", source.EquipmentName);
        Assert.Equal("ls2", source.LightSourceName);
        Assert.Equal(new[] { "ch2" }, source.LightsourceChannel);
    }

    [Fact]
    public void Program_tree_structure_is_restored()
    {
        var program = RoundTrip(SampleState()).CurrentProgram.Program;

        var root = Assert.IsType<DoTimesElement>(program);
        Assert.Equal(2, root.NTotal);
        Assert.Equal(new[] { typeof(DetectionElement), typeof(RelativeStageLoopElement), typeof(StageLoopElement),
                typeof(TimeLapseElement), typeof(IrradiationElement), typeof(UpdateAcquisition) },
            root.Elements.Select(e => e.GetType()));
        Assert.Equal(new[] { "d1", "rel", "sl", "tl", "irr", "ua" }, root.Elements.Select(e => e.ElementId));

        var stageLoop = (StageLoopElement)root.Elements[2];
        Assert.Equal(new[] { "p1", "p2" }, stageLoop.Positions.Select(p => p.Name));
        Assert.True(stageLoop.Positions[1].Coordinates.usinghardwareautofocus);
        Assert.Equal(6, stageLoop.Positions[1].Coordinates.z);

        var timeLapse = (TimeLapseElement)root.Elements[3];
        Assert.Equal(1.5, timeLapse.TimeDelta);
        Assert.Equal(2, Assert.IsType<WaitElement>(Assert.Single(timeLapse.Elements)).Duration);

        var irradiation = (IrradiationElement)root.Elements[4];
        Assert.Equal(new[] { 40.0 }, Assert.Single(irradiation.Irradiation).LightSourcePower);
    }

    [Fact]
    public void Relative_stage_loop_planes_are_not_appended_to_the_defaults()
    {
        // Regression: RelativeStageLoopParams initialises each list to {0, 0}; Newtonsoft's
        // default ObjectCreationHandling appended the saved values, giving [0, 0, neg, pos].
        var rel = (RelativeStageLoopElement)RoundTrip(SampleState()).CurrentProgram.Program.Elements[1];

        Assert.Equal(new[] { 1, 2 }, rel.Params.AdditionalPlanesX);
        Assert.Equal(new[] { 0, 1 }, rel.Params.AdditionalPlanesY);
        Assert.Equal(new[] { 3, 0 }, rel.Params.AdditionalPlanesZ);
        Assert.Equal(0.5, rel.Params.DeltaX);
        Assert.True(rel.Params.ReturnToStartingPosition);
    }

    [Fact]
    public void Detection_count_is_the_same_before_and_after_loading()
    {
        var original = SampleProgram();
        var loaded = RoundTrip(SampleState()).CurrentProgram.Program;

        // 2 x (1 + relstage (1+1+2)(1+0+1)(1+3+0)=32 + stageloop 2) = 70
        Assert.Equal(70, original.CountTotalDetections());
        Assert.Equal(70, loaded.CountTotalDetections());
    }

    [Fact]
    public void Missing_current_program_loads_as_null()
    {
        var json = JObject.Parse(FullEquipmentStateSerializer.Serialize(SampleState()));
        json.Remove("currentprogram");

        var state = FullEquipmentStateSerializer.Deserialize(json.ToString());

        Assert.Null(state.CurrentProgram);
        Assert.NotNull(state.CurrentEquipment);
    }

    [Fact]
    public void Missing_api_version_defaults_to_2_0()
    {
        var json = JObject.Parse(FullEquipmentStateSerializer.Serialize(SampleState()));
        json.Remove("apiversion");

        Assert.Equal("2.0", FullEquipmentStateSerializer.Deserialize(json.ToString()).ApiVersion);
    }

    [Fact]
    public void Program_without_program_element_is_rejected()
    {
        var json = JObject.Parse(FullEquipmentStateSerializer.Serialize(SampleState()));
        ((JObject)json["currentprogram"]!).Remove("program");

        Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() =>
            FullEquipmentStateSerializer.Deserialize(json.ToString()));
    }
}
