using System;
using System.Collections.Generic;

using ImagerAvalonia.Services.MeasurementControl;
namespace ImagerAvalonia.Data.Measurements;

public abstract class MeasurementElement
{
    // Automatically populated on instantiation, mapping to ElementID
    public Guid ElementId { get; set; } = Guid.NewGuid();
}

// ---------------------------------------------------------
// Core Measurement Elements
// ---------------------------------------------------------

public class MEDetection : MeasurementElement
{
    public List<string> DetectionNames { get; set; } = new();
    public List<string> SmartProgramIds { get; set; } = new();
}

public class MEIrradiation : MeasurementElement
{
    public double DurationInSeconds { get; set; }
    public List<IrradiationParams> Irradiation { get; set; } = new();
}

public class MEWait : MeasurementElement
{
    public double DurationInSeconds { get; set; }
}

public class MEExecuteRobotProgram : MeasurementElement
{
    public RobotProgramExecutionParams ProgramParameters { get; set; } = new();
}

public class MEDoTimes : MeasurementElement
{
    public int NumIterationsTotal { get; set; }
    public string? SmartProgramId { get; set; }
    public List<MeasurementElement> Elements { get; set; } = new();
}

public class METimeLapse : MeasurementElement
{
    public int NumIterationsTotal { get; set; }
    public double WaitDurationInSeconds { get; set; }
    public string? SmartProgramId { get; set; }
    public List<MeasurementElement> Elements { get; set; } = new();
}

public class MEStageLoop : MeasurementElement
{
    public string StageName { get; set; } = string.Empty;
    public List<PositionNameAndCoords> Positions { get; set; } = new();
    public string? SmartProgramId { get; set; }
    public List<MeasurementElement> Elements { get; set; } = new();
}

public class MERelativeStageLoop : MeasurementElement
{
    public string StageName { get; set; } = string.Empty;
    public RelativeStageLoopParams Params { get; set; } = new();
    public string? SmartProgramId { get; set; }
    public List<MeasurementElement> Elements { get; set; } = new();
}

public class MEUpdateAcquisition : MeasurementElement
{
    public string? SmartProgramId { get; set; }
    public string? DetectionName { get; set; }
}

// ---------------------------------------------------------
// Supplementary Options and Parameters
// ---------------------------------------------------------

public class IrradiationParams
{
    public string EquipmentName { get; set; } = string.Empty;
    public string LightSourceName { get; set; } = string.Empty;
    public List<string> LightSourceChannels { get; set; } = new();
    public List<double> Powers { get; set; } = new();
}

// Robot parameters
public class RobotProgramExecutionParams
{
    public string EquipmentName { get; set; } = string.Empty;
    public string RobotName { get; set; } = string.Empty;
    public RobotProgramCallParams ProgramCallParameters { get; set; } = new();
}

public class RobotProgramCallParams
{
    public string ProgramName { get; set; } = string.Empty;
    public List<RobotProgramArgument> Arguments { get; set; } = new();
}

public abstract class RobotProgramArgument
{
    public string ArgumentName { get; set; } = string.Empty;
}

public class DiscreteRobotProgramArgument : RobotProgramArgument
{
    public string ArgumentValue { get; set; } = string.Empty;
}

public class ContinuousRobotProgramArgument : RobotProgramArgument
{
    public double ArgumentValue { get; set; }
}

// Stage properties
public class PositionNameAndCoords
{
    public string Name { get; set; } = string.Empty;
    public StagePosition Coordinates { get; set; } = new();
}

public class StagePosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class RelativeStageLoopParams
{
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
    public double DeltaZ { get; set; }
    public (int RangeNegative, int RangePositive) AdditionalPlanesX { get; set; }
    public (int RangeNegative, int RangePositive) AdditionalPlanesY { get; set; }
    public (int RangeNegative, int RangePositive) AdditionalPlanesZ { get; set; }
    public bool ReturnToStartingPosition { get; set; }
}

public class DefinedDetection
{
    public string Name { get; set; } = string.Empty;
    public DetectionParams Settings { get; set; } = new();
}

// ---------------------------------------------------------
// Detection Details (Stored in DefinedDetections Map)
// ---------------------------------------------------------

public class DetectionParams
{
    public List<DetectorEquipment> Detectors { get; set; } = new();
    public List<IrradiationParams> Irradiation { get; set; } = new();
    public List<MovableComponentParams> MovableComponents { get; set; } = new();
}

public class MovableComponentParams
{
    public string EquipmentName { get; set; } = string.Empty;
    public List<MovableComponentSetting> ComponentSettings { get; set; } = new();
}
    
public class MovableComponentSetting
{
    public string ComponentName { get; set; } = string.Empty;
    public string DesiredSetting { get; set; } = string.Empty;
    public string Type { get; set; } = "discretemovablesetting"; // or "continuousmovablesetting"
}

// ---------------------------------------------------------
// Top-Level Execution Payload
// ---------------------------------------------------------

public class MeasurementProgramPayload
{
    public string Action { get; set; } = "executemeasurementprogram";
    public Dictionary<string, DetectionParams> DefinedDetections { get; set; } = new();
    public MeasurementElement? Program { get; set; }
    public SmartProgramCodePayload SmartProgramCode { get; set; } = new();
}

public class SmartProgramCodePayload
{
    public string Type { get; set; } = "programrunnercode";
    public List<object> Programs { get; set; } = new(); // Abstracted as it varies between DAG/runner
}

