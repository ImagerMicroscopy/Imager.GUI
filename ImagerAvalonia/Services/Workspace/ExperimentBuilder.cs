using System;
using System.Collections.Generic;
using System.Linq;
using ImagerAvalonia.Data;
using ImagerAvalonia.Data.Measurements;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Central manager for experiment configuration.
/// Owns both the UI tree structure (NodeBase) and the canonical state (MeasurementElement tree).
/// All GUI interactions should send commands to this class, which updates state and notifies the UI.
/// </summary>
public class ExperimentBuilder : IDisposable
{
    // ========================================================================
    // State (Canonical - Source of Truth)
    // ========================================================================
    
    private MeasurementElement? _programRoot;
    private readonly List<DefinedDetection> _definedDetections = new();
    private readonly SmartProgramCodePayload _smartProgramCode = new();
    
    // ========================================================================
    // UI Structure (Parallel tree for display)
    // ========================================================================
    
    private RootNode? _uiRootNode;
    private readonly Dictionary<Guid, NodeBase> _elementIdToNodeMap = new();
    private readonly Dictionary<Guid, MeasurementElement> _elementIdToStateMap = new();
    
    // ========================================================================
    // Dependencies
    // ========================================================================
    
    private readonly SystemDefinedSettingsViewModel _acquisitionSettings;
    private readonly IStageControl _stageControl;
    private readonly INodeFactory _nodeFactory;
    
    // ========================================================================
    // Events
    // ========================================================================
    
    /// <summary>Fired when the experiment configuration changes (add/remove/update nodes).</summary>
    public event EventHandler? ConfigurationChanged;
    
    /// <summary>Fired when a node is added to the tree.</summary>
    public event Action<NodeBase>? NodeAdded;
    
    /// <summary>Fired when a node is removed from the tree.</summary>
    public event Action<NodeBase>? NodeRemoved;
    
    /// <summary>Fired when a node's properties are updated.</summary>
    public event Action<NodeBase>? NodeUpdated;
    
    /// <summary>Fired when the root node changes.</summary>
    public event Action<NodeBase?>? RootNodeChanged;
    
    // ========================================================================
    // Constructor
    // ========================================================================
    
    public ExperimentBuilder(
        SystemDefinedSettingsViewModel acquisitionSettings,
        IStageControl stageControl,
        INodeFactory nodeFactory)
    {
        _acquisitionSettings = acquisitionSettings;
        _stageControl = stageControl;
        _nodeFactory = nodeFactory;
        
        // Create the root node
        _uiRootNode = new RootNode();
        _uiRootNode.UserAcquisitionSettings = acquisitionSettings;
        
        // Register the root node
        RegisterNode(_uiRootNode);
    }
    
    // ========================================================================
    // Public Properties
    // ========================================================================
    
    /// <summary>Gets the root node of the UI tree.</summary>
    public NodeBase? UiRootNode => _uiRootNode;
    
    /// <summary>Gets the root of the MeasurementElement tree (canonical state).</summary>
    public MeasurementElement? ProgramRoot => _programRoot;
    
    /// <summary>Gets the list of defined detections.</summary>
    public IReadOnlyList<DefinedDetection> DefinedDetections => _definedDetections.AsReadOnly();
    
    // ========================================================================
    // Node Management Commands (Called by GUI)
    // ========================================================================
    
    /// <summary>
    /// Creates and adds a new child node under the specified parent.
    /// </summary>
    public NodeBase AddNode(string elementType, NodeBase parent, int? insertIndex = null)
    {
        // Create the new node via factory
        var newNode = _nodeFactory.CreateChildNodeOfType(elementType, _acquisitionSettings, parent);
        
        // Add to parent's children
        if (insertIndex.HasValue && insertIndex.Value >= 0 && insertIndex.Value <= parent.Children.Count)
        {
            parent.Children.Insert(insertIndex.Value, newNode);
        }
        else
        {
            parent.Children.Add(newNode);
        }
        
        // Set up the ViewModel with ElementId and reference to this ExperimentBuilder
        if (newNode.NodeViewModel is MeasurementViewModel mv)
        {
            if (mv.Elementid == Guid.Empty)
            {
                mv.Elementid = Guid.NewGuid();
            }
            // Set the reference to this ExperimentBuilder so ViewModel can delegate commands
            mv.ExperimentBuilder = this;
        }
        
        // Register the node
        RegisterNode(newNode);
        
        // Create corresponding MeasurementElement in state tree
        CreateStateElement(newNode);
        
        // Fire events
        NodeAdded?.Invoke(newNode);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        
        return newNode;
    }
    
    /// <summary>
    /// Removes a node from the tree.
    /// </summary>
    public void RemoveNode(NodeBase node)
    {
        if (node.Parent == null && node != _uiRootNode)
            throw new InvalidOperationException("Cannot remove a node without a parent.");
        
        // Remove from parent
        if (node.Parent != null)
        {
            node.Parent.Children.Remove(node);
        }
        
        // Unregister the node
        UnregisterNode(node);
        
        // Remove corresponding state element
        RemoveStateElement(node);
        
        // Fire events
        NodeRemoved?.Invoke(node);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        
        // Dispose the node
        node.Dispose();
    }
    
    /// <summary>
    /// Moves a node to a new parent and/or position.
    /// </summary>
    public void MoveNode(NodeBase node, NodeBase newParent, int newIndex)
    {
        if (node == newParent)
            throw new InvalidOperationException("Cannot move a node to be its own parent.");
        
        if (newIndex < 0 || newIndex > newParent.Children.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex), "New index is out of range.");
        
        // Remove from old parent
        if (node.Parent != null)
        {
            node.Parent.Children.Remove(node);
        }
        
        // Add to new parent
        newParent.Children.Insert(newIndex, node);
        node.Parent = newParent;
        
        // Update state tree structure
        UpdateStateElementParent(node, newParent);
        
        // Fire events
        NodeUpdated?.Invoke(node);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    // ========================================================================
    // Property Update Commands (Called by ViewModels)
    // ========================================================================
    
    /// <summary>Updates the number of iterations for a DoTimes element.</summary>
    public void UpdateDoTimesIterations(Guid elementId, int count)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEDoTimes doTimes)
        {
            doTimes.NumIterationsTotal = count;
            // Don't call UpdateViewModelFromState here to avoid feedback loop
            // ViewModel already has the new value
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates the time lapse parameters.</summary>
    public void UpdateTimeLapse(Guid elementId, int numIterations, double waitDurationSeconds)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is METimeLapse timeLapse)
        {
            timeLapse.NumIterationsTotal = numIterations;
            timeLapse.WaitDurationInSeconds = waitDurationSeconds;
            // Don't call UpdateViewModelFromState here to avoid feedback loop
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates the wait duration.</summary>
    public void UpdateWaitDuration(Guid elementId, double durationSeconds)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEWait wait)
        {
            wait.DurationInSeconds = durationSeconds;
            // Don't call UpdateViewModelFromState here to avoid feedback loop
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates the stage name for a stage loop.</summary>
    public void UpdateStageLoopStageName(Guid elementId, string stageName)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element))
        {
            if (element is MEStageLoop stageLoop)
            {
                stageLoop.StageName = stageName;
                // Don't sync back to ViewModel to avoid feedback loop
            
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (element is MERelativeStageLoop relStageLoop)
            {
                relStageLoop.StageName = stageName;
                // Don't sync back to ViewModel to avoid feedback loop
            
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    /// <summary>Adds a position to a stage loop.</summary>
    public void AddStagePosition(Guid elementId, XYStagePosition position)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEStageLoop stageLoop)
        {
            stageLoop.Positions.Add(new PositionNameAndCoords
            {
                Name = position.Name,
                Coordinates = new StagePosition
                {
                    X = (double)position.XPos,
                    Y = (double)position.YPos,
                    Z = (double)position.ZPos
                }
            });
            
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Updates all positions for a stage loop (replaces existing positions).</summary>
    public void UpdateStagePositions(Guid elementId, List<XYStagePosition> positions)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEStageLoop stageLoop)
        {
            stageLoop.Positions = positions.Select(p => new PositionNameAndCoords
            {
                Name = p.Name,
                Coordinates = new StagePosition
                {
                    X = (double)p.XPos,
                    Y = (double)p.YPos,
                    Z = (double)p.ZPos
                }
            }).ToList();
            
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Removes a position from a stage loop.</summary>
    public void RemoveStagePosition(Guid elementId, string positionName)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEStageLoop stageLoop)
        {
            stageLoop.Positions.RemoveAll(p => p.Name == positionName);
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates relative stage loop parameters.</summary>
    public void UpdateRelativeStageLoopParams(
        Guid elementId,
        double deltaX, double deltaY, double deltaZ,
        int tileNegX, int tilePosX,
        int tileNegY, int tilePosY,
        int tileNegZ, int tilePosZ,
        bool returnToStart)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MERelativeStageLoop relStageLoop)
        {
            relStageLoop.Params.DeltaX = deltaX;
            relStageLoop.Params.DeltaY = deltaY;
            relStageLoop.Params.DeltaZ = deltaZ;
            relStageLoop.Params.AdditionalPlanesX = (tileNegX, tilePosX);
            relStageLoop.Params.AdditionalPlanesY = (tileNegY, tilePosY);
            relStageLoop.Params.AdditionalPlanesZ = (tileNegZ, tilePosZ);
            relStageLoop.Params.ReturnToStartingPosition = returnToStart;
            
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates irradiation parameters.</summary>
    public void UpdateIrradiation(
        Guid elementId,
        double durationSeconds,
        List<IrradiationParams> irradiations)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEIrradiation irradiation)
        {
            irradiation.DurationInSeconds = durationSeconds;
            irradiation.Irradiation = irradiations;
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates robot program parameters.</summary>
    public void UpdateRobotProgram(
        Guid elementId,
        string equipmentName,
        string robotName,
        string programName,
        List<RobotProgramArgument> arguments)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEExecuteRobotProgram robot)
        {
            robot.ProgramParameters.EquipmentName = equipmentName;
            robot.ProgramParameters.RobotName = robotName;
            robot.ProgramParameters.ProgramCallParameters.ProgramName = programName;
            robot.ProgramParameters.ProgramCallParameters.Arguments = arguments;
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates update acquisition parameters.</summary>
    public void UpdateUpdateAcquisition(
        Guid elementId,
        string? detectionName,
        string? smartProgramId)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEUpdateAcquisition update)
        {
            update.DetectionName = detectionName;
            update.SmartProgramId = smartProgramId;
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>Updates detection parameters.</summary>
    public void UpdateDetection(
        Guid elementId,
        List<string> detectionNames,
        List<string> smartProgramIds)
    {
        if (_elementIdToStateMap.TryGetValue(elementId, out var element) && element is MEDetection detection)
        {
            detection.DetectionNames = detectionNames;
            detection.SmartProgramIds = smartProgramIds;
            // Don't sync back to ViewModel to avoid feedback loop
            
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    // ========================================================================
    // Detection Management
    // ========================================================================
    
    public void AddDetection(DefinedDetection detection)
    {
        _definedDetections.Add(detection);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public void RemoveDetection(string detectionName)
    {
        _definedDetections.RemoveAll(d => d.Name == detectionName);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public void UpdateDetection(string detectionName, DetectionParams settings)
    {
        var detection = _definedDetections.FirstOrDefault(d => d.Name == detectionName);
        if (detection != null)
        {
            detection.Settings = settings;
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    // ========================================================================
    // Smart Program Management
    // ========================================================================
    
    public void SetSmartProgramCode(List<object> programs)
    {
        _smartProgramCode.Programs = programs;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    // ========================================================================
    // Build Methods
    // ========================================================================
    
    /// <summary>
    /// Compiles the current state into a MeasurementProgramPayload for execution.
    /// Uses the canonical MeasurementElement tree maintained by this manager.
    /// </summary>
    public MeasurementProgramPayload BuildMeasurementProgram()
    {
        if (_programRoot == null)
            throw new InvalidOperationException("No program defined.");
        
        return new MeasurementProgramPayload
        {
            Action = "executemeasurementprogram",
            DefinedDetections = _definedDetections.ToDetectionParamsDictionary(),
            Program = _programRoot,
            SmartProgramCode = _smartProgramCode
        };
    }
    
    /// <summary>
    /// Builds just the MeasurementElement tree (for backwards compatibility).
    /// </summary>
    public MeasurementElement BuildMeasurementElement()
    {
        if (_programRoot == null)
            throw new InvalidOperationException("No program defined.");
        
        return _programRoot;
    }
    
    /// <summary>Gets the list of defined detections.</summary>
    public List<DefinedDetection> GetDefinedDetections() => new(_definedDetections);
    
    /// <summary>Gets the smart program code.</summary>
    public List<object> GetSmartProgramCode() => new(_smartProgramCode.Programs);
    
    // ========================================================================
    // State Synchronization
    // ========================================================================
    
    /// <summary>
    /// Synchronizes the entire UI tree with the state tree.
    /// Call this after loading from file or when external changes occur.
    /// </summary>
    public void SyncUiFromState()
    {
        if (_uiRootNode == null || _programRoot == null)
            return;
        
        SyncNodeFromElement(_uiRootNode, _programRoot);
    }
    
    private void SyncNodeFromElement(NodeBase node, MeasurementElement element)
    {
        // Update the ViewModel from the element
        if (node.NodeViewModel != null)
        {
            MeasurementElementConverters.UpdateViewModelFromState(node.NodeViewModel, element);
        }
        
        // Sync children recursively
        if (element.IsContainerElement() && node is ExperimentNode expNode)
        {
            var containerElement = element as dynamic;
            var elements = containerElement.Elements as List<MeasurementElement>;
            
            if (elements != null)
            {
                for (int i = 0; i < Math.Min(node.Children.Count, elements.Count); i++)
                {
                    SyncNodeFromElement(node.Children[i], elements[i]);
                }
            }
        }
    }
    
    /// <summary>
    /// Updates a ViewModel from its corresponding MeasurementElement.
    /// </summary>
    private void UpdateViewModelFromState(Guid elementId, MeasurementElement element)
    {
        if (_elementIdToNodeMap.TryGetValue(elementId, out var node) && node.NodeViewModel != null)
        {
            MeasurementElementConverters.UpdateViewModelFromState(node.NodeViewModel, element);
        }
    }
    
    // ========================================================================
    // Internal Helper Methods
    // ========================================================================
    
    private void RegisterNode(NodeBase node)
    {
        if (node.NodeViewModel is MeasurementViewModel mv && mv.Elementid != Guid.Empty)
        {
            _elementIdToNodeMap[mv.Elementid] = node;
        }
        
        // Register children recursively
        foreach (var child in node.Children)
        {
            RegisterNode(child);
        }
    }
    
    private void UnregisterNode(NodeBase node)
    {
        if (node.NodeViewModel is MeasurementViewModel mv)
        {
            _elementIdToNodeMap.Remove(mv.Elementid);
            _elementIdToStateMap.Remove(mv.Elementid);
        }
        
        // Unregister children recursively
        foreach (var child in node.Children)
        {
            UnregisterNode(child);
        }
    }
    
    private void CreateStateElement(NodeBase node)
    {
        if (node.NodeViewModel == null) return;
        
        MeasurementElement? stateElement = null;
        
        // Try to get the element ID
        Guid elementId = (node.NodeViewModel as MeasurementViewModel)?.Elementid ?? Guid.NewGuid();
        
        // Create the appropriate element based on ViewModel type
        switch (node.NodeViewModel)
        {
            case DoTimesViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case TimeLapseViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case WaitViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case StageLoopViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case RelStageViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case IrradiationPanelViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case RobotControlViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case UpdateAcquisitionViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case AcquisitionPanelViewModel vm:
                stateElement = vm.ToMeasurementElement();
                break;
            case RootPanelViewModel:
                // Root doesn't have a direct MeasurementElement representation
                // It's handled specially in the tree
                break;
        }
        
        if (stateElement != null)
        {
            stateElement.ElementId = elementId;
            _elementIdToStateMap[elementId] = stateElement;
            
            // If this is the root's first child, set it as program root
            if (node.Parent == _uiRootNode && _programRoot == null)
            {
                _programRoot = stateElement;
            }
            
            // Handle container elements - create state for children
            if (stateElement.IsContainerElement() && node is ExperimentNode expNode)
            {
                var container = stateElement as dynamic;
                var elementsList = new List<MeasurementElement>();
                container.Elements = elementsList;
                
                foreach (var child in expNode.Children)
                {
                    CreateStateElement(child);
                    // Add to parent's elements list
                    if (_elementIdToStateMap.TryGetValue((child.NodeViewModel as MeasurementViewModel)?.Elementid ?? Guid.Empty, out var childElement))
                    {
                        elementsList.Add(childElement);
                    }
                }
            }
        }
        
        // Process children recursively
        foreach (var child in node.Children)
        {
            CreateStateElement(child);
        }
    }
    
    private void RemoveStateElement(NodeBase node)
    {
        if (node.NodeViewModel is MeasurementViewModel mv)
        {
            _elementIdToStateMap.Remove(mv.Elementid);
            
            // If this was the program root, clear it
            if (_programRoot?.ElementId == mv.Elementid)
            {
                _programRoot = null;
            }
        }
        
        // Remove children recursively
        foreach (var child in node.Children)
        {
            RemoveStateElement(child);
        }
    }
    
    private void UpdateStateElementParent(NodeBase node, NodeBase newParent)
    {
        if (node.NodeViewModel is not MeasurementViewModel mv || !_elementIdToStateMap.TryGetValue(mv.Elementid, out var element))
            return;
        
        // If element is a container, update its children
        if (element.IsContainerElement() && node is ExperimentNode expNode)
        {
            var container = element as dynamic;
            if (container.Elements != null)
            {
                container.Elements.Clear();
                foreach (var child in expNode.Children)
                {
                    if (_elementIdToStateMap.TryGetValue((child.NodeViewModel as MeasurementViewModel)?.Elementid ?? Guid.Empty, out var childElement))
                    {
                        container.Elements.Add(childElement);
                    }
                }
            }
        }
        
        // If new parent is root and program root is null, set this as program root
        if (newParent == _uiRootNode && _programRoot == null)
        {
            _programRoot = element;
        }
        
        // Process children recursively
        foreach (var child in node.Children)
        {
            UpdateStateElementParent(child, newParent);
        }
    }
    
    // ========================================================================
    // IDisposable
    // ========================================================================
    
    public void Dispose()
    {
        _elementIdToNodeMap.Clear();
        _elementIdToStateMap.Clear();
        
        if (_uiRootNode != null)
        {
            _uiRootNode.Dispose();
            _uiRootNode = null;
        }
        
        _programRoot = null;
        _definedDetections.Clear();
    }
}
