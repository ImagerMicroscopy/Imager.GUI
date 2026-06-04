using Autofac;
using Avalonia.Controls;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;


namespace ImagerAvalonia.Services.MeasurementControl
{


    public enum TreeMeasurementType
    { 
        ActionType,
        ExperimentType
        
    }

    public interface IMeasurementTypeRegistry
    {
        void Register<T>(string description) where T : IMeasurementTypes;
        Type GetTypeByDescription(string description);
    }

    public class MeasurementTypeRegistry : IMeasurementTypeRegistry
    {
        private readonly Dictionary<string, Type> _descriptionToType = new();
        private readonly Dictionary<Type, string> _typeToDescription = new();

        public void Register<T>(string description) where T : IMeasurementTypes
        {
            var type = typeof(T);
            _descriptionToType[description] = type;
            _typeToDescription[type] = description;
        }

        public Type GetTypeByDescription(string description)
        {
            if (_descriptionToType.TryGetValue(description, out var type))
                return type;

            throw new NotImplementedException($"Measurement type '{description}' not implemented.");
        }

       

        
    }



    public interface IMeasurementTypes
    {
        public string MeasurementName { get; }

        public Avalonia.Controls.UserControl MeasurementView { get; }
        public TreeMeasurementType NodeType { get; }

        public JObject Serialize(IExperimentSerialization experimentSerialization);
        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings);
        public void DetectionWiseTraversal(NodeBase node, TraversalState traversal);
    }







    public class Detection :  IMeasurementTypes
    {
        
        public Detection(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ActionType;
            MeasurementName = "Detection";
            MeasurementView = new Views.AcquisitionPanelView(availableAcquisitions);
            if (MeasurementView.DataContext is AcquisitionPanelViewModel acq)
            {
                AcquisitionViewModel = acq;
            }
        }
        public TreeMeasurementType NodeType { get; }
        public string MeasurementName { get;  }
        public List<Guid> SmartProgramIDS = new();
        private AcquisitionPanelViewModel AcquisitionViewModel;
        public string ElementType = "detection";
        public UserControl MeasurementView { get; }

        

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (parameters is not null &&
                parameters.TryGetValue("detectionnames",out JToken? detection_names) )
            {


                foreach (var item in AcquisitionViewModel.IsAquisitionEnabled)
                {
                    item.IsEnabled = false;
                }


                foreach (JToken detection_name in (JArray)detection_names)
                {
                    string ref_name = detection_name.ToString();
                    if(experimentSerialization.AcquisitionMaps.TryGetValue(detection_name.ToString(), out string? newname))
                    {
                        ref_name = newname;
                    }
  
                        
                    var acq_setting = AcquisitionViewModel.IsAquisitionEnabled.Where(x => x.Name == ref_name).First();

                    if (acq_setting != null)
                    {
                        acq_setting.IsEnabled = true;
                        var acq_setting_vm = AcquisitionViewModel.AvailableAcquisitions.Where(x => x.Name == acq_setting.Name).First();

                        foreach (DetectorEquipmentViewModel det in acq_setting_vm.Detector)
                        {
                            if (det.IsEnabled)
                            {
                                var acqdetpair = new AcqDetPair(acq_setting_vm.AcquisitionSettings, det.Name);
                                if (!acq_setting_vm.AcqDetPairs.Contains(acqdetpair))
                                {
                                    acq_setting_vm.AcqDetPairs.Add(acqdetpair);
                                }
                            }
                        }
                    }
                }
            }
        }



        public void DetectionWiseTraversal(NodeBase node, TraversalState traversal)
        {
            traversal.UpdateLast();
        }



        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {
            if (AcquisitionViewModel.IsAquisitionEnabled.ToList().Where(x => x.IsEnabled).Count() != 0)
            {
                JArray EnabledAcquisitions = JArray.FromObject(
                    AcquisitionViewModel.IsAquisitionEnabled
                        .Where(x => x.IsEnabled && x.acquisition?.Name != null)
                        .Select(x => x.acquisition?.Name ?? string.Empty)
                        .ToList()
                );
                JObject SerializedAcquisition = new JObject(new JProperty("elementtype", ElementType));
                SerializedAcquisition["detectionnames"] = EnabledAcquisitions;
                SerializedAcquisition["smartprogramids"] = new JArray(SmartProgramIDS);
                SerializedAcquisition["elementid"] = AcquisitionViewModel.Elementid;

                return SerializedAcquisition;
            }
            else
            {
                throw new Exception($"Acquisition element {AcquisitionViewModel.Elementid} contains no enabled acquisitions");
            }
        }
    }









    public class Irradiation : IMeasurementTypes
    {


        public Irradiation(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ActionType;
            MeasurementName = "Irradiation";
            ElementType = "irradiation";
            MeasurementView = new Views.IrradiationPanelView(availableAcquisitions);
            UserAcquisitionSettings = availableAcquisitions;


        }
        public ObservableCollection<AcquisitionSettingsViewModel> AvailableAcquisitions => UserAcquisitionSettings.Acquisitions;
        public SystemDefinedSettingsViewModel UserAcquisitionSettings { get; set; }
        public string ElementType;
        public TreeMeasurementType NodeType { get;  }
        public string MeasurementName { get; }
        public NodeBase Node { get; set; }
        public UserControl MeasurementView { get; }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (MeasurementView.DataContext is not IrradiationPanelViewModel irr_settings)
                return; 
            if (parameters != null)
            {
                foreach(JToken src in parameters["irradiation"])
                {
                    var sourcevm = irr_settings.SourcesViewModels.ToList().Find(x => x.EquipmentName == src["equipmentname"].ToString() &&
                    x.LightSource.LightSourceName == src["lightsourcename"].ToString());


                    for(int i=0; i < src["lightsourcechannel"].Count(); i++)
                    {
                        foreach(var channel in sourcevm.Channels)
                        {
                            if(channel.Name == src["lightsourcechannel"][i].ToString())
                            {
                                channel.PowerLevel = src["lightsourcepower"][i].ToObject<int>();
                                channel.IsEnabled = true;
                            }

                        }
                    }

                }
                irr_settings.IrradiationTimes = (double?)parameters["duration"] ?? 0.0;
            }
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState traversal)
        {

        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {


            if (MeasurementView.DataContext is IrradiationPanelViewModel irr_settings)
            {
                List<Source> enabled_sources = new List<Source> { };
                foreach (Source s in irr_settings.SourcesViewModels.Select(x => x.LightSource))
                {
                    if (s.LightsourceChannel.Count > 0)
                    {
                        enabled_sources.Add(s);
                    }

                }

                JObject SerializedIrradiation = new JObject(new JProperty("elementtype", ElementType));

                SerializedIrradiation["irradiation"] = JArray.FromObject(enabled_sources.Select(x => x.Serialize()).ToList()); ;
                SerializedIrradiation["duration"] = irr_settings.IrradiationTimes;
                SerializedIrradiation["elementid"] = irr_settings.Elementid;
                return SerializedIrradiation;
            }
            else
            {
                throw new Exception("No acquisitions enabled in irradiation");
            }

        }
    }



    public class Robot : IMeasurementTypes
    {
        public string MeasurementName => "Robot";
        public string ElementType => "executerobotprogram";
        public UserControl MeasurementView { get; }

        public TreeMeasurementType NodeType => TreeMeasurementType.ActionType;

        public Robot(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            MeasurementView = new RobotControlView(availableAcquisitions);

        }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (MeasurementView.DataContext is RobotControlViewModel robot_settings && parameters is not null)
            {
                if(parameters.TryGetValue("programparameters", out var program_params))
                {
                    string? robot_name = program_params["robotname"]?.Value<string>();
                    int index = currentSettings.Robots.FindIndex(x => x.robotname == robot_name);

                    if (index!=-1 && program_params["programcallparameters"] is JToken programcall_params)
                    {
                        string? program_name = programcall_params["programname"]?.Value<string>();

                        var found_robot = currentSettings.Robots[index];
                        int found_program_ind = found_robot.robotPrograms.FindIndex(y => y.programname == program_name); 
                       
                        if(found_program_ind!=-1)
                        {
                            var found_program = found_robot.robotPrograms[found_program_ind].programname;

                            robot_settings.SelectedRobot = robot_settings.Robots.
                                First(x => x.RobotName == robot_name);
                            robot_settings.SelectedRobot.SelectedRobotProgram = robot_settings.SelectedRobot.RobotPrograms.
                                First(x => x.ProgramName == found_program);

                            var program = robot_settings.SelectedRobot.SelectedRobotProgram;

                            JArray? args = programcall_params["arguments"] as JArray;

                            if (args != null)
                            {
                                foreach (JToken arg in args)
                                {
                                    string? name = arg["argumentname"]?.Value<string>();
                                    string? type = arg["robotprogramargumenttype"]?.Value<string>();
                                    string? value = arg["argument"]?.Value<string>();

                                    if (program.ProgramArguments.First(x => x.ProgramArgumentName == name) is var programArgument && value !=null)
                                    {
                                        if (programArgument is DiscreteArgumentsViewModel discrecteArgumentSetting &&
                                            discrecteArgumentSetting.PermissibleValues.Contains(value))
                                        {
                                            discrecteArgumentSetting.SelectedValue = value;
                                        }
                                        if (programArgument is ContinuousArgumentsViewModel continuousArgumentSetting)
                                        {
                                            if (float.TryParse(value, out float numeric_val))
                                            {
                                                continuousArgumentSetting.SelectedValue = numeric_val;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState traversal)
        {

        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {
            if(MeasurementView.DataContext is RobotControlViewModel vm)
            {
                JObject serializedRobot = new JObject(new JProperty("elementtype", ElementType));
                serializedRobot["elementid"] = vm.Elementid;
                serializedRobot["programparameters"] = vm.Serialize();

                string val = serializedRobot.ToString();
                return serializedRobot;
            }
            throw new Exception($"Robot element contains invalid view model type");

        }
    }

    public class WaitForTime :  IMeasurementTypes
    {


        public WaitForTime(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            ElementType = "wait"; 
            MeasurementName = "Wait";
            MeasurementView = new Views.WaitView(availableAcquisitions);

        }
        public ObservableCollection<AcquisitionSettings> AvailableAcquisitions { get; } = new();
        public TreeMeasurementType NodeType => TreeMeasurementType.ActionType;
        public string ElementType { get;  }
        public NodeBase? Node { get; set; }
        public string MeasurementName { get; }

        public UserControl MeasurementView { get; }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (MeasurementView.DataContext is WaitViewModel wait_settings && parameters is not null)
            {
                if (parameters.TryGetValue("duration", out JToken? duration))
                {
                    if (double.TryParse(duration.ToString(), out double value))
                    {
                        wait_settings.WaitPeriod = value;
                    }
                }
            }
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {
          
        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {
            if (MeasurementView.DataContext is WaitViewModel wait_settings)
            {
                JObject SerializedWait = new JObject(new JProperty("elementtype", ElementType));
                SerializedWait["duration"] = wait_settings.WaitPeriod;
                SerializedWait["elementid"] = wait_settings.Elementid;
                return SerializedWait;
            }
            else
            {
                throw new Exception($"Wait element contains invalid view model type");
            }
        }
    }


    public class UpdateAcquisition: IMeasurementTypes
    {

        public UpdateAcquisition(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ActionType;
            ElementType = "updateacquisition";
            MeasurementName = "Update Acquisition";
            MeasurementView = new Views.UpdateAcquisitionView(availableAcquisitions);

        }
        public ObservableCollection<AcquisitionSettings> AvailableAcquisitions { get; } = new();
        public TreeMeasurementType NodeType { get; }
        public string ElementType { get; }
        public NodeBase? Node { get; set; }
        public string MeasurementName { get; }

        public UserControl MeasurementView { get; }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {

        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {

        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {

            var update_acquisition_params = new JObject();
            if ( MeasurementView.DataContext is UpdateAcquisitionViewModel updateVM)
            {
                update_acquisition_params["elementtype"] = ElementType;
                update_acquisition_params["elementid"] = updateVM.Elementid;
                if(updateVM.ToUpdateAcquisitions.Where(x => x.Enabledupdate).Count()==0)
                {
                    throw new Exception("No Acquisitions selected in Acquisition Update element.");
                }
                update_acquisition_params["detectionname"] = updateVM.ToUpdateAcquisitions.Where(x=> x.Enabledupdate).Select(x => x.Name).First();
                if (updateVM.SelectedProgramId is not null)
                {
                    update_acquisition_params["smartprogramid"] = updateVM.SelectedProgramId.SmartProgramID.ToString();
                }
                else
                {
                    throw new Exception("Update Acquisition element can not be used without a smart program binding.");
                }
            }

            return update_acquisition_params;
        }
    }








    public class RelativeStageLoop : IMeasurementTypes
    {

        public RelativeStageLoop(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ExperimentType;
            ElementType = "relativestageloop";
            MeasurementName = "Relative stage loop";
            MeasurementView = new Views.RelStageView(availableAcquisitions);


        }

        public TreeMeasurementType NodeType { get; }
        public string ElementType { get; }
        public NodeBase Node { get; set; }

        public string MeasurementName { get; }
        public string MeasurementType { get; }

        public UserControl MeasurementView { get; set; }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (parameters is not null  && MeasurementView.DataContext is RelStageViewModel relstage_settings &&
                parameters["params"] is JObject paramsObj)
            {
                var paramsx = paramsObj["additionalplanesx"]?.ToObject<JArray>();
                if (paramsx is { Count: >= 2 })
                {
                    relstage_settings.TileNegativeX = (decimal?)paramsx[0] ?? 0m;
                    relstage_settings.TilePositiveX = (decimal?)paramsx[1] ?? 0m;
                }

                var paramsy = paramsObj["additionalplanesy"]?.ToObject<JArray>();
                if (paramsy is { Count: >= 2 })
                {
                    relstage_settings.TileNegativeY = (decimal?)paramsy[0] ?? 0m;
                    relstage_settings.TilePositiveY = (decimal?)paramsy[1] ?? 0m;
                }

                var paramsz = paramsObj["additionalplanesz"]?.ToObject<JArray>();
                if (paramsz is { Count: >= 2 })
                {
                    relstage_settings.TileNegativeZ = (decimal?)paramsz[0] ?? 0m;
                    relstage_settings.TilePositiveZ = (decimal?)paramsz[1] ?? 0m;
                }

                relstage_settings.StepSizeX = (decimal?)paramsObj["deltax"] ?? 0m;
                relstage_settings.StepSizeY = (decimal?)paramsObj["deltay"] ?? 0m;
                relstage_settings.StepSizeZ = (decimal?)paramsObj["deltaz"] ?? 0m;

                relstage_settings.ReturnToStartingPosition =
                    (bool?)paramsObj["returntostartingposition"] ?? false;

              
            }
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {
            if (MeasurementView.DataContext is RelStageViewModel relstage_settings)
            {
                for (int stepx = 0; stepx <= relstage_settings.NumStepsX; stepx++)
                {
                    for (int stepy = 0; stepy <= relstage_settings.NumStepsY; stepy++)
                    {
                        for (int stepz = 0; stepz <= relstage_settings.NumStepsZ; stepz++)
                        {
                            node.TraverseMeasurement(state);
                        }
                    }
                }
            }

        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {
            if (MeasurementView.DataContext is not RelStageViewModel relstage_settings)
                return new JObject();

            var stage_params = new JObject
            {
                ["elementtype"] = ElementType,
                ["params"] = new JObject()
            };

            var paramsObj = (JObject)stage_params["params"]!;

            paramsObj["additionalplanesx"] = new JArray(
                new List<decimal>
                {
                    relstage_settings.TileNegativeX ?? 0m,
                    relstage_settings.TilePositiveX ?? 0m
                });

            paramsObj["additionalplanesy"] = new JArray(
                new List<decimal>
                {
                    relstage_settings.TileNegativeY ?? 0m,
                    relstage_settings.TilePositiveY ?? 0m
                });

            paramsObj["additionalplanesz"] = new JArray(
                new List<decimal>
                {
                    relstage_settings.TileNegativeZ ?? 0m,
                    relstage_settings.TilePositiveZ ?? 0m
                });

            paramsObj["deltax"] = relstage_settings.StepSizeX ?? 0m;
            paramsObj["deltay"] = relstage_settings.StepSizeY ?? 0m;
            paramsObj["deltaz"] = relstage_settings.StepSizeZ ?? 0m;

            paramsObj["returntostartingposition"] = relstage_settings.ReturnToStartingPosition;

            stage_params["stagename"] = relstage_settings.StageName ?? string.Empty;
            stage_params["smartprogramid"] = null;
            stage_params["elementid"] = relstage_settings.Elementid;

            // Handle optional program ID safely
            if (relstage_settings.FromProgramId &&
                relstage_settings.SelectedProgramId?.SmartProgramID is { } programId)
            {
                stage_params["smartprogramid"] = programId.ToString();
            }

            return stage_params;

        }
    }














    public class DoTimes : IMeasurementTypes
    {

        public DoTimes(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ExperimentType;
            MeasurementName = "Do Times";
            ElementType = "dotimes";
            MeasurementView = new Views.DoTimesView(availableAcquisitions);


        }
        public TreeMeasurementType NodeType { get; }
        public string ElementType { get; }
        public NodeBase Node { get; set; }

        public string MeasurementName { get; }
        public string MeasurementType { get; }
        public UserControl MeasurementView { get; }

        public void Deserialize(JObject parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (MeasurementView.DataContext is DoTimesViewModel dotimes_settings &&
                parameters["ntotal"] is JToken nTotalToken)
            {
                dotimes_settings.num_frames = (int?)nTotalToken ?? 0;
            }
        }


        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {


            if (MeasurementView.DataContext is DoTimesViewModel dotimes_settings)
            {
                for (int i = 0; i < dotimes_settings.num_frames; i++)
                {

                    node.TraverseMeasurement(state);
                }
            }
        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {
            if (MeasurementView.DataContext is not DoTimesViewModel dotimes_settings)
                return new JObject(); // or handle error appropriately

            var dotimes_params = new JObject
            {
                ["elementtype"] = ElementType,
                ["ntotal"] = dotimes_settings.NumRepeats,
                ["smartprogramid"] = null,
                ["elementid"] = dotimes_settings.Elementid
            };

            // Safely handle optional program ID
            if (dotimes_settings.FromProgramId &&
                dotimes_settings.SelectedProgramId?.SmartProgramID is { } programId)
            {
                dotimes_params["smartprogramid"] = programId.ToString();
            }

            return dotimes_params;

        }
    }











    public class StageLoop : IMeasurementTypes
    {

        public bool IsSerializable { get; private set; } = true;

        public StageLoop(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ExperimentType;
            MeasurementName = "Stage loop";
            ElementType = "stageloop";
            MeasurementView = new StageLoopView(availableAcquisitions);
                
                //new Views.StageLoopView(availableAcquisitions);



        }

        public NodeBase Node { get; set; }
        public TreeMeasurementType NodeType { get; }
        public string ElementType { get; }
        public string MeasurementName { get; }
        public string MeasurementType { get; }

        public UserControl MeasurementView { get; }


        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {

            if(MeasurementView.DataContext is StageLoopViewModel stageloop_settings)
            {
                JObject stageloop_params = new JObject();
                stageloop_params["elementtype"] = ElementType;
                JArray pos_data = new JArray();
                if (stageloop_settings.XYPositions.Count == 0)
                {
                    throw new Exception("Encountered a stage loop that has no positions defined during serialization. Please add at least one XYZ position.");
                }
                foreach (XYStagePosition xyz in stageloop_settings.XYPositions)
                {
                    experimentSerialization.TryAddStagePosition(xyz);
                    JObject coords = new JObject();
                    coords["hardwareautofocusoffset"] = xyz.PFSOffset;
                    coords["usinghardwareautofocus"] = xyz.IsPFSEnabled;
                    coords["x"] = xyz.XPos;
                    coords["y"] = xyz.YPos;
                    coords["z"] = xyz.ZPos;


                    JObject coords_data = new JObject();
                    coords_data["coordinates"] = coords;

                    if(string.IsNullOrEmpty(xyz.Name) || string.IsNullOrWhiteSpace(xyz.Name))
                    {
                        throw new Exception("One or more stage positions have an empty name");
                    }
                    coords_data["name"] = xyz.Name;

                    pos_data.Add(coords_data);
                }

                stageloop_params["positions"] = pos_data;
                stageloop_params["stagename"] = stageloop_settings.StageName;
                stageloop_params["smartprogramid"] = null;
                stageloop_params["elementid"] = stageloop_settings.Elementid;
                if (stageloop_settings.FromProgramId && stageloop_settings.SelectedProgramId is not null)
                {

                    stageloop_params["smartprogramid"] = stageloop_settings.SelectedProgramId.SmartProgramID.ToString();
                }
                return stageloop_params;
            }
            return new JObject();
        }

        public void Deserialize(JObject? parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (parameters is not null && MeasurementView.DataContext is StageLoopViewModel stageloop_settings)
            {
                stageloop_settings.XYPositions = new();
                stageloop_settings.XYPositions.CollectionChanged += stageloop_settings.XYPositions_CollectionChanged;
                parameters.TryGetValue("positions", out var position_params);
                if (position_params is not null && position_params is JArray positions)
                {
                    foreach (JObject pos in positions)
                    {
                        if (pos.TryGetValue("coordinates", out var xy_properties) && pos.TryGetValue("name", out var name))
                        {
                            var xyz = JsonConvert.DeserializeObject<XYStagePosition>(xy_properties.ToString());
                            if (xyz != null)
                            {
                                xyz.Name = name.Value<string>();
                                stageloop_settings.XYPositions.Add( xyz);
                                experimentSerialization.TryAddStagePosition(xyz);                     
                            }
                        }
                    }
                }
            }
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {
            if (MeasurementView.DataContext is StageLoopViewModel stageloop_settings &&
                stageloop_settings.XYPositions is not null)
            {
                foreach (var xy_pos in stageloop_settings.XYPositions)
                {
                    node?.TraverseMeasurement(state);
                }
            }

        }
    }










    public class TimeLapse : IMeasurementTypes
    {

        public TimeLapse(SystemDefinedSettingsViewModel availableAcquisitions)
        {
            NodeType = TreeMeasurementType.ExperimentType;
            MeasurementName = "Time lapse";
            ElementType = "timelapse";
            MeasurementView = new Views.TimeLapseView(availableAcquisitions);


        }
        public TreeMeasurementType NodeType { get; }
        public string ElementType { get; }
        public NodeBase Node { get; set; }

        public string MeasurementName { get; }
        public string MeasurementType { get; }
        public UserControl MeasurementView { get; }

        public void Deserialize(JObject parameters, IExperimentSerialization experimentSerialization, SystemDefinedSettingsViewModel currentSettings)
        {
            if (MeasurementView.DataContext is not TimeLapseViewModel timelapse_settings)
                return; 

            timelapse_settings.NTimes = (double?)parameters["ntotal"] ?? 0.0;
            timelapse_settings.TimeDelta = (decimal?)parameters["timedelta"] ?? 0m;
        }

        public void DetectionWiseTraversal(NodeBase node, TraversalState state)
        {
            if (MeasurementView.DataContext is TimeLapseViewModel timelapse_settings)
            {
                for (int i = 0; i < timelapse_settings.NTimes; i++)
                {
                    node.TraverseMeasurement(state);
                }
            }
        }

        public JObject Serialize(IExperimentSerialization experimentSerialization)
        {

            if (MeasurementView.DataContext is not TimeLapseViewModel timelapse_settings)
                return new JObject(); 

            var timelapse_params = new JObject
            {
                ["elementtype"] = ElementType,
                ["ntotal"] = timelapse_settings.NTimes ?? 0,
                ["timedelta"] = timelapse_settings.TimeDelta ?? 0m,
                ["elementid"] = timelapse_settings.Elementid,
                ["smartprogramid"] = null
            };

            if (timelapse_settings.FromProgramId &&
                timelapse_settings.SelectedProgramId?.SmartProgramID is { } programId)
            {
                timelapse_params["smartprogramid"] = programId.ToString();
            }
            return timelapse_params;
        }
    }
}
