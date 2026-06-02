using System;
using System.Collections.Generic;
using ImagerAvalonia.Data;
using ImagerAvalonia.Data.Measurements;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Responsible for maintaining the "Draft" state of the experiment the user is currently building in the UI.
/// UI components bound to experiment configuration interact directly with this.
/// </summary>
public class ExperimentBuilder
{
    // These properties reflect what the user sees in the UI Tree/Lists
    private List<DefinedDetection> _draftDetections = new();
    private MeasurementElement? _draftProgramRoot;

    // Events to notify the UI if the configuration is changed programmatically
    public event EventHandler? ConfigurationChanged;

    public void AddDetection(DefinedDetection detection)
    {
        _draftDetections.Add(detection);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetProgramRoot(MeasurementElement rootElement)
    {
        _draftProgramRoot = rootElement;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Compiles the UI draft into the final C# AST needed by the engine.
    /// </summary>
    public MeasurementElement BuildMeasurementElement()
    {
        if (_draftProgramRoot == null)
            throw new InvalidOperationException("No program defined.");

        return _draftProgramRoot; // Might involve cloning or validating in a real scenario
    }

    public List<DefinedDetection> GetDefinedDetections() => new(_draftDetections);

    public List<object> GetSmartProgramCode() => new(); // Extend as needed
}
