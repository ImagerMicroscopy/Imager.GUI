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
        EnsureCanHaveChildren(parent);
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

    /// <summary>
    /// Moves node under newParent so that it ends up at newIndex among newParent's children
    /// (0..Count, where Count excludes the node itself when it already lives there).
    /// </summary>
    public void MoveNode(MeasurementElementViewModel node, MeasurementElementViewModel newParent, int newIndex)
    {
        if (node == newParent)
            throw new InvalidOperationException("Cannot move a node to be its own parent.");

        if (IsAncestorOf(node, newParent))
            throw new InvalidOperationException("Cannot move a node into one of its own descendants.");

        EnsureCanHaveChildren(newParent);

        // Validate against the list as it will be after removal, so a failed move never
        // leaves the node detached from the tree.
        int countAfterRemoval = newParent.Children.Count - (node.Parent == newParent ? 1 : 0);
        if (newIndex < 0 || newIndex > countAfterRemoval)
            throw new ArgumentOutOfRangeException(nameof(newIndex), "New index is out of range.");

        node.Parent?.Children.Remove(node);
        newParent.Children.Insert(newIndex, node);
        node.Parent = newParent;
    }

    private static bool IsAncestorOf(MeasurementElementViewModel ancestor, MeasurementElementViewModel node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current == ancestor)
                return true;
        }
        return false;
    }

    private static void EnsureCanHaveChildren(MeasurementElementViewModel parent)
    {
        if (parent is not RootNode && !parent.ToModel().CanHaveChildren())
            throw new InvalidOperationException($"'{parent.Header}' cannot contain other elements.");
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