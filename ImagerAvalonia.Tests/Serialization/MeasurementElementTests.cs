using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Serialization;

public class MeasurementElementTests
{
    private static MeasurementElementBase Parse(string json) =>
        MeasurementSerializer.Deserialize<MeasurementElementBase>(json);

    // ---------- Converter ----------

    [Theory]
    [InlineData("detection", typeof(DetectionElement))]
    [InlineData("dotimes", typeof(DoTimesElement))]
    [InlineData("stageloop", typeof(StageLoopElement))]
    [InlineData("timelapse", typeof(TimeLapseElement))]
    [InlineData("wait", typeof(WaitElement))]
    [InlineData("irradiation", typeof(IrradiationElement))]
    [InlineData("relativestageloop", typeof(RelativeStageLoopElement))]
    [InlineData("executerobotprogram", typeof(ExecuteRobotProgramElement))]
    [InlineData("updateacquisition", typeof(UpdateAcquisition))]
    [InlineData("DoTimes", typeof(DoTimesElement))]
    public void Element_type_selects_the_model_class(string elementType, Type expected)
    {
        var element = Parse($$"""{"elementtype":"{{elementType}}","elementid":"x"}""");

        Assert.IsType(expected, element);
        Assert.Equal("x", element.ElementId);
    }

    [Theory]
    [InlineData("""{"elementtype":"teleport"}""")]
    [InlineData("""{"elementid":"no-type"}""")]
    public void Unknown_or_missing_element_type_is_rejected(string json)
    {
        Assert.Throws<JsonSerializationException>(() => Parse(json));
    }

    [Fact]
    public void Written_json_uses_lowercase_keys_and_registry_element_type()
    {
        var json = JObject.Parse(MeasurementSerializer.Serialize(new DoTimesElement { NTotal = 3, ElementId = "a" }));

        Assert.Equal("dotimes", json["elementtype"]!.Value<string>());
        Assert.Equal(3, json["ntotal"]!.Value<int>());
        Assert.Equal("a", json["elementid"]!.Value<string>());
    }

    [Fact]
    public void Leaf_elements_do_not_write_an_elements_list()
    {
        var json = JObject.Parse(MeasurementSerializer.Serialize(new WaitElement { Duration = 1 }));

        Assert.False(json.ContainsKey("elements"));
    }

    [Fact]
    public void Container_elements_write_their_children()
    {
        var loop = new DoTimesElement { NTotal = 2 };
        loop.AddChild(new WaitElement { Duration = 1 });

        var json = JObject.Parse(MeasurementSerializer.Serialize(loop));

        Assert.Equal("wait", json["elements"]![0]!["elementtype"]!.Value<string>());
    }

    [Fact]
    public void Nested_tree_round_trips()
    {
        var program = ImagProjectSerializationTests.SampleProgram();

        var first = MeasurementSerializer.Serialize(program);
        var second = MeasurementSerializer.Serialize(Parse(first));

        Assert.True(JToken.DeepEquals(JToken.Parse(first), JToken.Parse(second)));
    }

    [Fact]
    public void Relative_stage_loop_planes_from_json_replace_the_defaults()
    {
        var element = (RelativeStageLoopElement)Parse("""
            {"elementtype":"relativestageloop","params":{
              "additionalplanesx":[4,5],"additionalplanesy":[0,0],"additionalplanesz":[1,1],
              "deltax":1,"deltay":1,"deltaz":1,"returntostartingposition":false}}
            """);

        Assert.Equal(new[] { 4, 5 }, element.Params.AdditionalPlanesX);
        Assert.Equal(new[] { 1, 1 }, element.Params.AdditionalPlanesZ);
    }

    [Fact]
    public void Relative_stage_loop_without_planes_keeps_the_defaults()
    {
        var element = (RelativeStageLoopElement)Parse("""{"elementtype":"relativestageloop","params":{"deltax":1}}""");

        Assert.Equal(new[] { 0, 0 }, element.Params.AdditionalPlanesX);
    }

    [Fact]
    public void Children_in_json_under_a_leaf_element_are_rejected()
    {
        var ex = Assert.ThrowsAny<Exception>(() => Parse("""
            {"elementtype":"wait","duration":1,"elements":[{"elementtype":"detection"}]}
            """));

        Assert.Contains("cannot have children", ex.ToString());
    }

    [Fact]
    public void Robot_program_keeps_only_robot_identity_and_call_parameters()
    {
        var element = new ExecuteRobotProgramElement
        {
            ProgramParameters =
            {
                Robot = { EquipmentName = "arm", RobotName = "r1" },
                ProgramCallParameters =
                {
                    ProgramName = "pick",
                    Arguments =
                    {
                        new DiscreteRobotProgramArgument { ArgumentName = "slot", RobotProgramArgumentType = "discrete", Argument = "A1" },
                        new ContinuousRobotProgramArgument { ArgumentName = "speed", RobotProgramArgumentType = "continuous", Argument = 0.5 },
                    }
                }
            }
        };

        var loaded = (ExecuteRobotProgramElement)Parse(MeasurementSerializer.Serialize(element));

        Assert.Equal("arm", loaded.ProgramParameters.Robot.EquipmentName);
        Assert.Equal("r1", loaded.ProgramParameters.Robot.RobotName);
        Assert.Equal("pick", loaded.ProgramParameters.ProgramCallParameters.ProgramName);
        var args = loaded.ProgramParameters.ProgramCallParameters.Arguments;
        Assert.Equal("A1", Assert.IsType<DiscreteRobotProgramArgument>(args[0]).Argument);
        Assert.Equal(0.5, Assert.IsType<ContinuousRobotProgramArgument>(args[1]).Argument);
    }

    [Fact]
    public void Unknown_robot_argument_type_is_rejected()
    {
        Assert.Throws<JsonSerializationException>(() => Parse("""
            {"elementtype":"executerobotprogram","programparameters":{"equipmentname":"a","robotname":"b",
             "programcallparameters":{"programname":"p","arguments":[{"argumentname":"x","robotprogramargumenttype":"magic"}]}}}
            """));
    }

    [Fact]
    public void Legacy_detection_list_format_is_still_read()
    {
        var detections = JsonConvert.DeserializeObject<Dictionary<string, DetectionParams>>("""
            [ { "name": "Old", "settings": { "detectors": [], "irradiation": [], "movablecomponents": [] } } ]
            """, MeasurementSerializer.Settings)!;

        Assert.Equal("Old", Assert.Single(detections).Key);
    }

    // ---------- Children validation ----------

    [Fact]
    public void Leaf_elements_cannot_get_children()
    {
        var wait = new WaitElement();

        Assert.False(wait.CanHaveChildren());
        Assert.Throws<InvalidOperationException>(() => wait.AddChild(new DetectionElement()));
        Assert.Throws<InvalidOperationException>(() => wait.Elements.Insert(0, new DetectionElement()));
        Assert.Throws<InvalidOperationException>(() => wait.Elements = new List<MeasurementElementBase> { new DetectionElement() });
    }

    [Fact]
    public void Container_elements_accept_children_and_reject_null()
    {
        var loop = new TimeLapseElement();

        loop.AddChild(new DetectionElement());
        loop.Elements.Insert(0, new WaitElement());

        Assert.Equal(new[] { typeof(WaitElement), typeof(DetectionElement) }, loop.Elements.Select(e => e.GetType()));
        Assert.Throws<ArgumentNullException>(() => loop.AddChild(null!));
    }

    [Fact]
    public void Setting_elements_to_empty_or_null_clears_them()
    {
        var loop = new DoTimesElement();
        loop.AddChild(new DetectionElement());

        loop.Elements = null!;
        Assert.Empty(loop.Elements);

        loop.AddChild(new DetectionElement());
        loop.Elements = new List<MeasurementElementBase>();
        Assert.Empty(loop.Elements);
    }

    // ---------- Detection counting ----------

    private static T With<T>(T parent, params MeasurementElementBase[] children) where T : MeasurementElementBase
    {
        foreach (var child in children)
            parent.AddChild(child);
        return parent;
    }

    [Fact]
    public void Single_detection_counts_one() =>
        Assert.Equal(1, new DetectionElement().CountTotalDetections());

    [Fact]
    public void Non_detection_leaves_count_zero()
    {
        Assert.Equal(0, new WaitElement().CountTotalDetections());
        Assert.Equal(0, new IrradiationElement().CountTotalDetections());
        Assert.Equal(0, ((MeasurementElementBase)null!).CountTotalDetections());
    }

    [Fact]
    public void Empty_container_counts_zero() =>
        Assert.Equal(0, new DoTimesElement { NTotal = 10 }.CountTotalDetections());

    [Fact]
    public void Loops_multiply_their_children()
    {
        Assert.Equal(10, With(new DoTimesElement { NTotal = 5 }, new DetectionElement(), new DetectionElement()).CountTotalDetections());
        Assert.Equal(3, With(new TimeLapseElement { NTotal = 3 }, new DetectionElement()).CountTotalDetections());

        var stageLoop = new StageLoopElement
        {
            Positions = { new XYStagePosition(0, 0, 0, 0, false, "a"), new XYStagePosition(0, 1, 1, 1, false, "b") }
        };
        Assert.Equal(2, With(stageLoop, new DetectionElement()).CountTotalDetections());
    }

    [Fact]
    public void Relative_stage_loop_counts_every_plane_including_the_start()
    {
        var rel = new RelativeStageLoopElement
        {
            Params = { AdditionalPlanesX = new() { 1, 1 }, AdditionalPlanesY = new() { 0, 0 }, AdditionalPlanesZ = new() { 2, 3 } }
        };

        // (1+1+1) x (1+0+0) x (1+2+3) = 18
        Assert.Equal(18, With(rel, new DetectionElement()).CountTotalDetections());
    }

    [Fact]
    public void Relative_stage_loop_with_short_plane_lists_does_not_throw()
    {
        var rel = new RelativeStageLoopElement
        {
            Params = { AdditionalPlanesX = new() { 2 }, AdditionalPlanesY = new(), AdditionalPlanesZ = null! }
        };

        Assert.Equal(3, With(rel, new DetectionElement()).CountTotalDetections());
    }

    [Fact]
    public void Nested_loops_multiply()
    {
        var tree = With(new DoTimesElement { NTotal = 2 },
            With(new TimeLapseElement { NTotal = 3 },
                new DetectionElement(),
                With(new DoTimesElement { NTotal = 4 }, new DetectionElement())));

        // 2 x 3 x (1 + 4) = 30
        Assert.Equal(30, tree.CountTotalDetections());
    }

    // ---------- Stage positions ----------

    [Fact]
    public void Stage_positions_compare_within_tolerance_and_by_name()
    {
        var a = new XYStagePosition(0, 1.0, 2.0, 3.0, false, "p");

        Assert.True(a.IsEqual(new XYStagePosition(0, 1.0005, 2.0, 3.0, true, "p")));
        Assert.False(a.IsEqual(new XYStagePosition(0, 1.01, 2.0, 3.0, false, "p")));
        Assert.False(a.IsEqual(new XYStagePosition(0, 1.0, 2.0, 3.0, false, "q")));
        Assert.False(a.IsEqual(null!));
    }
}
