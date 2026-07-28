// ===================== AcquisitionSettingsViewModel.cs =====================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels;

/// <summary>
/// Broadcast whenever an AcquisitionSettingsViewModel's Name changes.
/// Anything that displays or keys off an acquisition's name can subscribe
/// to this instead of the owning VM having to manually forward PropertyChanged.
/// </summary>
public sealed class AcquisitionNameChangedMessage
{
    public AcquisitionSettingsViewModel Acquisition { get; }
    public string NewName { get; }

    public AcquisitionNameChangedMessage(AcquisitionSettingsViewModel acquisition, string newName)
    {
        Acquisition = acquisition;
        NewName = newName;
    }
}

public partial class AcquisitionSettingsViewModel : ObservableValidator, IDisposable
{
    public DefinedDetection DetectionSettings;
    private readonly ImagerWorkspace _imagerWorkspace;
    private readonly ExperimentManager _experimentManager;

    // ---------------- Sources ----------------
    private ObservableCollection<SourcesEquipmentViewModel> _sources;
    public ObservableCollection<SourcesEquipmentViewModel> Sources
    {
        get
        {
            if (_sources == null && DetectionSettings?.Settings.Irradiation != null)
            {
                _sources = new ObservableCollection<SourcesEquipmentViewModel>(
                    DetectionSettings.Settings.Irradiation.Select(x => new SourcesEquipmentViewModel(x)));
                _sources.CollectionChanged += Sources_CollectionChanged;
            }
            return _sources;
        }
        set => SetCollection(ref _sources, value, Sources_CollectionChanged, nameof(Sources));
    }

    private void Sources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetectionSettings.Settings.Irradiation = _sources.Select(x => x.LightSource).ToList();
    }

    // ---------------- FilterWheels ----------------
    private ObservableCollection<MovableEquipmentViewModel> _filterWheels;
    public ObservableCollection<MovableEquipmentViewModel> FilterWheels
    {
        get
        {
            if (_filterWheels == null && DetectionSettings?.Settings.MovableComponents != null)
            {
                _filterWheels = new ObservableCollection<MovableEquipmentViewModel>(
                    DetectionSettings.Settings.MovableComponents.Select(x => new MovableEquipmentViewModel(x)));
                _filterWheels.CollectionChanged += Filter_CollectionChanged;
            }
            return _filterWheels;
        }
        set => SetCollection(ref _filterWheels, value, Filter_CollectionChanged, nameof(FilterWheels));
    }

    private void Filter_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetectionSettings.Settings.MovableComponents = _filterWheels.Select(x => x.MovableEquipmentProperties).ToList();
    }

    // ---------------- Detector ----------------
    private ObservableCollection<DetectorEquipmentViewModel> _detector;
    public ObservableCollection<DetectorEquipmentViewModel> Detector
    {
        get
        {
            if (_detector == null && DetectionSettings?.Settings.Detectors != null)
            {
                _detector = new ObservableCollection<DetectorEquipmentViewModel>(
                    DetectionSettings.Settings.Detectors.Select(x => new DetectorEquipmentViewModel(x, _imagerWorkspace,_experimentManager)));
                //WireDetectorCollection(_detector);
            }
            return _detector;
        }
        set
        {
            if (value == _detector) return;
        }
    }


    private void SetCollection<T>(
        ref ObservableCollection<T> field,
        ObservableCollection<T> value,
        NotifyCollectionChangedEventHandler handler,
        string propertyName)
    {
        if (value == field) return;

        if (field != null)
            field.CollectionChanged -= handler;

        field = value;

        if (field != null)
            field.CollectionChanged += handler;

        OnPropertyChanged(propertyName);
    }

    // ---------------- Name ----------------
    [ObservableProperty]
    private string _name;

    public int AcquisitionID;

    partial void OnNameChanged(string? oldValue, string newValue)
    {
        DetectionSettings.Name = newValue;
        WeakReferenceMessenger.Default.Send(new AcquisitionNameChangedMessage(this, newValue));
    }

    public List<Tuple<string, string>> AcqDetPairs = new();
    public List<Stage> Stages;

    public AcquisitionSettingsViewModel(string name, List<string> detector_names)
    {
        Name = name;
    }

    public AcquisitionSettingsViewModel(string name, List<Source> sources,
        List<MovableComponentModel> filterWheels, List<DetectorEquipmentModel> detectors, ImagerWorkspace imagerWorkspace, ExperimentManager experimentManager)
    {
        _imagerWorkspace = imagerWorkspace;
        _experimentManager = experimentManager;
        DetectionSettings = DetectionSettingsFactory.FromComponents(name, sources, filterWheels, detectors);

        // Touch the lazy getters instead of duplicating the wiring logic here.
        var wiredFilterWheels = FilterWheels;
        var wiredDetectors = Detector;
        Name = name;
    }

    public AcquisitionSettingsViewModel(DefinedDetection acquisition, ImagerWorkspace imagerWorkspace, ExperimentManager experimentManager)
        : this(acquisition.Name, acquisition.Settings.Irradiation, acquisition.Settings.MovableComponents, acquisition.Settings.Detectors, imagerWorkspace,
              experimentManager)
    {
    }

    public JObject SerializeAcquisition() => null;

    public DefinedDetection GetAcquisitionSettingsFromViewModel() => DetectionSettings;

    public List<Tuple<string,string>> GetAcquisitionDetectorPairs()
    {
        var pairs = new List<Tuple<string, string>>();
        foreach (var det in Detector)
        {
            if (det.IsEnabled)
            {
                pairs.Add(new Tuple<string, string>(Name, det.Name));
            }
        }
        return pairs;
    }

    public void Dispose()
    {
        if (_sources != null)
            _sources.CollectionChanged -= Sources_CollectionChanged;

        if (_filterWheels != null)
            _filterWheels.CollectionChanged -= Filter_CollectionChanged;
    }
}