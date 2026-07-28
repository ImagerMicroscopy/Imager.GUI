using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia;

public sealed record EquipmentInitResult(
    List<DetectorEquipmentModel> Detectors,
    List<Source> Sources,
    List<MovableComponentModel> FilterWheels,
    List<RobotModel> Robots)
{
    public EquipmentInitResult()
        : this([], [], [], [])
    {
    }
}
/// <summary>
/// Responsible for instantiation of equipment from Imager
/// </summary>
public class EquipmentCoordinator
{
    private IStageControl _stageControl;
    private IImagerCommunicationManager _communicationManager;
    private EquipmentState _equipmentState;

    public EquipmentCoordinator(IStageControl stageControl,
        IImagerCommunicationManager communicationManager,
        EquipmentState state)
    {
        _stageControl = stageControl;
        _communicationManager = communicationManager;
        _equipmentState = state;
    }

    private async Task<EquipmentInitResult> FetchAsync(
        IImagerCommunicationManager comm,
        EquipmentState state)
    {
        await comm.CancelMeasurementProgramAsync();

        var detectors = (await comm.ListAvailableDetectorsAsync()).ToList();
        var eq = await comm.ListAvailableEquipmentAsync();
        await _stageControl.InitializeStageInfo();
        return new EquipmentInitResult(
            Detectors: detectors,
            Sources: state.ParseAvailableLightSources(eq),
            FilterWheels: state.ParseAvailableFilterWheels(eq),
            Robots: state.ParseAvailableRobots(eq));
    }

    public async Task<EquipmentInitResult> FetchEquipment()
    {

        var result = await FetchAsync(_communicationManager, _equipmentState);
        return result;
    }
}


public enum EquipmentPropertyType
{
    LightSourceProperty,
    MovableComponentProperty,
    DetectorProperty
}


public class EquipmentState
{
    public List<EquipmentProperty> EquipmentProperties = new();

    public EquipmentState()
    {

    }

    public List<MovableComponentModel> ParseAvailableFilterWheels(List<EquipmentContainer> eq)
    {
        var availableFilterWheels = new List<MovableComponentModel>();
        for (int fw = 0; fw < eq.Count; fw++)
        {
            if (eq[fw].availablemovablecomponents.Count != 0)
            {
                availableFilterWheels.Add(new MovableComponentModel(eq[fw].availablemovablecomponents, eq[fw].name));
                foreach (var component in eq[fw].availablemovablecomponents)
                {
                    EquipmentProperties.Add(new EquipmentProperty()
                    {
                        EquipmentPath = new List<string>() { eq[fw].name, component.Name },
                        EquipmentType = EquipmentPropertyType.MovableComponentProperty
                    });
                }

            }
        }
        return availableFilterWheels;
    }

    public List<RobotModel> ParseAvailableRobots(List<EquipmentContainer> eq)
    {
        var robots = new List<RobotModel>();
        for (int rb = 0; rb < eq.Count; rb++)
        {
            if (eq[rb].availablerobots.Count != 0)
            {
                foreach (var robot in eq[rb].availablerobots)
                {
                    robot.EquipmentName = eq[rb].name;
                }

                robots.AddRange(eq[rb].availablerobots);
            }
        }
        return robots;
    }


    public List<Source> ParseAvailableLightSources(List<EquipmentContainer> eq)
    {
        var availableSources = new List<Source>();

        for (int sc = 0; sc < eq.Count; sc++)
        {
            if (eq[sc].availablelightsources.Count != 0)
            {
                foreach (var lightsource in eq[sc].availablelightsources)
                {
                    lightsource.EquipmentName = eq[sc].name;
                    availableSources.Add(lightsource);
                    foreach (string channelname in lightsource.AvailableChannels)
                    {
                        EquipmentProperties.Add(new EquipmentProperty()
                        {
                            EquipmentPath = new List<string>() { lightsource.EquipmentName, lightsource.LightSourceName, channelname },
                            EquipmentType = EquipmentPropertyType.LightSourceProperty
                        });

                    }
                }
            }
        }
        return availableSources;
    }

    //public List<Stages> ParseAvailableStages
}

public class EquipmentProperty
{
    public List<string> EquipmentPath;
    public EquipmentPropertyType EquipmentType;
}

