using Autofac;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services
{
    public enum RunningAcquisitionState
    {
        IsInExperiment,
        IsInLive,
        Idle
    }


    public class AcquisitionStateService
    {
        public ExperimentalPanelViewModel? SelectedExperiment { get; set; }  
        public AcquisitionSettingsViewModel? SelectedAcquisition { get; set; }
        public event EventHandler? EndLive;
        public event EventHandler? StartLive;

        private readonly IStageControl _stageControl;
        private readonly EquipmentState _equipmentState;
        private readonly SmartProcessingRegisterViewModel _processingViewModel;
        private readonly ComUtils _comUtils;
        public RunningAcquisitionState RunningAcquisitionState { get; set; }  = RunningAcquisitionState.Idle;

        public AcquisitionStateService(IStageControl stageControl, SmartProcessingRegisterViewModel processViewModel, ComUtils comUtils,EquipmentState eqState) 
        {
            _stageControl = stageControl;
            _equipmentState = eqState;
            _processingViewModel = processViewModel;
            _comUtils = comUtils;
        }    

        

        public void GetTifSettings(object? sender, ILifetimeScope scope)
        {
            var storageProvider = scope.Resolve<IStorageProvider>();
            var experimentSerializer = scope.Resolve<IExperimentSerialization>();


            storageProvider.OpenReadStream();
            JObject program = JObject.Parse(storageProvider.GetImagerProgram());
            if (program.TryGetValue("program", out JToken? imager_program) && imager_program is not null)
            {

                ObservableCollection<AcquisitionSettings> acq_settings = EquipmentState.GetAcquisitionsFromImagerProgram(imager_program);
                ObservableCollection<AcquisitionSettingsViewModel> acq_model = new ObservableCollection<AcquisitionSettingsViewModel>(acq_settings.Select(x => new AcquisitionSettingsViewModel(x)));
                UserDefinedAcquisitions user_acqs = new UserDefinedAcquisitions();
                user_acqs.Acquisitions = acq_model;
                ExperimentalPanelViewModel exp = new ExperimentalPanelViewModel(new UserDefinedAcquisitions(acq_settings), _stageControl);

                experimentSerializer.SetExperiment(exp);
                NodeBase exp_nodes = experimentSerializer.GetDeserializedExperiment(imager_program, user_acqs);

                exp.SetExpNodes(new ObservableCollection<NodeBase>() { exp_nodes });
                exp.SetAcquisitions(acq_model);

                experimentSerializer.SetExperiment(exp);
                storageProvider.SetMaxFrameNumber(storageProvider.LoadMaxFrameNumber());
                storageProvider.SetAcqDetPairs(experimentSerializer.GetAcqDetPairs()); 
                if (imager_program.ToObject<JObject>() is JObject loaded_program)
                {
                    storageProvider.SetMeasurementProgram(loaded_program);
                }
                else
                {
                    throw new Exception("Could not de-serialize imager program");
                }
            }
        }


        public void GetLiveSettings(object? sender, ILifetimeScope scope)
        {
            var storageProvider = scope.Resolve<IStorageProvider>();
            var experimentSerializer = scope.Resolve<IExperimentSerialization>();


            if (SelectedAcquisition is not null)
            {
                var imagerSavedProgram = new ImageDefaultFastAcquisitionProgram();

                if (imagerSavedProgram.Program.Program.Elements.Count == 0)
                {
                    imagerSavedProgram.Program.Program.Elements.Add(new Element());
                }

                var firstElement = imagerSavedProgram.Program.Program.Elements[0];
                firstElement.DetectionNames.Clear();
                firstElement.DetectionNames.Add(SelectedAcquisition.Name);

                imagerSavedProgram.Program.DefinedDetections.Clear();
                imagerSavedProgram.Program.DefinedDetections[SelectedAcquisition.Name] =
                    SelectedAcquisition.SerializeAcquisition();

                imagerSavedProgram.Program.SmartProgramCode.Code.Clear();
                imagerSavedProgram.Program.SmartProgramCode.Type = "dagorchestratorcode";

                JObject imager_saved_program = JObject.FromObject(imagerSavedProgram);



                storageProvider.SetAcqDetPairs(SelectedAcquisition.AcqDetPairs);
                storageProvider.SetMaxFrameNumber(0);
                storageProvider.SetMeasurementProgram(imager_saved_program);
            }
            else
            {
                throw new Exception("No acquisition selected");
            }


        }

        public void GetSelectedExperiment(object? sender, ILifetimeScope scope)
        {
            var storageProvider = scope.Resolve<IStorageProvider>();
            var experimentSerializer = scope.Resolve<IExperimentSerialization>();

            if (SelectedExperiment == null)
            {
                throw new Exception("No selected experiment found. Select an experiment from the menu.");
            }

            JObject measurement_program = experimentSerializer.SerializeExperiment(SelectedExperiment, _processingViewModel);
            JObject imager_saved_program = new JObject();

            imager_saved_program["program"] = measurement_program;
            imager_saved_program["kind"] = "Imager saved program";
            imager_saved_program["version"] = 3.0;
            experimentSerializer.SetExperiment(SelectedExperiment);


            var schema = experimentSerializer.GetMaxNumberOfDetectionsInTree(SelectedExperiment);

            storageProvider.SetEnabledStorage(SelectedExperiment.Root.IsExperimentStorageEnabled);
            storageProvider.SetStoragePath(SelectedExperiment.GetStoragePath());
            storageProvider.SetMaxFrameNumber(experimentSerializer.GetMaxNumberOfDetectionsInTree(SelectedExperiment));
            storageProvider.SetAcqDetPairs(experimentSerializer.GetAcqDetPairs()); // null disables loading from schema, instead relying on async traversal.
            storageProvider.SetMeasurementProgram(imager_saved_program);
            
            
        }


        public void SetLiveState()
        {
            RunningAcquisitionState = RunningAcquisitionState.IsInLive;
        }

        public void SetIdleState()
        {
            RunningAcquisitionState = RunningAcquisitionState.Idle;
        }

        public void SetExperimentState()
        {
            RunningAcquisitionState = RunningAcquisitionState.IsInExperiment;
        }

        public void InvokeLiveEnd()
        {
            EndLive?.Invoke(this, new EventArgs());
        }

        public void InvokeLiveStart()
        {
            StartLive?.Invoke(this, new EventArgs());
        }

        internal async Task CheckIfAcquisitionFinsihed()
        {
            string message = string.Empty;
            _comUtils.SendDataRequest(ComUtils.fetchasyncstatus, "", response_message => { message = response_message; }, response_data => { });
            while (!message.Contains("error"))
            {
                _comUtils.SendDataRequest(ComUtils.fetchasyncstatus, "", response_message => { message = response_message; }, response_data => { });
                await Task.Delay(50);
            }
        }
    }
}


public class ImageDefaultFastAcquisitionProgram
{
    [JsonProperty("program")]
    public ProgramContainer Program { get; set; } = new ProgramContainer();

    [JsonProperty("kind")]
    public string Kind { get; set; } = "Imager saved program";

    [JsonProperty("version")]
    public double Version { get; set; } = 3.0;
}

public class ProgramContainer
{
    [JsonProperty("program")]
    public InnerProgram Program { get; set; } = new InnerProgram();

    [JsonProperty("defineddetections")]
    public Dictionary<string, object> DefinedDetections { get; set; } = new();

    [JsonProperty("smartprogramcode")]
    public SmartProgramCode SmartProgramCode { get; set; } = new();

    [JsonProperty("action")]
    public string Action { get; set; } = "executemeasurementprogram";
}

public class InnerProgram
{
    [JsonProperty("elements")]
    public List<Element> Elements { get; set; } = new List<Element>();

    [JsonProperty("elementtype")]
    public string ElementType { get; set; } = "dotimes";

    [JsonProperty("elementid")]
    public Guid ElementID { get; set; } = Guid.NewGuid();

    [JsonProperty("ntotal")]
    public double NTotal { get; set; } = 10000000.0;

    [JsonProperty("smartprogramid")]
    public object? SmartProgramId { get; set; } = null;
}

public class Element
{
    [JsonProperty("elementtype")]
    public string ElementType { get; set; } = "detection";

    [JsonProperty("smartprogramids")]
    public List<object> SmartProgramIds { get; set; } = new List<object>();

    [JsonProperty("detectionnames")]
    public List<string> DetectionNames { get; set; } = new();

    [JsonProperty("elementid")]
    public Guid ElementID { get; set; } = Guid.NewGuid();
}

public class SmartProgramCode
{
    [JsonProperty("code")]
    public List<object> Code { get; set; } = new List<object>();

    [JsonProperty("type")]
    public string Type { get; set; } = "dagorchestratorcode";
}