
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ImagerAvalonia.ViewModels;






public partial class AcquisitionSettingsViewModel : ObservableValidator, IDisposable
{

    public AcquisitionSettings AcquisitionSettings;


    private ObservableCollection<SourcesViewModel> _sources;
    public ObservableCollection<SourcesViewModel> Sources
    {
        get
        {
            if (_sources == null && AcquisitionSettings?.Sources != null)
            {
                _sources = new ObservableCollection<SourcesViewModel>(AcquisitionSettings.Sources.Select(x=> new SourcesViewModel(x)));
                _sources.CollectionChanged += Sources_CollectionChanged;
            }
            return _sources;
        }
        set
        {
            if (value != _sources)
            {
                _sources = value;
                //_acquisitionSettings.Sources = value.Select(x => x.LightSource).ToList(); 
                OnPropertyChanged(nameof(Sources));
            }
        }
    }

    private void Sources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AcquisitionSettings.Sources = _sources.Select(x =>x.LightSource).ToList(); 
    }



    public ObservableCollection<MovableEquipmentViewModel> _filterWheels;
    public ObservableCollection<MovableEquipmentViewModel> FilterWheels
    {
        get
        {
            if (_filterWheels == null && AcquisitionSettings?.FilterWheels != null)
            {
                _filterWheels = new ObservableCollection<MovableEquipmentViewModel>(AcquisitionSettings.FilterWheels.Select(x => new MovableEquipmentViewModel(x)));
                _filterWheels.CollectionChanged += Filter_CollectionChanged;
            }
            return _filterWheels;
        }
        set
        {
            if (value != _filterWheels)
            {
                _filterWheels = value;
                //_acquisitionSettings.FilterWheels = value.Select(x => x.DetectorEquipmentProperties).ToList();
                OnPropertyChanged(nameof(FilterWheels));
            }
        }
    }


    private ObservableCollection<DetectorEquipmentViewModel> _detector;
    public ObservableCollection<DetectorEquipmentViewModel> Detector 
    {
        get
        {
            if (_detector == null && AcquisitionSettings?.Detector != null)
            {
                _detector = new ObservableCollection<DetectorEquipmentViewModel>(AcquisitionSettings.Detector.Select(x => new DetectorEquipmentViewModel(x)));
                _detector.CollectionChanged += Detector_CollectionChanged;
                foreach(var det in _detector)
                {
                    det.WhenDetectorEnabled += UpdateAcqDetPairs;
                    det.IsEnabled = true;
                }
            }
            return _detector;
        }
        set
        {
            if (value != _detector)
            {
                _detector = value;
                AcquisitionSettings.Detector = value.Select(x => x.DetectorEquipmentProperties).ToList(); 
                OnPropertyChanged(nameof(Detector));
            }
        }
    }

    private void Detector_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AcquisitionSettings.Detector = _detector.Select(x => x.DetectorEquipmentProperties).ToList();
    }

    private void Filter_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AcquisitionSettings.FilterWheels =
            _filterWheels.Select(x => x.MovableEquipmentProperties).ToList();
    }

    public event EventHandler<string>? AcqNameChanged;


    [ObservableProperty]
    private string _name;

    public int AcquisitionID;

    partial void OnNameChanged(string? oldValue, string newValue)
    {
        AcquisitionSettings.Name = newValue;    
        AcqNameChanged?.Invoke(this, newValue);
    }

    public List<AcqDetPair> AcqDetPairs = new();

    public List<Stage> Stages;

    public AcquisitionSettingsViewModel(string name, List<string> detector_names)
    {

        AcquisitionSettings = AcquisitionSettingsFactory.FromDetectorNames(name, detector_names);
        Name = name;

    }

    public AcquisitionSettingsViewModel(string name, List<Source> sources,
    List<MovableComponent> filterWheels, List<DetectorEquipment> detectors)
    {



        AcquisitionSettings = AcquisitionSettingsFactory.FromComponents(name, sources, filterWheels, detectors);
        FilterWheels = new ObservableCollection<MovableEquipmentViewModel>(AcquisitionSettings.FilterWheels.Select(x => new MovableEquipmentViewModel(x)));
        Detector = new ObservableCollection<DetectorEquipmentViewModel>(AcquisitionSettings.Detector.Select(x => new DetectorEquipmentViewModel(x)));
        foreach (var item in Detector)
        {
            item.WhenDetectorEnabled += UpdateAcqDetPairs;
            var pair = new AcqDetPair(this.AcquisitionSettings, item.Name);
            if (item.IsEnabled && !AcquisitionSettings.acqDetPairs.Contains(pair))
            {
                AcquisitionSettings.acqDetPairs.Add(pair);
            }
        }
        AcqDetPairs = AcquisitionSettings.acqDetPairs;
        Name = name;




    }

    public AcquisitionSettingsViewModel(AcquisitionSettings acquisition): this(acquisition.Name, acquisition.Sources, acquisition.FilterWheels, acquisition.Detector)
    {

    }

    public JObject SerializeAcquisition() => AcquisitionSettings.SerializeAcquisition();

    public AcquisitionSettings GetAcquisitionSettingsFromViewModel() =>  AcquisitionSettings;

    private void UpdateAcqDetPairs(object? sender, PropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(sender);
        if (e.PropertyName == nameof(DetectorEquipmentViewModel.IsEnabled))
        {
            if (sender is DetectorEquipmentViewModel det)
            {
                if (det.IsEnabled)
                {
                    AcqDetPairs.Add(new AcqDetPair(this.AcquisitionSettings, det.Name));
                }
                else
                {
                    AcqDetPairs.Remove(new AcqDetPair(this.AcquisitionSettings, det.Name));
                }
                AcquisitionSettings.acqDetPairs = AcqDetPairs;
            }
        }
    }

    public void Dispose()
    {
        if (_sources != null) _sources.CollectionChanged -= Sources_CollectionChanged;
        if (_filterWheels != null) _filterWheels.CollectionChanged -= Filter_CollectionChanged;
        if (_detector != null)
        {
            _detector.CollectionChanged -= Detector_CollectionChanged;
            foreach (var det in _detector)
            {
                det.WhenDetectorEnabled -= UpdateAcqDetPairs;
            }
        }
    }
}



