using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels;


// ---------- Abstract Base Node ----------

public abstract partial class MeasurementElementViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] string _DisplayedInfo = string.Empty;
    [ObservableProperty] Guid _elementid = Guid.NewGuid();
    [ObservableProperty] ObservableCollection<SmartProgramViewModel> _smartPrograms = new();
    [ObservableProperty] SmartProgramViewModel? _selectedProgramId = null;
    [ObservableProperty] bool _fromProgramId = false;
    [ObservableProperty] ObservableCollection<SmartProgramInput?> _smartProgramBindings = new();
    [ObservableProperty] ObservableCollection<MeasurementElementViewModel> _children = new();
    [ObservableProperty] string _Header = string.Empty;
    
    public event EventHandler<MeasurementElementViewModel?>?  OnNodeDeleted; 
    public MeasurementElementViewModel? Parent { get; set; }

    /// <summary>
    /// Converts the ViewModel state to a MeasurementElementBase model.
    /// Each concrete implementation populates its model based on observable properties.
    /// </summary>
    public abstract MeasurementElementBase ToModel();

    /// <summary>
    /// Convenience property exposing the current view model as a MeasurementElementBase model.
    /// Note: this generates the model from the view-model state by calling ToModel().
    /// </summary>
    public MeasurementElementBase MeasurementElement => ToModel();

    public virtual void Dispose()
    {
        OnNodeDeleted?.Invoke(null, this);
        foreach (var child in Children){
            child.Dispose();
        }
    }

    public MeasurementElementViewModel()
    {
        SmartPrograms = App.Container.Resolve<SmartProcessingRegisterViewModel>().SmartProgramViewModels;
    }

    public MeasurementElementBase Traverse()
    {
        var element = ToModel();
        foreach (var child_vm in Children)
        {
            element.Elements.Add(child_vm.Traverse());
        }
        return element;
    }

    /// <summary>
    /// Calculates the total number of detections across the entire measurement tree,
    /// accounting for DoTimes repetitions that multiply the detection count of descendant nodes.
    /// </summary>
    public int GetTotalDetectionCount() => GetDetectionCountWithMultiplier(multiplier: 1);

    /// <summary>
    /// Recursively calculates detection count with a cumulative multiplier from parent DoTimes nodes.
    /// </summary>
    private int GetDetectionCountWithMultiplier(int multiplier)
    {
        // Count this node if it's a detection
        int count = (this is DetectionElementViewModel ? 1 : 0) * multiplier;

        // If this is a DoTimes, multiply the children by NumRepeats
        int childMultiplier = multiplier;
        if (this is DoTimesViewModel doTimes)
        {
            childMultiplier *= doTimes.NumRepeats;
        }

        // Recursively count children with the appropriate multiplier
        foreach (var child in Children)
        {
            count += child.GetDetectionCountWithMultiplier(childMultiplier);
        }

        return count;
    }

    public virtual void LoadFromModel(MeasurementElementBase model, LoadContext context)
    {
        if (Guid.TryParse(model.ElementId, out var id))
            Elementid = id;
    }

}




public partial class RootNode : MeasurementElementViewModel
{
    [ObservableProperty] bool _canBeDeleted = false;

    public GlobalDefinedSettingsViewModel? UserAcquisitionSettings { get; set; }

    // Storage properties - stored via StorageService reference
    private ExperimentStorageService? _storageService;

    public ExperimentStorageService? StorageService 
    { 
        get => _storageService;
        set => _storageService = value;
    }

    public string OutputFolder
    {
        get => _storageService?.OutputFolder ?? string.Empty;
        set { if (_storageService != null) _storageService.OutputFolder = value; }
    }

    public string FileName
    {
        get => _storageService?.FileName ?? string.Empty;
        set { if (_storageService != null) _storageService.FileName = value; }
    }

    public bool IsExperimentStorageEnabled
    {
        get => _storageService?.IsExperimentStorageEnabled ?? true;
        set { if (_storageService != null) _storageService.IsExperimentStorageEnabled = value; }
    }

    public void SelectOutputFolder() => _storageService?.SelectOutputFolder();

    public override MeasurementElementBase ToModel()
    {
        return new DoTimesElement() { NTotal = 1, ElementId = Elementid.ToString() };
    }


    public RootNode()
    {
        Header = "Root";
    }
}

