using System.Globalization;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Equipment;

public class EquipmentModelTests
{
    private static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // ---------- Parsing backend equipment ----------

    private const string ContinuousComponentJson =
        """{"componentname":"sl","type":"continuousmovablecomponent","minvalue":0.5,"maxvalue":99.5,"increment":0.25}""";

    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-BE")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Continuous_component_range_is_parsed_independent_of_culture(string culture)
    {
        var part = InCulture(culture, () => JsonConvert.DeserializeObject<MovableComponentPart>(ContinuousComponentJson)!);

        var props = Assert.IsType<ContinuousMovableComponentPartProperties>(part.movablecomponent);
        Assert.Equal(0.5, props.MinValue);
        Assert.Equal(99.5, props.MaxValue);
        Assert.Equal(0.25, props.increment);
        Assert.Equal(0.5, props.desiredsetting); // defaults to the minimum
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-BE")]
    public void Backend_equipment_list_is_parsed_independent_of_culture(string culture)
    {
        var equipment = JObject.Parse(Communication.ImagerCoreWire.AvailableEquipment)["equipment"]![0]!;

        var container = InCulture(culture, () =>
            equipment.ToObject<EquipmentContainer>(JsonSerializer.Create(DetectionEquipmentSerializer.Settings))!);

        var slider = Assert.IsType<ContinuousMovableComponentPartProperties>(container.availablemovablecomponents[1].movablecomponent);
        Assert.Equal(100, slider.MaxValue);
        Assert.Equal(1, slider.increment);
    }

    [Fact]
    public void Discrete_component_defaults_to_first_setting()
    {
        var part = new MovableComponentPart("fw", new List<string> { "DAPI", "GFP" }, "discretemovablecomponent", null!, null!, null!, null!);

        Assert.Equal("DAPI", ((DiscreteMovableComponentPartProperties)part.movablecomponent!).desiredsetting);
    }

    [Fact]
    public void Discrete_component_without_settings_is_rejected()
    {
        Assert.ThrowsAny<Exception>(() =>
            new MovableComponentPart("fw", new List<string>(), "discretemovablecomponent", null!, null!, null!, null!));
    }

    [Fact]
    public void Unknown_component_type_is_rejected()
    {
        Assert.ThrowsAny<Exception>(() =>
            new MovableComponentPart("fw", new List<string> { "a" }, "rotarymagic", null!, null!, null!, null!));
    }

    // ---------- Copies ----------

    [Fact]
    public void Filter_wheel_settings_are_linked_to_their_parts()
    {
        var wheel = EquipmentFixtures.FilterWheel();

        Assert.Same(wheel.movablecomponents[0].movablecomponent, wheel.movablecomponentsettings[0]);
    }

    [Fact]
    public void Filter_wheel_copy_is_independent_but_keeps_parts_and_settings_linked()
    {
        var original = EquipmentFixtures.FilterWheel();
        var copy = new MovableComponentModel(original);

        var copiedFilter = (DiscreteMovableComponentPartProperties)copy.movablecomponentsettings[0]!;
        copiedFilter.desiredsetting = "RFP";

        Assert.Equal("GFP", ((DiscreteMovableComponentPartProperties)original.movablecomponentsettings[0]!).desiredsetting);
        Assert.Same(copy.movablecomponents[0].movablecomponent, copy.movablecomponentsettings[0]);
        Assert.Equal("fw1", copy.equipmentname);
    }

    [Fact]
    public void Filter_wheel_copy_clones_settings_that_have_no_matching_part()
    {
        var original = new MovableComponentModel
        {
            equipmentname = "fw9",
            movablecomponentsettings = { new DiscreteMovableComponentPartProperties("fw", new List<string> { "a", "b" }, "b") }
        };

        var copy = new MovableComponentModel(original);

        Assert.NotSame(original.movablecomponentsettings[0], copy.movablecomponentsettings[0]);
        Assert.Equal("b", ((DiscreteMovableComponentPartProperties)copy.movablecomponentsettings[0]!).desiredsetting);
    }

    [Fact]
    public void Source_copy_is_independent()
    {
        var original = EquipmentFixtures.Laser(activeChannels: "ch1");
        var copy = new Source(original);

        copy.LightsourceChannel.Add("ch2");
        copy.AvailableChannels.Add("ch3");

        Assert.Equal(new[] { "ch1" }, original.LightsourceChannel);
        Assert.Equal(new[] { "ch1", "ch2" }, original.AvailableChannels);
        Assert.True(copy.IsEnabled);
        Assert.Equal("ls", copy.LightSourceName);
    }

    [Fact]
    public void Detection_params_clone_is_deep()
    {
        var original = EquipmentFixtures.Detection();
        var clone = original.Clone();

        clone.Irradiation[0].LightsourceChannel.Clear();
        ((CategoricDetectorProperty)clone.Detectors[0].DetectorProperties[1]).current = "4";

        Assert.Equal(new[] { "ch1" }, original.Irradiation[0].LightsourceChannel);
        Assert.Equal("1", ((CategoricDetectorProperty)original.Detectors[0].DetectorProperties[1]).current);
    }

    // ---------- Converters ----------

    [Theory]
    [InlineData("null", typeof(double?), null)]
    [InlineData("null", typeof(double), 0.0)]
    [InlineData("1.5", typeof(double), 1.5)]
    public void Rounding_converter_reads_values(string json, Type type, object? expected)
    {
        var converter = new RoundingDoubleJsonConverter();
        using var reader = new JsonTextReader(new StringReader(json));
        reader.Read();

        Assert.Equal(expected, converter.ReadJson(reader, type, null, JsonSerializer.CreateDefault()));
    }

    [Fact]
    public void Movable_selection_type_names_map_between_gui_and_backend()
    {
        var settings = new JsonSerializerSettings { Converters = { new SelectedMovableComponentConverter() } };

        Assert.Equal("\"discretemovablesetting\"",
            JsonConvert.SerializeObject(MovableComponentType.discretemovablecomponent, settings));
        Assert.Equal(MovableComponentType.continuousmovablecomponent,
            JsonConvert.DeserializeObject<MovableComponentType>("\"continuousmovablesetting\"", settings));
        Assert.Equal(MovableComponentType.discretemovablecomponent,
            JsonConvert.DeserializeObject<MovableComponentType>("\"discretemovablecomponent\"", settings));
    }

    [Fact]
    public void Backend_payload_for_a_filter_wheel_sends_only_the_selection()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(EquipmentFixtures.FilterWheel(), DetectionEquipmentSerializer.Settings));

        Assert.Equal("fw1", json["equipmentname"]!.Value<string>());
        Assert.False(json.ContainsKey("movablecomponents"));

        var settings = (JArray)json["movablecomponentsettings"]!;
        Assert.Equal("discretemovablesetting", settings[0]!["type"]!.Value<string>());
        Assert.Equal("GFP", settings[0]!["desiredsetting"]!.Value<string>());
        Assert.Equal("continuousmovablesetting", settings[1]!["type"]!.Value<string>());
        Assert.Equal(12.5, settings[1]!["desiredsetting"]!.Value<double>());
    }

    [Fact]
    public void Backend_payload_skips_disabled_light_sources()
    {
        var detection = EquipmentFixtures.Detection(irradiation: new[]
        {
            EquipmentFixtures.Laser(name: "on", activeChannels: "ch1"),
            EquipmentFixtures.Laser(name: "off"),
        });

        var json = JObject.Parse(JsonConvert.SerializeObject(detection, DetectionEquipmentSerializer.Settings));

        Assert.Equal("on", Assert.Single((JArray)json["irradiation"]!)["lightsourcename"]!.Value<string>());
    }

    // ---------- Reconciliation ----------

    [Fact]
    public void Reconcile_restores_the_selection_for_each_light_source_on_the_same_equipment()
    {
        // Regression: matching on EquipmentName alone gave every source the first one's channels.
        var equipment = EquipmentFixtures.Workspace(sources: new[]
        {
            EquipmentFixtures.Laser(name: "ls"),
            EquipmentFixtures.Laser(name: "ls2"),
        });
        var saved = EquipmentFixtures.Detection(irradiation: new[]
        {
            EquipmentFixtures.Laser(name: "ls", activeChannels: "ch1"),
            EquipmentFixtures.Laser(name: "ls2", activeChannels: "ch2"),
        });

        var result = EquipmentReconciler.Reconcile(saved, equipment);

        Assert.Equal(new[] { "ls", "ls2" }, result.Irradiation.Select(s => s.LightSourceName));
        Assert.Equal(new[] { "ch1" }, result.Irradiation[0].LightsourceChannel);
        Assert.Equal(new[] { "ch2" }, result.Irradiation[1].LightsourceChannel);
        Assert.All(result.Irradiation, s => Assert.True(s.IsEnabled));
    }

    [Fact]
    public void Reconcile_adds_unselected_sources_as_disabled()
    {
        var equipment = EquipmentFixtures.Workspace(sources: new[]
        {
            EquipmentFixtures.Laser(name: "ls"),
            EquipmentFixtures.Laser(equipment: "other", name: "led"),
        });
        var saved = EquipmentFixtures.Detection(irradiation: new[] { EquipmentFixtures.Laser(name: "ls", activeChannels: "ch2") });

        var result = EquipmentReconciler.Reconcile(saved, equipment);

        var led = result.Irradiation.Single(s => s.LightSourceName == "led");
        Assert.False(led.IsEnabled);
        Assert.Empty(led.LightsourceChannel);
        Assert.Equal(new[] { "ch1", "ch2" }, led.AvailableChannels);
    }

    [Fact]
    public void Reconcile_treats_a_source_with_no_channels_as_unselected()
    {
        var equipment = EquipmentFixtures.Workspace(sources: new[] { EquipmentFixtures.Laser() });
        var saved = EquipmentFixtures.Detection(irradiation: new[] { EquipmentFixtures.Laser() });

        Assert.False(Assert.Single(EquipmentReconciler.Reconcile(saved, equipment).Irradiation).IsEnabled);
    }

    [Fact]
    public void Reconcile_applies_saved_filter_selection_onto_available_wheel()
    {
        var equipment = EquipmentFixtures.Workspace(filterWheels: new[] { EquipmentFixtures.FilterWheel(filter: "DAPI", slider: "0") });
        var saved = EquipmentFixtures.Detection(filterWheels: new[] { EquipmentFixtures.FilterWheel(filter: "RFP", slider: "42") });

        var wheel = Assert.Single(EquipmentReconciler.Reconcile(saved, equipment).MovableComponents);

        Assert.Equal("RFP", ((DiscreteMovableComponentPartProperties)wheel.movablecomponentsettings[0]!).desiredsetting);
        Assert.Equal(42, ((ContinuousMovableComponentPartProperties)wheel.movablecomponentsettings[1]!).desiredsetting);
        Assert.Equal(100, ((ContinuousMovableComponentPartProperties)wheel.movablecomponentsettings[1]!).MaxValue);
    }

    [Fact]
    public void Reconcile_keeps_a_filter_wheel_that_is_not_in_the_reference_equipment()
    {
        // Used to throw InvalidOperationException from .First().
        var equipment = EquipmentFixtures.Workspace(filterWheels: new[] { EquipmentFixtures.FilterWheel("fw1") });
        var saved = EquipmentFixtures.Detection(filterWheels: new[] { EquipmentFixtures.FilterWheel("fw-gone", filter: "RFP") });

        var wheel = Assert.Single(EquipmentReconciler.Reconcile(saved, equipment).MovableComponents);

        Assert.Equal("fw-gone", wheel.equipmentname);
        Assert.Equal("RFP", ((DiscreteMovableComponentPartProperties)wheel.movablecomponentsettings[0]!).desiredsetting);
    }

    [Fact]
    public void Reconcile_copies_detectors()
    {
        var saved = EquipmentFixtures.Detection();

        var result = EquipmentReconciler.Reconcile(saved, EquipmentFixtures.Workspace());

        Assert.NotSame(saved.Detectors[0], result.Detectors[0]);
        Assert.Equal("DummyCam1", result.Detectors[0].Detectorname);
    }

    [Fact]
    public void Reconcile_acquisitions_creates_view_models_with_unique_names()
    {
        var settings = new GlobalDefinedSettingsViewModel();
        var equipment = EquipmentFixtures.Workspace();

        var first = settings.ReconcileAcquisitions(new[] { ("Acq", EquipmentFixtures.Detection()) }, equipment);
        var second = settings.ReconcileAcquisitions(new[]
        {
            ("Acq", EquipmentFixtures.Detection()),
            ("acq", EquipmentFixtures.Detection()),
            ("Other", EquipmentFixtures.Detection()),
        }, equipment);

        Assert.Equal("Acq", first["Acq"].Name);
        Assert.Equal("Acq (2)", second["Acq"].Name);
        Assert.Equal("acq (3)", second["acq"].Name); // uniqueness is case-insensitive
        Assert.Equal("Other", second["Other"].Name);
        Assert.Equal(new[] { "Acq", "Acq (2)", "acq (3)", "Other" }, settings.Acquisitions.Select(a => a.Name));
    }
}
