using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImagerAvalonia.Data;
using ImagerAvalonia.Data.Measurements;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Represents the overall state of the application.
/// </summary>
public enum WorkspaceState {
    Idle,           // User is configuring experiments or viewing idle data
    Acquiring,      // Hardware is actively running a measurement
    Processing,     // Data is being processed (post-measurement or smart-algo)
    Error           // Something has faulted and requires user intervention
}

/// <summary>
/// The central hub of the application. It owns the specialized managers and handles the 
/// high-level state. ViewModels will bind to this (or interact with it) to trigger application-wide changes.
/// </summary>
public class ImagerWorkspace {
    // The three specialized sub-managers
    public ExperimentBuilder ExperimentBuilder { get; }
    public AcquisitionEngine AcquisitionEngine { get; }
    public DataWorkspace DataWorkspace { get; }

    // State exposing
    public WorkspaceState CurrentState { get; private set; } = WorkspaceState.Idle;
    public event EventHandler<WorkspaceState>? StateChanged;

    public ImagerWorkspace(
        ExperimentBuilder experimentBuilder, 
        AcquisitionEngine acquisitionEngine, 
        DataWorkspace dataWorkspace) {
        ExperimentBuilder = experimentBuilder;
        AcquisitionEngine = acquisitionEngine;
        DataWorkspace = dataWorkspace;

        // Subscribe to engine events to update Workspace state and delegate data
        AcquisitionEngine.MeasurementStarted += (s, e) => SetState(WorkspaceState.Acquiring);
        AcquisitionEngine.MeasurementCompleted += (s, e) => SetState(WorkspaceState.Idle);
        AcquisitionEngine.ImageReceived += (s, img) => DataWorkspace.AddImage(img);
    }

    /// <summary>
    /// Called by the UI (e.g. MainViewModel) when the user clicks "Start Experiment".
    /// </summary>
    public async Task StartExperimentAsync(CancellationToken cancellationToken = default) {
        if (CurrentState != WorkspaceState.Idle) 
            throw new InvalidOperationException("Workspace is not idle.");

        // 1. Pull the complete measurement program payload from the builder
        var measurementProgram = ExperimentBuilder.BuildMeasurementProgram();

        // 2. Prepare the data workspace for a new run (clear old visuals, etc.)
        DataWorkspace.ClearWorkspace();

        // 3. Delegate the actual hardware execution to the engine
        // Convert from MeasurementProgramPayload format to what AcquisitionEngine expects
        var detections = measurementProgram.DefinedDetections.ToDefinedDetectionList();
        var smartCode = measurementProgram.SmartProgramCode.Programs;
        
        await AcquisitionEngine.RunMeasurementAsync(
            measurementProgram.Program,
            detections,
            smartCode,
            cancellationToken);
    }

    /// <summary>
    /// Called by the UI to load historical data from disk.
    /// </summary>
    public async Task LoadHistoricalDataAsync(string directoryPath) {
        if (CurrentState == WorkspaceState.Acquiring)
            throw new InvalidOperationException("Cannot load data while acquiring.");

        DataWorkspace.ClearWorkspace();
        // Load data from disk and populate DataWorkspace...
    }

    private void SetState(WorkspaceState newState) {
        CurrentState = newState;
        StateChanged?.Invoke(this, newState);
    }
}
