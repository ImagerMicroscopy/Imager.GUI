using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Data.Measurements;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels;

public partial class ExperimentalPanelViewModel : ViewModelBase {
    public List<int[]> AcqDetDetectionsList = new();
    public List<AcqDetPair> AcqDetPairs = new();

    public ObservableCollection<NodeBase> Items { get; set; }
    public SystemDefinedSettingsViewModel AcquisitionSettings;
    [ObservableProperty] public string? _ExperimentName;
    [ObservableProperty] public ViewModelBase _ContentPane;
    [ObservableProperty] public NodeBase _SelectedTreeItem;
    [ObservableProperty] public RootNode _Root;
    [ObservableProperty] public bool _AreSourcesAvailable = true;
    [ObservableProperty] public bool _AreStagesAvailable = true;

    public List<EnabledAcquisition> EnabledAcquisitions = new();
    private readonly INodeFactory _nodeFactory = new NodeFactory();
    private readonly ExperimentBuilder _experimentBuilder;
    public readonly EnabledAcquisitionTracker AcquisitionTracker = new();

    public ExperimentalPanelViewModel(
        SystemDefinedSettingsViewModel user_acq,
        IStageControl stageControl) {
        // Create the ExperimentBuilder that will manage the experiment state
        _experimentBuilder = new ExperimentBuilder(user_acq, stageControl, _nodeFactory);
        
        // Get the root node from ExperimentBuilder
        if (_experimentBuilder.UiRootNode is RootNode rootNode) {
            Root = rootNode;
        } else {
            Root = new RootNode();
        }

        if (user_acq.Acquisitions.Any() && user_acq.Acquisitions.First().Sources.Count == 0) {
            AreSourcesAvailable = false;
        }

        if (stageControl.StageName == null) {
            AreStagesAvailable = false;
        }

        AcquisitionSettings = user_acq;
        Root.UserAcquisitionSettings = AcquisitionSettings;
        
        // Get items from ExperimentBuilder's UI tree
        Items = new ObservableCollection<NodeBase> { Root };
        SelectedTreeItem = Root;
        ContentPane = Root.NodeViewModel;
        
        // Subscribe to ExperimentBuilder events
        _experimentBuilder.NodeAdded += OnNodeAdded;
        _experimentBuilder.NodeRemoved += OnNodeRemoved;
        _experimentBuilder.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) {
        // Refresh UI as needed
    }

    private void OnNodeAdded(NodeBase node) {
        // Node is already added to parent's Children by ExperimentBuilder
        // Just ensure our Items collection is in sync if needed
        if (!Items.Contains(node) && node.Parent == Root) {
            // This shouldn't happen as Items contains Root which has Children
        }
    }

    private void OnNodeRemoved(NodeBase node) {
        // Node is already removed from parent's Children by ExperimentBuilder
        // No additional action needed for Items since it contains Root
    }

    public void SetExpNodes(ObservableCollection<NodeBase> nodes) {
        Items = nodes;
        if (Items.Count > 0 && Items[0] is RootNode root) {
            Root = root;
            // Update ExperimentBuilder to use this loaded tree
            _experimentBuilder.SetUiRootNode(root);
        }
    }

    public void SetAcquisitions(ObservableCollection<AcquisitionSettingsViewModel> acquisitions) {
        foreach (var acq in acquisitions) {
            EnabledAcquisitions.Add(new EnabledAcquisition(true, acq));
        }
        AcquisitionTracker.EnabledAcquisitions.AddRange(EnabledAcquisitions);
    }

    public string GetStoragePath() {
        if (this.Items[0].NodeViewModel is RootPanelViewModel root_vm) {
            RootPanelViewModel main_storage_directory = root_vm;
            return System.IO.Path.Combine(
                main_storage_directory.GetOutputFolder(),
                $"{main_storage_directory.GetUniqueFileName()}.tif");
        } else {
            throw new Exception("Could not get storage path");
        }
    }

    public void DeleteNode() {
        if (SelectedTreeItem != null) {
            // Use ExperimentBuilder to remove the node
            _experimentBuilder.RemoveNode(SelectedTreeItem);
        }
    }

    public void AddNode(string? elementName) {
        // Use ExperimentBuilder to add a new node
        var elementType = elementName ?? string.Empty;
        NodeBase node = _experimentBuilder.AddNode(elementType, Root);
        
        if (node.NodeViewModel is AcquisitionPanelViewModel acquisition_vm) {
            acquisition_vm.SetAcquisitionTracker(AcquisitionTracker);
        }
    }

    /// <summary>Gets the current experiment as a MeasurementProgramPayload for execution.</summary>
    public MeasurementProgramPayload BuildMeasurementProgram() {
        return _experimentBuilder.BuildMeasurementProgram();
    }

    public override void Dispose() {
        _experimentBuilder.NodeAdded -= OnNodeAdded;
        _experimentBuilder.NodeRemoved -= OnNodeRemoved;
        _experimentBuilder.ConfigurationChanged -= OnConfigurationChanged;
        _experimentBuilder.Dispose();
        Root.Dispose();
    }
}
