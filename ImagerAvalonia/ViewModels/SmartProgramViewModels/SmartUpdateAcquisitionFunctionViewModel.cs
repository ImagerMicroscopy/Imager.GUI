using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.MeasurementControl;
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
        [ObservableProperty] Guid _elementID = Guid.Empty;
        [ObservableProperty] string _acquisitionUpdate;
        [ObservableProperty] SmartProgramInput? _smartProgramBinding;
        [ObservableProperty] ObservableCollection<UpdateAcquisitionFunctionParameterViewModel> _acquisitionUpdateParameters = new();

        // Source of truth for serialization - mirrors InputFunctionViewModel.Model.
        [ObservableProperty] SmartAcquisitionUpdateModel _model = new();

        public SmartProgramViewModel SmartProgramViewModel { get; set; }

        public Guid SmartProgramID { get; set; }
        private MeasurementElementViewModel? _selectedNode;
        public MeasurementElementViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (value is null)
                {
                    _selectedNode = null;
                }

                if (value is MeasurementElementViewModel detectionNode)
                {
                    if (_selectedNode is not null)
                    {
                        ClearUpdateAcquisitionNodeBindings(_selectedNode);
                    }
                    _selectedNode = detectionNode;

                    SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel> smBinding =
                        new SmartProgramBinding<SmartUpdateAcquisitionFunctionViewModel>(this);
                    SmartProgramBinding = smBinding;
                    SmartProgramBinding.SmartProgramID = SmartProgramViewModel.SmartProgramID;
                    ElementID = detectionNode.Elementid; // syncs into Model via OnElementIDChanged
                    detectionNode.SmartProgramBindings = new ObservableCollection<SmartProgramInput> { smBinding };
                }

                OnPropertyChanged(nameof(SelectedNode));
                OnPropertyChanged(nameof(HasSelectedNode));
            }
        }

        public bool HasSelectedNode => SelectedNode is not null;

        public EquipmentState EquipmentState { get; set; }
        public ImportedInputFunctionModel? UpdateParameters
        {
            get;
            set
            {
                AcquisitionUpdateParameters.Clear();
                Model.acquisitionupdateparameters.Clear();
                foreach (var param in value.method_params)
                {
                    var paramVM = new UpdateAcquisitionFunctionParameterViewModel(param, EquipmentState);
                    AcquisitionUpdateParameters.Add(paramVM);
                    Model.acquisitionupdateparameters.Add(paramVM.Model);
                }
            }
        }

        public SmartUpdateAcquisitionFunctionViewModel()
        {

        }

        // Keep Model in step with the bindable properties, same role as
        // InputFunctionViewModel setting Model.methodname from MethodName.
        partial void OnElementIDChanged(Guid value)
        {
            Model.elementid = value;
        }

        partial void OnAcquisitionUpdateChanged(string value)
        {
            Model.acquisitionupdatefunction = value ?? string.Empty;
        }

        private void ClearUpdateAcquisitionNodeBindings(MeasurementElementViewModel node)
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
                SelectedNode = null; // <-- added so HasSelectedNode flips back to false
            }
        }

        public void DraggedNode_OnNodeDeleted(object? sender, MeasurementElementViewModel? e)
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
            writer.WriteValue(value.Model.elementid);
            writer.WritePropertyName("acquisitionupdatefunction");
            writer.WriteValue(value.Model.acquisitionupdatefunction);
            writer.WritePropertyName("acquisitionupdateparameters");
            serializer.Serialize(writer, value.Model.acquisitionupdateparameters);
            writer.WriteEndObject(); // was missing before - left the JSON object unclosed
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
        [JsonIgnoreAttribute] public List<EquipmentProperty> EquipmentProperties;

        // Serializable state for this single parameter - mirrors InputParameterViewModel.ParamModel.
        public UpdateAcquisitionParameterModel Model { get; } = new();

        public UpdateAcquisitionFunctionParameterViewModel(string parameterName, EquipmentState equipmentState)
        {
            EquipmentProperties = equipmentState.EquipmentProperties;
            EquipmentPaths = new ObservableCollection<string>(
                equipmentState.EquipmentProperties.Select(x => string.Join('/', x.EquipmentPath)));
            SelectedEquipmentPaths = equipmentState.EquipmentProperties.Select(x => x.EquipmentPath).ToList();

            Model.SelectedEquipmentPaths = SelectedEquipmentPaths;
            Model.EquipmentPaths = EquipmentPaths.ToList();

            ParameterName = parameterName; // triggers OnParameterNameChanged -> Model.ParameterName
        }

        partial void OnParameterNameChanged(string value)
        {
            Model.ParameterName = value ?? string.Empty;
        }

        partial void OnSelectedPathChanged(string value)
        {
            Model.SelectedPath = value ?? string.Empty;
        }

        partial void OnSelectedPathIndexChanged(int value)
        {
            if (EquipmentProperties is null || value < 0 || value >= EquipmentProperties.Count)
            {
                return;
            }
            PropertyType = EquipmentProperties[value].EquipmentType;
            Model.PropertyType = PropertyType;
            Model.SelectedPathIndex = value;
            SelectedPath = EquipmentPaths[value]; // triggers OnSelectedPathChanged too
        }

    }


}