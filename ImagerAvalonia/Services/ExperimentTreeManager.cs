using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services;

#pragma warning disable CS8602 // Dereference of a possibly null reference

// ---------- Abstract Base Node ----------

public abstract partial class NodeBase : ObservableObject, IDisposable
{
    public abstract JObject Traverse(IExperimentSerialization experimentSerialization);
    public abstract IMeasurementTypes? MeasurementType { get; set; }
    public event EventHandler<ViewModelBase?>? OnNodeDeleted;
    public MeasurementViewModel NodeViewModel { get; protected set; }
    public NodeBase? Parent { get; set; }
    public string? Header { get; set; }
    [ObservableProperty] private string? _visibleInfo;
    public ObservableCollection<NodeBase> Children { get; set; } = new();
    public SystemDefinedSettingsViewModel? UserAcquisitionSettings { get; set; }

    public ObservableCollection<AcquisitionSettingsViewModel> AvailableAcquisitions =>
        UserAcquisitionSettings.Acquisitions;

    public abstract void DeleteExperiment(MenuItem? sender, bool removefromparent);

    protected void RaiseNodeDeleted()
    {
        OnNodeDeleted?.Invoke(this, NodeViewModel);
    }


    public virtual void Dispose()
    {
        NodeViewModel?.Dispose();
        foreach (var child in Children)
        {
            child.Dispose();
        }
    }

    public void TraverseMeasurement(TraversalState state)
    {
        if (Children.Count > 0)
        {
            foreach (var child in Children)
                child.MeasurementType.DetectionWiseTraversal(child, state);
        }
        else
        {
            MeasurementType.DetectionWiseTraversal(this, state);
        }
    }
}

// ---------- Root Node ----------

public class RootNode : NodeBase
{
    public override IMeasurementTypes? MeasurementType { get; set; }

    public RootNode()
    {
        Header = "Root";
        NodeViewModel = new RootPanelViewModel();
    }


    public void AppendNextItemToNode(NodeBase node)
    {
        Children.Add(node);
    }

    public bool IsExperimentStorageEnabled { get
        {
            var root = NodeViewModel as RootPanelViewModel;
            return root.IsStorageEnabled;
        }
        
    }

    public override JObject Traverse(IExperimentSerialization experimentSerialization)
    {
        if (Children.Count == 0)
        {
            throw new ExperimentTraversalException("Element contains no children", "Root");
        }

        var childParams = new JArray();
        foreach (var child in Children)
        {
            childParams.Add(child.Traverse(experimentSerialization));
        }

        return new JObject
        {
            ["elements"] = childParams,
            ["elementtype"] = "dotimes",
            ["ntotal"] = 1,
            ["elementid"] = "root",
            ["smartprogramid"] = null

        };
    }

    public override void DeleteExperiment(MenuItem? sender, bool removefromparent) { /* Root can't be deleted */ }
}

// ---------- Action Node ----------

public partial class ActionNode : NodeBase
{
    public override IMeasurementTypes? MeasurementType { get; set; }
    [ObservableProperty] ObservableCollection<SmartProgramInput?> _smartProgramBindings = new();

    public ActionNode(NodeBase parent, IMeasurementTypes measurement)
    {
        Parent = parent;
        UserAcquisitionSettings = parent.UserAcquisitionSettings;
        MeasurementType = measurement;
        Header = measurement.MeasurementName;
        if (measurement.MeasurementView.DataContext is MeasurementViewModel measurementVM)
        {
            NodeViewModel = measurementVM;
            NodeViewModel.PropertyChanged += NodeViewModel_PropertyChanged;
        }
        VisibleInfo = NodeViewModel.DisplayedInfo;

    }

    private void NodeViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.DisplayedInfo))
        {
            VisibleInfo = NodeViewModel.DisplayedInfo;
        }
    }

    public override JObject Traverse(IExperimentSerialization experimentSerialization) =>
        MeasurementType.Serialize(experimentSerialization);

    public override void DeleteExperiment(MenuItem? sender, bool removefromparent)
    {
        RaiseNodeDeleted();
        NodeViewModel?.Dispose();
        if (removefromparent)
        {
            Parent?.Children?.Remove(this);
        }
    }


}

// ---------- Experiment Node ----------

public class ExperimentNode : NodeBase
{
    public override IMeasurementTypes? MeasurementType { get; set; }

    public ExperimentNode(NodeBase parent, IMeasurementTypes measurement)
    {
        Parent = parent;
        UserAcquisitionSettings = parent.UserAcquisitionSettings;
        MeasurementType = measurement;
        Header = measurement.MeasurementName;
        if (measurement.MeasurementView.DataContext is MeasurementViewModel measurementVM)
        {
            NodeViewModel = measurementVM;
            NodeViewModel.PropertyChanged += NodeViewModel_PropertyChanged;
        }
        VisibleInfo = NodeViewModel.DisplayedInfo;

    }

    private void NodeViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.DisplayedInfo))
        {
            VisibleInfo = NodeViewModel.DisplayedInfo;
        }
    }

    public override JObject Traverse(IExperimentSerialization experimentSerialization)
    {
        if (Children.Count == 0)
            throw new ExperimentTraversalException("Element contains no children", MeasurementType.MeasurementName);

        var childParams = new JArray();
        foreach (var child in Children)
        {
            childParams.Add(child.Traverse(experimentSerialization));
        }

        var serialized = MeasurementType.Serialize(experimentSerialization);
        serialized["elements"] = childParams;
        return serialized;
    }

    public override void DeleteExperiment(MenuItem? sender, bool removefromparent)
    {
        for (int childi = 0; childi < Children.Count; childi++)
        {
            Children[childi].DeleteExperiment(sender, false);
        }
        NodeViewModel?.Dispose();
        if (removefromparent)
        {
            Parent?.Children?.Remove(this);
        }
    }
}

// ---------- Node Factory ----------

public interface INodeFactory
{
    NodeBase CreateChildNodeOfType(string measurementName, SystemDefinedSettingsViewModel acquisitions, NodeBase parent);
}

public class NodeFactory : INodeFactory
{
    public NodeBase CreateChildNodeOfType(string measurementName, SystemDefinedSettingsViewModel acquisitions, NodeBase parent)
    {
        string? currentNamespace = typeof(IMeasurementTypes).Namespace;
        
        Type type = Type.GetType($"{currentNamespace}.{measurementName}")
                     ?? throw new ArgumentException($"Measurement type '{measurementName}' not found in namespace '{currentNamespace}'.");

        var measurementType = Activator.CreateInstance(type, acquisitions) as IMeasurementTypes;

        return measurementType.NodeType switch
        {
            TreeMeasurementType.ActionType => new ActionNode(parent, measurementType),
            TreeMeasurementType.ExperimentType => new ExperimentNode(parent, measurementType),
            _ => throw new NotImplementedException($"Unsupported node type: {measurementType.NodeType}")
        };
    }
}

// ---------- Traversal Exception ----------

public class ExperimentTraversalException : Exception
{
    private readonly string _elementName;
    private readonly string _errorMessage;

    public override string Message =>
        $"An exception occurred when serializing element '{_elementName}': {_errorMessage}";

    public ExperimentTraversalException(string errorMessage, string elementName)
    {
        _elementName = elementName;
        _errorMessage = errorMessage;
    }
}

public partial class SmartProgramBinding<T> : SmartProgramInput
{
    [ObservableProperty] Avalonia.Media.SolidColorBrush _smartProgramColor;
    [ObservableProperty] T _smartProgramInputVM;
    private static readonly Random _random = new Random();

    public SmartProgramBinding(T smartProgramInputVM)
    {
        byte r = (byte)_random.Next(256);
        byte g = (byte)_random.Next(256);
        byte b = (byte)_random.Next(256);

        base.SmartProgramColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, b));
        SmartProgramInputVM = smartProgramInputVM;  
        
    }
}

public partial class SmartProgramInput : ObservableObject 
{
    [ObservableProperty] Guid _smartProgramID;
    [ObservableProperty] Avalonia.Media.SolidColorBrush _smartProgramColor;
    private static readonly Random _random = new Random();

}