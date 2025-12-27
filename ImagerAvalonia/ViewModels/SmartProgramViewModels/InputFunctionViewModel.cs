using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.ViewModels
{
    public partial class InputFunctionViewModel :ViewModelBase
    {
        [ObservableProperty] string _methodName;
        public Guid ProgramID;
        [ObservableProperty] ObservableCollection<InputParameterViewModel> _methodParams;

        public InputFunctionViewModel(InputFunction inputFunction, Guid programID)
        {
            MethodName = inputFunction.method_name;
            ProgramID = programID;
            MethodParams = new ObservableCollection<InputParameterViewModel>(inputFunction.method_params.Select(x =>
            new InputParameterViewModel(x, this)));

        }
    }

    public partial class InputParameterViewModel : ViewModelBase
    {
        public MeasurementViewModel? SelectedDetection { get; set; }
        private ActionNode? _selectedNode;
        private InputFunctionViewModel _inputFunction;
        public Guid SmartProgramID { get; set; }
        public string InputParameterName;
        [ObservableProperty] SmartProgramInput? _smartProgramBinding;
        [ObservableProperty] DetectorEquipmentViewModel _detectorInput;
        [ObservableProperty] EnabledAcquisition _AcquisitionInput;

        [ObservableProperty] ObservableCollection<DetectorEquipmentViewModel> _DefinedDetectors = new();
        [ObservableProperty] ObservableCollection<EnabledAcquisition> _definedAcquisitions = new();
        [ObservableProperty] ObservableCollection<EnabledAcquisition> _filteredAcquisitions = new();


        public InputParameterViewModel(string input_parameter_name, InputFunctionViewModel vm)
        {
            InputParameterName = input_parameter_name;  
            _inputFunction = vm;
            SmartProgramID = vm.ProgramID;
        }

        public ActionNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (value is ActionNode detectionNode)
                {
                    if (_selectedNode is not null)
                    {
                        if (_selectedNode.SmartProgramBindings.Contains(SmartProgramBinding))
                        {
                            _selectedNode.SmartProgramBindings.Remove(SmartProgramBinding);
                            if (_selectedNode.MeasurementType is Detection acq)
                            {

                                acq.SmartProgramIDS.Remove(SmartProgramID);
                            }
                        }
                    }

                    if (detectionNode.NodeViewModel is AcquisitionPanelViewModel acq_vm)
                    {
                        DefinedAcquisitions = acq_vm.IsAquisitionEnabled;
                        acq_vm.IsAquisitionEnabled.CollectionChanged += IsAquisitionEnabled_CollectionChanged;
                        acq_vm.AvailableAcquisitions.CollectionChanged += AvailableAcquisitions_CollectionChanged;
                        foreach (var enabled_acq in acq_vm.IsAquisitionEnabled)
                        {
                            enabled_acq.PropertyChanged += IsEnabled_PropertyChanged;
                        }
                        RefreshFilteredAcquisitions();
                        if(SmartProgramBinding!=null)
                        {
                            if(detectionNode.SmartProgramBindings.Contains(SmartProgramBinding))
                            {
                                detectionNode.SmartProgramBindings.Remove(SmartProgramBinding);
                            }
                        }

                        _selectedNode = detectionNode;
                        SmartProgramBinding<InputFunctionViewModel> smBinding = new SmartProgramBinding<InputFunctionViewModel>(_inputFunction);
                        SmartProgramBinding = smBinding;
                        SmartProgramBinding.SmartProgramID = SmartProgramID;

                        if (detectionNode.MeasurementType is Detection acq)
                        {

                            detectionNode.SmartProgramBindings.Add(smBinding);
                            acq.SmartProgramIDS.Add(SmartProgramID);
                        }
                    }
                }
            }
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

        internal void NodeDeleted(object? sender, ViewModelBase e)
        {
            if (e is MeasurementViewModel node)
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
                }
            }
        }

        partial void OnAcquisitionInputChanged(EnabledAcquisition acq_input)
        {
            if (AcquisitionInput != null)
            {
                DefinedDetectors = acq_input.acquisition.Detector;
            }
        }

        private void RefreshFilteredAcquisitions()
        {

            FilteredAcquisitions = new ObservableCollection<EnabledAcquisition>(
                DefinedAcquisitions.Where(a => a.IsEnabled));

        }

        internal void RemoveExperimentBindings()
        {
            if (SmartProgramBinding != null && SelectedNode != null)
            {
                SelectedNode.SmartProgramBindings.Remove(SmartProgramBinding);
                if (SelectedNode.MeasurementType is Detection acq)
                {
                    acq.SmartProgramIDS.Remove(SmartProgramID);
                }
                SmartProgramBinding = null;
            }
        }
    }
}
