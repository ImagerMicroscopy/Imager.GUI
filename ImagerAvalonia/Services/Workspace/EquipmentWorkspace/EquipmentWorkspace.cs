using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Owns the set of available equipment (sources, filter wheels, robots, detectors)
/// and ensures a default acquisition exists. Decoupled from experiment lifecycle.
/// </summary>
public class EquipmentWorkspace
{
    public DefinedDetection? DefaultAcquisition;
    private int _numAcquisition = 1;

    public IReadOnlyList<Source> AvailableSources { get; private set; } = new List<Source>();
    public IReadOnlyList<MovableComponentModel> AvailableFilterWheels { get; private set; } = new List<MovableComponentModel>();
    public IReadOnlyList<RobotModel> AvailableRobots { get; private set; } = new List<RobotModel>();
    public IReadOnlyList<DetectorEquipmentModel> AvailableDetectors { get; private set; } = new List<DetectorEquipmentModel>();

    
    public int NumAcquisition
    {
        get => _numAcquisition;
        set => _numAcquisition = value;
    }

    public void IncrementNumAcquisition() => _numAcquisition++;

    public void Initialize(
        ImagerWorkspace imagerWorkspace,
        List<Source> availableSources,
        List<MovableComponentModel> availableFilterWheels,
        List<RobotModel> availableRobots,
        List<DetectorEquipmentModel> availableDetectors)
    {
        AvailableSources = new List<Source>(availableSources);
        AvailableFilterWheels = new List<MovableComponentModel>(availableFilterWheels);
        AvailableRobots = new List<RobotModel>(availableRobots);
        AvailableDetectors = new List<DetectorEquipmentModel>(availableDetectors);


        EnsureDefaultAcquisition(imagerWorkspace);
    }

    private void EnsureDefaultAcquisition(ImagerWorkspace imagerWorkspace)
    {
        if (DefaultAcquisition != null)
            return;

        DefaultAcquisition = DetectionSettingsFactory.FromComponents(
            "NewAcq",
            AvailableSources.ToList(),
            AvailableFilterWheels.ToList(),
            AvailableDetectors.ToList());
    }
}