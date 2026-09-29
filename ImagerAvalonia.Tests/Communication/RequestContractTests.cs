using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Communication;

/// <summary>
/// Checks the JSON the GUI sends against what Imager.Core's FromJSON RequestMessage
/// (Imager.Core/src/CuvettorTypes.hs) requires: the action name plus every field it reads
/// with (.:), which fails on missing keys.
/// </summary>
public class RequestContractTests
{
    public static TheoryData<ImagerRequest, string, string[]> Requests => new()
    {
        { new ListWavelengthsRequest(), "listwavelengths", Array.Empty<string>() },
        { new ListAvailableEquipmentRequest(), "listavailableequipment", Array.Empty<string>() },
        { new ListAvailableDetectorsRequest(), "listavailabledetectors", Array.Empty<string>() },
        { new PingRequest(), "ping", Array.Empty<string>() },
        { new FetchAsyncDataRequest(), "fetchasyncspectra", Array.Empty<string>() },
        { new FetchAsyncStatusMessagesRequest(), "fetchasyncstatusmessages", Array.Empty<string>() },
        { new CancelAsyncAcquisitionRequest(), "cancelasyncacquisition", Array.Empty<string>() },
        { new IsAsyncAcquisitionRunningRequest(), "isasyncacquisitionrunning", Array.Empty<string>() },
        { new GetDetectorPropertiesRequest("cam"), "getdetectorproperties", new[] { "detectorname" } },
        { new GetMotorizedStagePositionRequest("stage"), "getmotorizedstageposition", new[] { "name" } },
        {
            new SetMotorizedStagePositionRequest("stage", new StageCoordinates(0, false, 1, 2, 3)),
            "setmotorizedstageposition", new[] { "name", "position" }
        },
        {
            new SetDetectorPropertyRequest("cam", new NumericDetectorProperty("Exposure", 0, 0.1)),
            "setdetectorproperty", new[] { "detectorname", "property" }
        },
        { new UseSharedMemoryForTransferRequest(true), "usesharedmemoryfortransfer", new[] { "usesharedmemory" } },
        { new AcknowledgeDataReceiptRequest(42), "acknowledgedatareceipt", new[] { "uptoandincluding" } },
        { new AcquireDataRequest(new JObject { ["a"] = 1 }), "acquiredata", new[] { "params" } },
        {
            new ExecuteMeasurementProgramRequest(new JObject(), new JObject(), new JObject()),
            "executemeasurementprogram", new[] { "program", "defineddetections", "smartprogramcode" }
        },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void Request_has_action_and_all_fields_the_backend_requires(
        ImagerRequest request, string expectedAction, string[] requiredFields)
    {
        var json = JObject.Parse(request.ToJson());

        Assert.Equal(expectedAction, json["action"]?.Value<string>());
        Assert.Equal(expectedAction, request.Action);

        foreach (var field in requiredFields)
        {
            Assert.True(json.ContainsKey(field), $"'{expectedAction}' is missing '{field}'");
            Assert.NotEqual(JTokenType.Null, json[field]!.Type);
        }
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public void Request_has_no_unexpected_fields(ImagerRequest request, string _, string[] requiredFields)
    {
        var json = JObject.Parse(request.ToJson());

        var extra = json.Properties()
            .Select(p => p.Name)
            .Except(requiredFields.Append("action"))
            .ToList();

        Assert.Empty(extra);
    }

    [Fact]
    public void Stage_position_uses_the_backend_key_names()
    {
        var request = new SetMotorizedStagePositionRequest("dStage", new StageCoordinates(0.5, true, 1.0, 2.0, 3.0));

        var position = (JObject)JObject.Parse(request.ToJson())["position"]!;

        Assert.Equal(1.0, position["x"]!.Value<double>());
        Assert.Equal(2.0, position["y"]!.Value<double>());
        Assert.Equal(3.0, position["z"]!.Value<double>());
        Assert.True(position["usinghardwareautofocus"]!.Value<bool>());
        Assert.Equal(0.5, position["hardwareautofocusoffset"]!.Value<double>());
    }

    [Fact]
    public void Acknowledge_sends_the_index_as_an_integer()
    {
        var json = JObject.Parse(new AcknowledgeDataReceiptRequest(ulong.MaxValue / 2).ToJson());

        Assert.Equal(JTokenType.Integer, json["uptoandincluding"]!.Type);
        Assert.Equal(ulong.MaxValue / 2, json["uptoandincluding"]!.Value<ulong>());
    }

    [Fact]
    public void Set_numeric_detector_property_matches_backend_detector_property_shape()
    {
        var request = new SetDetectorPropertyRequest("DummyCam1", new NumericDetectorProperty("Exposure time", 0, 0.123456789));

        var property = (JObject)JObject.Parse(request.ToJson())["property"]!;

        // SCCameraTypes.hs FromJSON DetectorProperty, "numeric" branch.
        Assert.Equal("numeric", property["kind"]!.Value<string>());
        Assert.Equal(0, property["propertycode"]!.Value<int>());
        Assert.Equal("Exposure time", property["descriptor"]!.Value<string>());
        Assert.Equal(JTokenType.Float, property["value"]!.Type);
        Assert.Equal(0.123456789, property["value"]!.Value<double>());
    }

    [Fact]
    public void Set_categoric_detector_property_matches_backend_detector_property_shape()
    {
        var request = new SetDetectorPropertyRequest("DummyCam1",
            new CategoricDetectorProperty("Binning", 2, "4", new List<string> { "1", "2", "4" }));

        var property = (JObject)JObject.Parse(request.ToJson())["property"]!;

        // SCCameraTypes.hs FromJSON DetectorProperty, "discrete" branch.
        Assert.Equal("discrete", property["kind"]!.Value<string>());
        Assert.Equal(2, property["propertycode"]!.Value<int>());
        Assert.Equal("Binning", property["descriptor"]!.Value<string>());
        Assert.Equal(JTokenType.String, property["current"]!.Type);
        Assert.Equal("4", property["current"]!.Value<string>());
        Assert.Equal(new[] { "1", "2", "4" }, property["availableoptions"]!.ToObject<string[]>());
    }

    [Fact]
    public void Categoric_property_with_null_current_serializes_null_which_the_backend_rejects()
    {
        // Documents the wire shape that caused "parsing Text failed, expected String, but
        // encountered Null". The view model must never let this reach SetDetectorPropertyAsync
        // (see DetectorEquipmentViewModelTests).
        var request = new SetDetectorPropertyRequest("DummyCam1",
            new CategoricDetectorProperty("Binning", 2, null!, new List<string> { "1" }));

        var property = (JObject)JObject.Parse(request.ToJson())["property"]!;

        Assert.Equal(JTokenType.Null, property["current"]!.Type);
    }
}
