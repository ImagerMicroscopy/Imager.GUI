using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels;
using System;

namespace ImagerAvalonia.ViewModels.MeasurementViewModels;

/// <summary>
/// Per-experiment builder. Handles node operations on the ViewModel tree and
/// building/serializing to MeasurementElementBase. One instance per experiment —
/// do not share across ExperimentalPanelViewModel instances.
/// </summary>
public class ExperimentBuilder : IDisposable
{
    private readonly IMeasurementElementViewModelFactory _elementFactory;

    /// <summary>Result of the most recent BuildMeasurementProgram call.</summary>
    public MeasurementElementBase? MeasurementElement { get; private set; }

    public ExperimentStorageService StorageService { get; }

    public ExperimentBuilder(IMeasurementElementViewModelFactory elementFactory, ExperimentStorageService? storageService = null)
    {
        _elementFactory = elementFactory;
        StorageService = storageService ?? new ExperimentStorageService();
    }

    public MeasurementElementViewModel AddNode(ExperimentElementType elementType, MeasurementElementViewModel parent)
    {
        var newNode = _elementFactory.Create(elementType);
        newNode.Parent = parent;
        parent.Children.Add(newNode);
        return newNode;
    }

    public void RemoveNode(MeasurementElementViewModel node)
    {
        node.Parent?.Children.Remove(node);
        node.Dispose();
    }

    public void MoveNode(MeasurementElementViewModel node, MeasurementElementViewModel newParent, int newIndex)
    {
        if (node == newParent)
            throw new InvalidOperationException("Cannot move a node to be its own parent.");

        if (newIndex < 0 || newIndex > newParent.Children.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex), "New index is out of range.");

        node.Parent?.Children.Remove(node);
        newParent.Children.Insert(newIndex, node);
        node.Parent = newParent;
    }

    /// <summary>
    /// Builds the MeasurementElement tree from the given root. Root is required —
    /// there is no ambiguous "current" root implied elsewhere.
    /// </summary>
    public MeasurementElementBase BuildMeasurementProgram(RootNode root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        MeasurementElement = root.Traverse();
        return MeasurementElement;
    }

    public void Dispose()
    {
        MeasurementElement = null;
    }
}