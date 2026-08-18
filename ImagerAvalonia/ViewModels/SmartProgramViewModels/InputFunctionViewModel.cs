using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;

namespace ImagerAvalonia.ViewModels
{
    public partial class InputFunctionViewModel : ViewModelBase
    {
        [ObservableProperty] string _methodName;
        public Guid ProgramID;
        [ObservableProperty] ObservableCollection<InputParameterViewModel> _methodParams;

        [ObservableProperty] InputFunctionModel _model;

        public InputFunctionViewModel(ImportedInputFunctionModel inputFunction, Guid programID)
        {
            MethodName = inputFunction.method_name;
            ProgramID = programID;

            Model = new InputFunctionModel { methodname = MethodName };

            MethodParams = new ObservableCollection<InputParameterViewModel>(inputFunction.method_params.Select(x =>
            new InputParameterViewModel(x, this)));


            foreach (var param in MethodParams)
            {
                Model.inputparams.Add(param.ParamModel);
            }
        }

        internal void ClearBinding()
        {
            foreach(var param in MethodParams)
            {
                param.RemoveExperimentBindings();
            }
        }
    }

    public partial class InputParameterViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedDetection))]
        private MeasurementElementViewModel? _selectedDetection;
        private MeasurementElementViewModel? _selectedNode;
        private InputFunctionViewModel _inputFunction;
        public Guid SmartProgramID { get; set; }
        public bool HasSelectedDetection => SelectedDetection is not null;
        public string InputParameterName;
        [ObservableProperty] SmartProgramInput? _smartProgramBinding;
        [ObservableProperty] DetectorEquipmentViewModel _detectorInput;
        [ObservableProperty] EnabledAcquisition _acquisitionInput;

        [ObservableProperty] ObservableCollection<DetectorEquipmentViewModel> _definedDetectors = new();

        [ObservableProperty] ObservableCollection<EnabledAcquisition> _definedAcquisitions = new();
        [ObservableProperty] ObservableCollection<EnabledAcquisition> _filteredAcquisitions = new();

        // Serializable state for this single parameter (acquisition / detection / elementid).
        [ObservableProperty] InputParameterModel _paramModel = new();

        public InputParameterViewModel(string input_parameter_name, InputFunctionViewModel vm)
        {
            InputParameterName = input_parameter_name;
            _inputFunction = vm;
            SmartProgramID = vm.ProgramID;
        }

        public MeasurementElementViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (value is MeasurementElementViewModel detectionNode)
                {
                    if (_selectedNode is not null)
                    {
                        if (_selectedNode.SmartProgramBindings.Contains(SmartProgramBinding))
                        {
                            _selectedNode.SmartProgramBindings.Remove(SmartProgramBinding);
                        }
                    }

                    if (detectionNode is DetectionElementViewModel acq_vm)
                    {
                        DefinedAcquisitions = acq_vm.IsAquisitionEnabled;
                        acq_vm.IsAquisitionEnabled.CollectionChanged += IsAquisitionEnabled_CollectionChanged;
                        acq_vm.AvailableAcquisitions.CollectionChanged += AvailableAcquisitions_CollectionChanged;
                       

                        foreach (var enabled_acq in acq_vm.IsAquisitionEnabled)
                        {
                            enabled_acq.PropertyChanged += IsEnabled_PropertyChanged;
                        }
                        RefreshFilteredAcquisitions();
                        if (SmartProgramBinding != null)
                        {
                            if (detectionNode.SmartProgramBindings.Contains(SmartProgramBinding))
                            {
                                detectionNode.SmartProgramBindings.Remove(SmartProgramBinding);
                            }
                        }

                        _selectedNode = detectionNode;
                        SmartProgramBinding<InputFunctionViewModel> smBinding = new SmartProgramBinding<InputFunctionViewModel>(_inputFunction);
                        SmartProgramBinding = smBinding;
                        SmartProgramBinding.SmartProgramID = SmartProgramID;

                        if (detectionNode is DetectionElementViewModel acq)
                        {
                            acq.SmartProgramBindings.Add(smBinding);
                            ParamModel.elementid = detectionNode.Elementid;
                            acq.OnNodeDeleted += ReferenceDetectionDeleted;
                        }

                    }
                }
            }
        }

        private void ReferenceDetectionDeleted(object? sender, MeasurementElementViewModel? e)
        {
            SelectedDetection = null;
            DefinedAcquisitions.Clear();
            FilteredAcquisitions.Clear();

            AcquisitionInput = null;
            DetectorInput = null;


            ParamModel.Clear();

        }

        private void AvailableAcquisitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

            foreach (var enabled_acq in DefinedAcquisitions)
            {
                enabled_acq.PropertyChanged += IsEnabled_PropertyChanged;
            }

            RefreshFilteredAcquisitions();

        }

        private void IsEnabled_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsEnabled")
            {
                RefreshFilteredAcquisitions();
            }
        }

        private void IsAquisitionEnabled_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshFilteredAcquisitions();
        }

        internal void NodeDeleted(object? sender, MeasurementElementViewModel e)
        {
            if (e is MeasurementElementViewModel node)
            {
                if (SelectedDetection == node)
                {
                    DefinedAcquisitions = new ObservableCollection<EnabledAcquisition>();
                    AcquisitionInput = null;
                    DefinedDetectors = new ObservableCollection<DetectorEquipmentViewModel>();
                    DetectorInput = null;
                    SelectedDetection = null;
                    SmartProgramBinding = null;
                    SelectedNode = null;
                    RefreshFilteredAcquisitions();

                    ParamModel.Clear();
                }
            }
        }


        partial void OnDetectorInputChanged(DetectorEquipmentViewModel acq_input)
        {
            if (acq_input is not null)
            {
                ParamModel.detection = acq_input.Name;
            }
            else
            {
                ParamModel.detection = null;
            }
        }

        partial void OnAcquisitionInputChanged(EnabledAcquisition acq_input)
        {
            if (DefinedDetectors.Count != 0)
            {
                foreach(var detector in DefinedDetectors)
                {
                    detector.WhenDetectorEnabled -= Detector_WhenDetectorEnabled;
                }
            }

            if (AcquisitionInput != null)
            {
                DefinedDetectors = acq_input.Acquisition.Detector;
                ParamModel.acquisition = acq_input.Acquisition.Name;
            }

            if (DefinedDetectors.Count != 0)
            {
                foreach (var detector in DefinedDetectors)
                {
                    detector.WhenDetectorEnabled += Detector_WhenDetectorEnabled;
                }
            }
        }

        private void Detector_WhenDetectorEnabled(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(sender is DetectorEquipmentViewModel detector &&
                detector == DetectorInput)
            {
                DetectorInput = null;
            }
        }

        private void RefreshFilteredAcquisitions()
        {
            AcquisitionInput = null;
            FilteredAcquisitions = new ObservableCollection<EnabledAcquisition>(
                DefinedAcquisitions.Where(a => a.IsEnabled));

        }

        internal void RemoveExperimentBindings()
        {
            if (SmartProgramBinding != null && SelectedNode != null)
            {
                SelectedNode.SmartProgramBindings.Remove(SmartProgramBinding);

                SmartProgramBinding = null;
            }
        }
    }
}