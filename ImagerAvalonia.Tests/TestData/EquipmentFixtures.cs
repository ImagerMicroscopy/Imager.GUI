using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;

namespace ImagerAvalonia.Tests.TestData;

/// <summary>Small, realistic equipment set shared by the model/serialization tests.</summary>
internal static class EquipmentFixtures
{
    public static Source Laser(string equipment = "hello", string name = "ls", params string[] activeChannels)
    {
        var source = new Source(allowmultiplechannels: true, cancontrolpower: true,
            channels: new List<string> { "ch1", "ch2" }, name: name)
        {
            EquipmentName = equipment,
        };
        source.LightsourceChannel = activeChannels.ToList();
        source.LightsourcePower = activeChannels.Select(_ => 40).ToList();
        source.IsEnabled = activeChannels.Length > 0;
        return source;
    }

    public static MovableComponentModel FilterWheel(string equipment = "fw1", string filter = "GFP", string slider = "12.5") =>
        new(new List<MovableComponentPart>
        {
            new("fw", new List<string> { "DAPI", "GFP", "RFP" }, "discretemovablecomponent", null!, null!, null!, filter),
            new("sl", null!, "continuousmovablecomponent", "100", "0", "0.5", slider),
        }, equipment);

    public static DetectorEquipmentModel Camera(string name = "DummyCam1", bool enabled = true, string binning = "1") =>
        new(name, new List<DetectorEquipmentProperties>
        {
            new NumericDetectorProperty("Exposure time", 0, 0.1),
            new CategoricDetectorProperty("Binning", 1, binning, new List<string> { "1", "2", "4" }),
        })
        { IsEnabled = enabled };

    public static EquipmentWorkspace Workspace(
        IEnumerable<Source>? sources = null,
        IEnumerable<MovableComponentModel>? filterWheels = null,
        IEnumerable<DetectorEquipmentModel>? detectors = null)
    {
        var workspace = new EquipmentWorkspace();
        workspace.Initialize(
            imagerWorkspace: null!,
            (sources ?? new[] { Laser() }).ToList(),
            (filterWheels ?? new[] { FilterWheel() }).ToList(),
            new List<RobotModel>(),
            (detectors ?? new[] { Camera() }).ToList());
        return workspace;
    }

    public static DetectionParams Detection(
        IEnumerable<Source>? irradiation = null,
        IEnumerable<MovableComponentModel>? filterWheels = null,
        IEnumerable<DetectorEquipmentModel>? detectors = null) =>
        new()
        {
            Irradiation = (irradiation ?? new[] { Laser(activeChannels: "ch1") }).ToList(),
            MovableComponents = (filterWheels ?? new[] { FilterWheel() }).ToList(),
            Detectors = (detectors ?? new[] { Camera() }).ToList(),
        };
}
