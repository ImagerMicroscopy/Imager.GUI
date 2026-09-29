using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Detectors;

public class DetectorPropertySerializationTests
{
    private static DetectorEquipmentProperties Parse(string json) =>
        JsonConvert.DeserializeObject<DetectorEquipmentProperties>(json)!;

    [Fact]
    public void Numeric_property_is_read_from_backend_json()
    {
        var prop = Parse("""{"propertycode":0,"descriptor":"Exposure time","kind":"numeric","value":5.0e-3}""");

        var numeric = Assert.IsType<NumericDetectorProperty>(prop);
        Assert.Equal(PropertyKind.numeric, numeric.kind);
        Assert.Equal(0, numeric.propertycode);
        Assert.Equal("Exposure time", numeric.descriptor);
        Assert.Equal(0.005, numeric.value);
    }

    [Fact]
    public void Discrete_property_is_read_from_backend_json()
    {
        var prop = Parse("""
            {"propertycode":3,"descriptor":"Binning","kind":"discrete","current":"2","availableoptions":["1","2","4"]}
            """);

        var categoric = Assert.IsType<CategoricDetectorProperty>(prop);
        Assert.Equal(PropertyKind.discrete, categoric.kind);
        Assert.Equal(3, categoric.propertycode);
        Assert.Equal("Binning", categoric.descriptor);
        Assert.Equal("2", categoric.current);
        Assert.Equal(new[] { "1", "2", "4" }, categoric.availableoptions);
    }

    [Fact]
    public void Field_order_in_backend_json_does_not_matter()
    {
        var prop = Parse("""
            {"availableoptions":["a","b"],"current":"b","kind":"discrete","descriptor":"Mode","propertycode":9}
            """);

        Assert.Equal("b", Assert.IsType<CategoricDetectorProperty>(prop).current);
    }

    [Theory]
    [InlineData("""{"propertycode":0,"descriptor":"x","kind":"weird"}""")]
    [InlineData("""{"propertycode":0,"descriptor":"x"}""")]
    public void Unknown_or_missing_kind_throws(string json)
    {
        Assert.Throws<JsonSerializationException>(() => Parse(json));
    }

    [Fact]
    public void Numeric_property_round_trips()
    {
        var original = new NumericDetectorProperty("Gain", 4, 12.5);

        var copy = Assert.IsType<NumericDetectorProperty>(Parse(JsonConvert.SerializeObject(original)));

        Assert.Equal(original.descriptor, copy.descriptor);
        Assert.Equal(original.propertycode, copy.propertycode);
        Assert.Equal(original.value, copy.value);
    }

    [Fact]
    public void Categoric_property_round_trips()
    {
        var original = new CategoricDetectorProperty("Binning", 2, "4", new List<string> { "1", "2", "4" });

        var copy = Assert.IsType<CategoricDetectorProperty>(Parse(JsonConvert.SerializeObject(original)));

        Assert.Equal(original.descriptor, copy.descriptor);
        Assert.Equal(original.propertycode, copy.propertycode);
        Assert.Equal(original.current, copy.current);
        Assert.Equal(original.availableoptions, copy.availableoptions);
    }

    [Fact]
    public void Serialized_properties_include_kind_so_backend_can_dispatch()
    {
        var numeric = JObject.Parse(JsonConvert.SerializeObject(new NumericDetectorProperty("a", 0, 1)));
        var categoric = JObject.Parse(JsonConvert.SerializeObject(
            new CategoricDetectorProperty("b", 1, "x", new List<string> { "x" })));

        Assert.Equal("numeric", numeric["kind"]!.Value<string>());
        Assert.Equal("discrete", categoric["kind"]!.Value<string>());
    }

    [Theory]
    [InlineData(0.1, 0.1)]
    [InlineData(1.0 / 3.0, 0.333333)]
    [InlineData(123.4567894, 123.456789)]
    public void Numeric_value_is_rounded_to_six_digits_in_measurement_payloads(double value, double expected)
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(
            new NumericDetectorProperty("a", 0, value), DetectionEquipmentSerializer.Settings));

        Assert.Equal(expected, json["value"]!.Value<double>());
    }

    [Fact]
    public void Numeric_value_is_not_rounded_with_default_settings()
    {
        // DetectorEquipmentPropertiesConverter writes "value" itself, so the
        // [JsonConverter(RoundingDoubleJsonConverter)] on the property does not apply;
        // rounding only happens when the serializer settings include the converter.
        var json = JObject.Parse(JsonConvert.SerializeObject(new NumericDetectorProperty("a", 0, 1.0 / 3.0)));

        Assert.Equal(1.0 / 3.0, json["value"]!.Value<double>());
    }

    [Fact]
    public void Numeric_clone_is_independent()
    {
        var original = new NumericDetectorProperty("Gain", 4, 1.0);
        var clone = (NumericDetectorProperty)original.Clone();

        clone.value = 2.0;

        Assert.Equal(1.0, original.value);
    }

    [Fact]
    public void Categoric_clone_is_independent_including_options_list()
    {
        var original = new CategoricDetectorProperty("Binning", 2, "1", new List<string> { "1", "2" });
        var clone = (CategoricDetectorProperty)original.Clone();

        clone.current = "2";
        clone.availableoptions.Add("4");

        Assert.Equal("1", original.current);
        Assert.Equal(new[] { "1", "2" }, original.availableoptions);
    }

    [Fact]
    public void Detector_model_copy_constructor_deep_copies_properties()
    {
        var original = new DetectorEquipmentModel("DummyCam1", new List<DetectorEquipmentProperties>
        {
            new NumericDetectorProperty("Exposure", 0, 0.1),
            new CategoricDetectorProperty("Binning", 1, "1", new List<string> { "1", "2" }),
        }) { IsEnabled = true };

        var copy = new DetectorEquipmentModel(original);
        ((CategoricDetectorProperty)copy.DetectorProperties[1]).current = "2";

        Assert.Equal("DummyCam1", copy.Detectorname);
        Assert.True(copy.IsEnabled);
        Assert.NotSame(original.DetectorProperties[0], copy.DetectorProperties[0]);
        Assert.Equal("1", ((CategoricDetectorProperty)original.DetectorProperties[1]).current);
    }

    [Fact]
    public void Detector_list_sent_to_backend_skips_disabled_detectors()
    {
        var detectors = new List<DetectorEquipmentModel>
        {
            new("On", new List<DetectorEquipmentProperties> { new NumericDetectorProperty("e", 0, 1) }) { IsEnabled = true },
            new("Off") { IsEnabled = false },
        };

        var json = JArray.Parse(JsonConvert.SerializeObject(detectors, DetectionEquipmentSerializer.Settings));

        var only = Assert.Single(json);
        Assert.Equal("On", only["detectorname"]!.Value<string>());
        Assert.Null(only["isenabled"]);
        Assert.Equal("numeric", only["detectorproperties"]![0]!["kind"]!.Value<string>());
    }

    [Fact]
    public void Detector_list_saved_to_project_keeps_disabled_detectors()
    {
        var detectors = new List<DetectorEquipmentModel>
        {
            new("On") { IsEnabled = true },
            new("Off") { IsEnabled = false },
        };

        var json = JsonConvert.SerializeObject(detectors, DetectionEquipmentSerializer.SettingsIncludingDisabled);
        var back = JsonConvert.DeserializeObject<List<DetectorEquipmentModel>>(json, DetectionEquipmentSerializer.SettingsIncludingDisabled)!;

        Assert.Equal(new[] { "On", "Off" }, back.Select(d => d.Detectorname));
        Assert.Equal(new[] { true, false }, back.Select(d => d.IsEnabled));
    }
}
