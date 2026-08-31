using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class MultiChannelConfigChannelViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedDetection))]
        private MeasurementElementViewModel? _selectedDetection;
        public bool HasSelectedDetection => SelectedDetection is not null;
        private MeasurementElementViewModel? _selectedNode;
        public Guid? SelectedDetectionId { get; set; } = Guid.Empty;
        [ObservableProperty] DetectorEquipmentViewModel _detectorInput;
        [ObservableProperty] EnabledAcquisition _AcquisitionInput;

        [ObservableProperty] ObservableCollection<DetectorEquipmentViewModel> _DefinedDetectors = new();
        [ObservableProperty] ObservableCollection<EnabledAcquisition> _definedAcquisitions = new();
        [ObservableProperty] ObservableCollection<EnabledAcquisition> _filteredAcquisitions = new();



        public MultiChannelConfigChannelViewModel() { }


        public MeasurementElementViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (value is MeasurementElementViewModel detectionNode)
                {

                    if (detectionNode is DetectionElementViewModel acq_vm)
                    {
                        SelectedDetectionId = acq_vm.Elementid;
                        DefinedAcquisitions = acq_vm.IsAquisitionEnabled;
                        acq_vm.IsAquisitionEnabled.CollectionChanged += IsAquisitionEnabled_CollectionChanged;
                        acq_vm.AvailableAcquisitions.CollectionChanged += AvailableAcquisitions_CollectionChanged;
                        foreach (var enabled_acq in acq_vm.IsAquisitionEnabled)
                        {
                            enabled_acq.PropertyChanged += IsEnabled_PropertyChanged;
                        }
                        RefreshFilteredAcquisitions();

                        _selectedNode = detectionNode;
                        SelectedDetection = detectionNode;
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

        internal void NodeDeleted(object? sender, MeasurementElementViewModel? e)
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
                    SelectedDetectionId = null;
                    SelectedNode = null;
                    RefreshFilteredAcquisitions();
                }
            }
        }

        partial void OnAcquisitionInputChanged(EnabledAcquisition acq_input)
        {
            if (AcquisitionInput != null)
            {
                DefinedDetectors = acq_input.Acquisition.Detector;
            }
        }

        private void RefreshFilteredAcquisitions()
        {

            FilteredAcquisitions = new ObservableCollection<EnabledAcquisition>(
                DefinedAcquisitions.Where(a => a.IsEnabled));

        }
    }

    
}

