using Autofac;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Detectors;

/// <summary>
/// Simulates Imager.Core's detector state: accepts setdetectorproperty, validates it the way
/// SCCameraTypes.hs FromJSON DetectorProperty would, and returns fresh models on re-list.
/// </summary>
internal sealed class FakeDetectorBackend
{
    private readonly string _detectorName;
    private readonly Dictionary<int, string> _categoric = new();
    private readonly Dictionary<int, double> _numeric = new();
    private readonly Dictionary<int, (string Descriptor, List<string> Options)> _categoricMeta = new();
    private readonly Dictionary<int, string> _numericMeta = new();

    /// <summary>Every property JSON sent via SetDetectorPropertyAsync, captured at call time.</summary>
    public List<JObject> SentProperties { get; } = new();

    /// <summary>Requests the real backend would reject as invalidquery.</summary>
    public List<string> Rejections { get; } = new();

    /// <summary>Optional hook to change what the backend reports back (e.g. clamping).</summary>
    public Func<int, double, double> NumericClamp { get; set; } = (_, v) => v;

    public Mock<IImagerCommunicationManager> Manager { get; } = new();

    public FakeDetectorBackend(string detectorName)
    {
        _detectorName = detectorName;

        Manager
            .Setup(m => m.SetDetectorPropertyAsync(_detectorName, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns<string, object, CancellationToken>((_, prop, _) =>
            {
                var json = JObject.Parse(JsonConvert.SerializeObject(prop));
                SentProperties.Add(json);

                int code = json["propertycode"]!.Value<int>();
                switch (json["kind"]?.Value<string>())
                {
                    case "discrete" when json["current"]?.Type != JTokenType.String:
                        Rejections.Add("parsing Text failed, expected String, but encountered Null");
                        throw new InvalidOperationException("Unexpected response type: UnknownJsonResponse");
                    case "discrete":
                        _categoric[code] = json["current"]!.Value<string>()!;
                        break;
                    case "numeric":
                        _numeric[code] = NumericClamp(code, json["value"]!.Value<double>());
                        break;
                }

                return Task.CompletedTask;
            });

        Manager
            .Setup(m => m.ListAvailableDetectorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<DetectorEquipmentModel> { Snapshot() });
    }

    public FakeDetectorBackend WithCategoric(int code, string descriptor, string current, params string[] options)
    {
        _categoric[code] = current;
        _categoricMeta[code] = (descriptor, options.ToList());
        return this;
    }

    public FakeDetectorBackend WithNumeric(int code, string descriptor, double value)
    {
        _numeric[code] = value;
        _numericMeta[code] = descriptor;
        return this;
    }

    public void SetBackendOptions(int code, params string[] options) =>
        _categoricMeta[code] = (_categoricMeta[code].Descriptor, options.ToList());

    public string CurrentOf(int code) => _categoric[code];
    public double ValueOf(int code) => _numeric[code];

    /// <summary>A fresh model, as ListAvailableDetectorsAsync would return.</summary>
    public DetectorEquipmentModel Snapshot()
    {
        var props = new List<DetectorEquipmentProperties>();
        foreach (var (code, descriptor) in _numericMeta)
            props.Add(new NumericDetectorProperty(descriptor, code, _numeric[code]));
        foreach (var (code, meta) in _categoricMeta)
            props.Add(new CategoricDetectorProperty(meta.Descriptor, code, _categoric[code], new List<string>(meta.Options)));

        return new DetectorEquipmentModel(_detectorName, props.OrderBy(p => p.propertycode).ToList()) { IsEnabled = true };
    }
}

public class DetectorEquipmentViewModelTests
{
    private static DetectorEquipmentViewModel CreateViewModel(FakeDetectorBackend backend)
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        var workspace = new ImagerWorkspace(
            Mock.Of<ILifetimeScope>(),
            loggerFactory.Object,
            Mock.Of<IImagerConnectionHandler>(),
            backend.Manager.Object,
            new SmartProgramRegistry());

        // ExperimentManager is only used to restart live mode; the workspace is idle here.
        return new DetectorEquipmentViewModel(backend.Snapshot(), workspace, null!, backend.Manager.Object);
    }

    private static CategoricDetectorPropertyViewModel Categoric(DetectorEquipmentViewModel vm, int code) =>
        vm.Properties.OfType<CategoricDetectorPropertyViewModel>().Single(p => p.Property.propertycode == code);

    private static NumericDetectorPropertyViewModel Numeric(DetectorEquipmentViewModel vm, int code) =>
        vm.Properties.OfType<NumericDetectorPropertyViewModel>().Single(p => p.Property.propertycode == code);

    private static FakeDetectorBackend DummyCam() =>
        new FakeDetectorBackend("DummyCam1")
            .WithNumeric(0, "Exposure time", 0.1)
            .WithCategoric(1, "Sensor cropping", "64x64", "16x16", "32x32", "64x64", "128x128")
            .WithCategoric(2, "Binning", "1", "1", "2", "4");

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met in time.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    // Hosts one CategoricDetectorPropertyView per categoric property, like the real UI.
    private static (Window Window, Dictionary<int, ComboBox> Combos) ShowCategoricViews(DetectorEquipmentViewModel vm)
    {
        var panel = new StackPanel();
        var combos = new Dictionary<int, ComboBox>();

        foreach (var prop in vm.Properties.OfType<CategoricDetectorPropertyViewModel>())
        {
            var view = new CategoricDetectorPropertyView { DataContext = prop };
            panel.Children.Add(view);
            combos[prop.Property.propertycode] = null!;
        }

        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        foreach (var view in panel.Children.OfType<CategoricDetectorPropertyView>())
        {
            var combo = view.GetLogicalDescendants().OfType<ComboBox>().Single();
            combos[((CategoricDetectorPropertyViewModel)view.DataContext!).Property.propertycode] = combo;
        }

        return (window, combos);
    }

    // ---------- Construction ----------

    [Fact]
    public void Properties_are_created_from_the_backend_model()
    {
        var vm = CreateViewModel(DummyCam());

        Assert.Equal("DummyCam1", vm.Name);
        Assert.Equal(3, vm.Properties.Count);

        var exposure = Numeric(vm, 0);
        Assert.Equal("Exposure time", exposure.Label);
        Assert.Equal(0.1, exposure.Value);

        var cropping = Categoric(vm, 1);
        Assert.Equal("Sensor cropping", cropping.Label);
        Assert.Equal("64x64", cropping.SelectedChoice);
        Assert.Equal(new[] { "16x16", "32x32", "64x64", "128x128" }, cropping.Availableoptions);
    }

    [Fact]
    public void Enabling_the_view_model_updates_the_model()
    {
        var backend = DummyCam();
        var vm = CreateViewModel(backend);

        vm.IsEnabled = false;
        Assert.False(vm.DetectorEquipmentProperties.IsEnabled);

        vm.IsEnabled = true;
        Assert.True(vm.DetectorEquipmentProperties.IsEnabled);
    }

    // ---------- Categoric property view model ----------

    [Fact]
    public void Selecting_a_choice_writes_it_to_the_model()
    {
        var vm = CreateViewModel(DummyCam());
        var binning = Categoric(vm, 2);

        binning.SelectedChoice = "4";

        Assert.Equal("4", ((CategoricDetectorProperty)binning.Property).current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_selection_never_overwrites_the_model(string? value)
    {
        var vm = CreateViewModel(DummyCam());
        var binning = Categoric(vm, 2);

        binning.SelectedChoice = value!;

        Assert.Equal("1", ((CategoricDetectorProperty)binning.Property).current);
    }

    [Fact]
    public void Refresh_from_model_restores_the_selection_after_it_was_nulled()
    {
        var vm = CreateViewModel(DummyCam());
        var binning = Categoric(vm, 2);

        binning.SelectedChoice = null!;
        binning.RefreshFromModel();

        Assert.Equal("1", binning.SelectedChoice);
        Assert.False(binning.IsRefreshingFromModel);
    }

    [Fact]
    public void Refresh_from_model_picks_up_new_options_and_current()
    {
        var vm = CreateViewModel(DummyCam());
        var binning = Categoric(vm, 2);
        var model = (CategoricDetectorProperty)binning.Property;

        model.availableoptions = new List<string> { "1", "8" };
        model.current = "8";
        binning.RefreshFromModel();

        Assert.Equal(new[] { "1", "8" }, binning.Availableoptions);
        Assert.Equal("8", binning.SelectedChoice);
    }

    [Fact]
    public void Refresh_from_model_keeps_the_options_collection_when_options_are_unchanged()
    {
        var vm = CreateViewModel(DummyCam());
        var binning = Categoric(vm, 2);
        var changes = 0;
        binning.Availableoptions.CollectionChanged += (_, _) => changes++;

        binning.RefreshFromModel();

        Assert.Equal(0, changes);
    }

    // ---------- Numeric property view model ----------

    [Fact]
    public void Numeric_refresh_from_model_updates_value_and_label()
    {
        var vm = CreateViewModel(DummyCam());
        var exposure = Numeric(vm, 0);
        var model = (NumericDetectorProperty)exposure.Property;

        model.value = 0.25;
        model.descriptor = "Exposure (s)";
        exposure.RefreshFromModel();

        Assert.Equal(0.25, exposure.Value);
        Assert.Equal("Exposure (s)", exposure.Label);
    }

    [Fact]
    public void Numeric_throttled_value_is_written_to_the_model()
    {
        var vm = CreateViewModel(DummyCam());
        var exposure = Numeric(vm, 0);

        exposure.ThrottledValue = 0.5;

        Assert.Equal(0.5, ((NumericDetectorProperty)exposure.Property).value);
    }

    // ---------- Full set -> re-list -> refresh round trip ----------

    [AvaloniaFact]
    public async Task Selecting_a_categoric_value_sends_it_once_and_never_sends_null()
    {
        // Regression: after a successful set, refreshing the options made the ComboBox push
        // SelectedItem = null back into the view model, which then sent
        // {"current": null} and Imager.Core answered "invalidquery: parsing Text failed ...".
        var backend = DummyCam();
        var vm = CreateViewModel(backend);
        var (window, combos) = ShowCategoricViews(vm);

        combos[2].SelectedItem = "4";
        await WaitUntilAsync(() => backend.SentProperties.Count >= 1);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(backend.Rejections);
        var sent = Assert.Single(backend.SentProperties);
        Assert.Equal(2, sent["propertycode"]!.Value<int>());
        Assert.Equal("4", sent["current"]!.Value<string>());

        Assert.Equal("4", backend.CurrentOf(2));
        Assert.Equal("4", Categoric(vm, 2).SelectedChoice);
        Assert.Equal("4", ((CategoricDetectorProperty)Categoric(vm, 2).Property).current);
        Assert.Equal("4", combos[2].SelectedItem);

        // The other categoric property must be untouched by the refresh.
        Assert.Equal("64x64", combos[1].SelectedItem);
        Assert.Equal("64x64", backend.CurrentOf(1));

        window.Close();
    }

    [AvaloniaFact]
    public async Task Changing_the_same_property_repeatedly_keeps_it_in_sync()
    {
        var backend = DummyCam();
        var vm = CreateViewModel(backend);
        var (window, combos) = ShowCategoricViews(vm);

        foreach (var (choice, n) in new[] { ("2", 1), ("4", 2), ("1", 3) })
        {
            combos[2].SelectedItem = choice;
            await WaitUntilAsync(() => backend.SentProperties.Count >= n);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(choice, backend.CurrentOf(2));
            Assert.Equal(choice, combos[2].SelectedItem);
        }

        Assert.Empty(backend.Rejections);
        Assert.Equal(new[] { "2", "4", "1" }, backend.SentProperties.Select(p => p["current"]!.Value<string>()));

        window.Close();
    }

    [AvaloniaFact]
    public async Task Options_changed_by_the_backend_are_shown_after_a_set()
    {
        // e.g. changing binning shrinks the valid sensor crops.
        var backend = DummyCam();
        var vm = CreateViewModel(backend);
        var (window, combos) = ShowCategoricViews(vm);

        backend.SetBackendOptions(1, "16x16", "32x32", "64x64");
        combos[2].SelectedItem = "2";
        await WaitUntilAsync(() => backend.SentProperties.Count >= 1);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(backend.Rejections);
        Assert.Single(backend.SentProperties);
        Assert.Equal(new[] { "16x16", "32x32", "64x64" }, Categoric(vm, 1).Availableoptions);
        Assert.Equal("64x64", combos[1].SelectedItem);
        Assert.Equal("64x64", ((CategoricDetectorProperty)Categoric(vm, 1).Property).current);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Numeric_value_adjusted_by_the_backend_is_reflected_in_the_view_model()
    {
        var backend = DummyCam();
        backend.NumericClamp = (_, v) => Math.Min(v, 1.0);
        var vm = CreateViewModel(backend);
        var exposure = Numeric(vm, 0);

        exposure.ThrottledValue = 5.0;
        await WaitUntilAsync(() => backend.SentProperties.Count >= 1);

        Assert.Equal(5.0, backend.SentProperties[0]["value"]!.Value<double>());
        Assert.Equal(1.0, backend.ValueOf(0));
        Assert.Equal(1.0, exposure.Value);
        Assert.Equal(1.0, ((NumericDetectorProperty)exposure.Property).value);
    }
}
