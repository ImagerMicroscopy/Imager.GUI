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
    /// Depth-first search of this node and its descendants for the node with
    /// the given Elementid. Used to re-resolve a saved elementid (e.g. from
    /// InputParameterModel.elementid) back to a live tree node after a
    /// project load / smart program import, so bindings can be re-attached
    /// via the normal SelectedNode setter rather than reconstructed by hand.
    /// </summary>
    public MeasurementElementViewModel? FindByElementId(Guid elementId)
    {
        if (Elementid == elementId)
            return this;

        foreach (var child in Children)
        {
            var found = child.FindByElementId(elementId);
            if (found is not null)
                return found;
        }

        return null;
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

    /// <summary>
    /// Smart program this node was saved as bound to, kept until a matching
    /// SmartProgramViewModel actually exists. A project load builds the tree
    /// before it restores the smart programs (ExperimentManager.ParseLoadedExperiment),
    /// so the lookup at LoadFromModel time normally finds nothing - without
    /// remembering the id here, the saved binding would be silently dropped and
    /// the loop node would come back unbound.
    /// </summary>
    public Guid? PendingSmartProgramId { get; private set; }

    /// <summary>
    /// Restores this node's smart program binding from the saved GUID, deferring
    /// it if that program hasn't been registered yet. Call from LoadFromModel in
    /// place of a direct SmartPrograms lookup.
    /// </summary>
    protected void LoadSmartProgramBinding(string? smartProgramId)
    {
        PendingSmartProgramId = Guid.TryParse(smartProgramId, out var parsed) ? parsed : null;
        ResolveSmartProgramBinding();
    }

    /// <summary>
    /// Resolves this node and all its descendants against the smart programs
    /// registered so far. Safe to call repeatedly; a node stays pending until its
    /// program shows up, and is left alone once resolved.
    /// </summary>
    public void ResolveSmartProgramBindings()
    {
        ResolveSmartProgramBinding();
        foreach (var child in Children)
        {
            child.ResolveSmartProgramBindings();
        }
    }

    /// <summary>Element ids of this subtree's nodes still waiting on a smart program that never arrived.</summary>
    public IEnumerable<(Guid ElementId, Guid SmartProgramId)> GetUnresolvedSmartProgramBindings()
    {
        if (PendingSmartProgramId is Guid pending)
            yield return (Elementid, pending);

        foreach (var child in Children)
        {
            foreach (var unresolved in child.GetUnresolvedSmartProgramBindings())
                yield return unresolved;
        }
    }

    private void ResolveSmartProgramBinding()
    {
        if (PendingSmartProgramId is not Guid pending)
            return;

        var program = SmartPrograms.FirstOrDefault(p => p.SmartProgramID == pending);
        if (program is null)
            return;

        SelectedProgramId = program;
        PendingSmartProgramId = null;
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

