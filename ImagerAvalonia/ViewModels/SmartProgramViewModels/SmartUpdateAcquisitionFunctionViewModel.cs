using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class SmartUpdateAcquisitionFunctionViewModel : ViewModelBase
    {
        [ObservableProperty] Guid _elementID =  Guid.Empty;
        [ObservableProperty] string _acquisitionUpdate;
        [ObservableProperty] SmartProgramInput? _smartProgramBinding;
        [ObservableProperty] ObservableCollection<UpdateAcquisitionFunctionParameterViewModel> _acquisitionUpdateParameters = new();

        public SmartProgramViewModel SmartProgramViewModel { get; set; }

        public Guid SmartProgramID { get; set; }
        private ActionNode? _selectedNode;
        public ActionNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if(value is null)
                {
                    _selectedNode = null;
                }
   
                if (value is ActionNode detectionNode)
                {
                    if (_selectedNode is not null)
                    {

                        if (_selectedNode.MeasurementType is UpdateAcquisition updateAcq &&
                            updateAcq.MeasurementView.DataContext is UpdateAcquisitionViewModel updateAcqVM)
                        {

                            updateAcqVM.SelectedProgramId = null;

                        }
                        ClearUpdateAcquisitionNodeBindings(_selectedNode);
                    }
                    _selectedNode = detectionNode;
                    if (detectionNode.MeasurementType is UpdateAcquisition updateAcqNew &&
                        updateAcqNew.MeasurementView.DataContext is UpdateAcquisitionViewModel updateAcqVMNew)
                    {

                        updateAcqVMNew.SelectedProgramId = SmartProgramViewModel;
                        ClearUpdateAcquisitionNodeBindings(detectionNode);
                    }

                    SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel> smBinding =
                        new SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel>(this);
                    SmartProgramBinding = smBinding;
                    SmartProgramBinding.SmartProgramID = SmartProgramID;
                    ElementID = detectionNode.NodeViewModel.Elementid;
                    detectionNode.SmartProgramBindings = new ObservableCollection<SmartProgramInput> { smBinding };
                }
            }
        }

        public EquipmentState EquipmentState { get; set; }
        public InputFunction? UpdateParameters { get;
            set
            {
                AcquisitionUpdateParameters.Clear();
                foreach (var param in  value.method_params)
                {
                    AcquisitionUpdateParameters.Add(
                        new UpdateAcquisitionFunctionParameterViewModel()
                        {
                            ParameterName = param,
                            EquipmentPaths = new ObservableCollection<string>(
                                EquipmentState.EquipmentProperties.Select(x => string.Join('/', x.EquipmentPath))),
                            SelectedEquipmentPaths = EquipmentState.EquipmentProperties.Select(x => x.EquipmentPath).ToList(),
                            EquipmentProperties = EquipmentState.EquipmentProperties
                        }
                        );
                }
            } 
        }

        public SmartUpdateAcquisitionFunctionViewModel()
        {

        }

        private void ClearUpdateAcquisitionNodeBindings(ActionNode node)
        {
            foreach (var binding in node.SmartProgramBindings)
            {
                if (binding is SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel> vm)
                {
                    vm.SmartProgramInputVM.SelectedNode = null;
                    vm.SmartProgramInputVM.SmartProgramBinding = null;
                    
                }
            }
            node.SmartProgramBindings.Clear();
        }

        public void RemoveExperimentBindings()
        {
            if (SelectedNode is not null)
            {
                foreach (var binding in SelectedNode.SmartProgramBindings)
                {
                    if (binding is SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel> vm)
                    {
                        vm.SmartProgramInputVM.SmartProgramBinding = null;
                    }
                }
                SelectedNode.SmartProgramBindings?.Clear();
                SmartProgramBinding = null;

                if (SelectedNode.MeasurementType is UpdateAcquisition updateAcqNew &&
                    updateAcqNew.MeasurementView.DataContext is UpdateAcquisitionViewModel updateAcqVMNew)
                {

                    updateAcqVMNew.SelectedProgramId = null;

                }
            }

        }

        internal void NodeDeleted(object? sender, ViewModelBase? e)
        {
            RemoveExperimentBindings();
        }
    }


    public class UpdateAcquisitionFunctionConverter : JsonConverter<SmartUpdateAcquisitionFunctionViewModel>
    {
        public override SmartUpdateAcquisitionFunctionViewModel? ReadJson(JsonReader reader, Type objectType,
            SmartUpdateAcquisitionFunctionViewModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var viewModel = new SmartUpdateAcquisitionFunctionViewModel();
            viewModel.AcquisitionUpdate = obj["acquisition_updates"]?.ToString() ?? string.Empty; 

            return viewModel;
        }

        public override void WriteJson(JsonWriter writer, SmartUpdateAcquisitionFunctionViewModel value, JsonSerializer serializer)
        {
           
            writer.WriteStartObject();
            writer.WritePropertyName("elementid");
            writer.WriteValue(value.ElementID);
            writer.WritePropertyName("acquisitionupdatefunction");
            writer.WriteValue(value.AcquisitionUpdate);
            writer.WritePropertyName("acquisitionupdateparameters");
            writer.WriteStartArray();

            foreach (var param in value.AcquisitionUpdateParameters)
            {
                serializer.Serialize(writer, param);
            }

            writer.WriteEndArray();
        }
    }

    public partial class UpdateAcquisitionFunctionParameterViewModel : ViewModelBase
    {
        [ObservableProperty] string _parameterName;
        [ObservableProperty] ObservableCollection<string> _equipmentPaths;

        [ObservableProperty] string _selectedPath;
        [ObservableProperty] int _selectedPathIndex;

        public List<List<string>> SelectedEquipmentPaths = new();

        [JsonConverter(typeof(StringEnumConverter))] public EquipmentPropertyType PropertyType;
        [JsonIgnoreAttribute] public  List<EquipmentProperty> EquipmentProperties;

        partial void OnSelectedPathIndexChanged(int value)
        {
            PropertyType = EquipmentProperties[value].EquipmentType;
        }

    }

}
