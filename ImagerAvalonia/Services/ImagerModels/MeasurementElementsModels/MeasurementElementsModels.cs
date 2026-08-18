using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ImagerAvalonia.Services.MeasurementControl
{

    // =========================
    // ENUMS
    // =========================
    public enum ExperimentElementType
    {
        Detection,
        RelativeStageLoop,
        StageLoop,
        WaitForTime,
        DoTimes,
        TimeLapse,
        Irradiation,
        UpdateAcquisition,
        Robot,
    }
    // =========================
    // INTERFACES & ATTRIBUTES
    // =========================

    /// <summary>
    /// Marker interface for elements that can contain child elements.
    /// Replaces the CanHaveChildrenAttribute for compile-time safety.
    /// </summary>
    public interface IContainerElement
    {
    }


    // =========================
    // BASE
    // =========================
    public abstract class MeasurementElementBase
    {
        public abstract string ElementType { get; set; }
        public string ElementId { get; set; } = "";

        private ValidatedChildrenCollection? _elements;

        /// <summary>
        /// Gets the children collection. Declared as IList&lt;MeasurementElementBase&gt;
        /// (not List&lt;MeasurementElementBase&gt;) so every caller — including
        /// MeasurementElementExtensions.AddChild and MeasurementElementViewModel.Traverse —
        /// actually dispatches to ValidatedChildrenCollection's validated Add/Insert,
        /// instead of resolving against plain List&lt;T&gt; and skipping validation.
        /// </summary>
        public System.Collections.Generic.IList<MeasurementElementBase> Elements
        {
            get => _elements ??= new ValidatedChildrenCollection(this);
            set
            {
                _elements = null;
                if (value != null && value.Count > 0)
                {
                    var temp = new ValidatedChildrenCollection(this);
                    temp.AddRange(value);
                    _elements = temp;
                }
            }
        }
    }

    // =========================
    // ELEMENTS
    // =========================
    public class DetectionElement : MeasurementElementBase
    {
        [JsonIgnore] public List<DefinedDetection> EnabledDetectionParameters = new(); // Tracks the enabled detection parameters for this element. Is a helper to get Acq/Det pairs
        public List<string> DetectionNames { get; set; } = new();
        public List<string> SmartProgramIds { get; set; } = new();
        public override string ElementType { get => "detection"; set; }
    }

    public class RelativeStageLoopElement : MeasurementElementBase, IContainerElement
    {
        public RelativeStageLoopParams Params { get; set; } = new();
        public string StageName { get; set; } = "";
        public string? SmartProgramId { get; set; } = null;
        public override string ElementType { get => "relativestageloop"; set; }

    }

    public class RelativeStageLoopParams
    {
        public List<int> AdditionalPlanesX { get; set; } = new() { 0, 0 };
        public List<int> AdditionalPlanesY { get; set; } = new() { 0, 0 };
        public List<int> AdditionalPlanesZ { get; set; } = new() { 0, 0 };

        public double DeltaX { get; set; }
        public double DeltaY { get; set; }
        public double DeltaZ { get; set; }

        public bool ReturnToStartingPosition { get; set; }
    }

    public class DoTimesElement : MeasurementElementBase, IContainerElement
    {
        public int NTotal { get; set; }
        public string? SmartProgramId { get; set; } = null;
        public override string ElementType { get => "dotimes"; set; }

    }

    public class StageLoopElement : MeasurementElementBase, IContainerElement
    {
        public List<XYStagePosition> Positions { get; set; } = new();
        public string StageName { get; set; } = "";
        public string? SmartProgramId { get; set; } = null;
        public override string ElementType { get => "stageloop"; set; }

    }

    public class XYStagePosition
    {
        public StageCoordinates Coordinates { get; set; } 
        public string Name { get; set; } = "";

        [JsonConstructor]
        public XYStagePosition(StageCoordinates coordinates, string name)
        {
            Coordinates = coordinates;
            Name = name;
        }

        public XYStagePosition(
            double hardwareautofocusoffset,
            double x,
            double y,
            double z,
            bool usinghardwareaf,
            string name)
        {
            Coordinates = new StageCoordinates(
                hardwareautofocusoffset,
                usinghardwareaf,
                x,
                y,
                z);

            Name = name;
        }

        private float tolerance = 0.001f;
        public bool IsEqual(XYStagePosition ref_positions)
        {
            if (ref_positions == null)
                return false;

            return Math.Abs(Coordinates.x - ref_positions.Coordinates.x) < tolerance &&
                   Math.Abs(Coordinates.y - ref_positions.Coordinates.y) < tolerance &&
                   Math.Abs(Coordinates.z - ref_positions.Coordinates.z) < tolerance &&
                   Name == ref_positions.Name;
        }
    }


    public class StageCoordinates
    {
        public double hardwareautofocusoffset { get; set; }
        public bool usinghardwareautofocus { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }

        public StageCoordinates(double hardwareAutofocusOffset, bool usingHardwareAutofocus, double x, double y, double z)
        {
            hardwareautofocusoffset = hardwareAutofocusOffset;
            usinghardwareautofocus = usingHardwareAutofocus;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class WaitElement : MeasurementElementBase
    {
        public double Duration { get; set; }
        public override string ElementType { get => "wait"; set; }

    }

    public class IrradiationElement : MeasurementElementBase
    {
        public List<IrradiationConfig> Irradiation { get; set; } = new();
        public double Duration { get; set; }
        public override string ElementType { get => "irradiation"; set; }

    }

    public class IrradiationConfig
    {
        public string LightSourceName { get; set; } = "";
        public List<string> LightSourceChannel { get; set; } = new();
        public List<double> LightSourcePower { get; set; } = new();
        public string EquipmentName { get; set; } = "";
    }

    public class TimeLapseElement : MeasurementElementBase, IContainerElement
    {
        public double NTotal { get; set; }
        public double TimeDelta { get; set; }
        public string? SmartProgramId { get; set; }
        public override string ElementType { get => "timelapse"; set ; }

    }

    public class UpdateAcquisition : MeasurementElementBase
    {
        public string? SmartProgramID { get; set; } = null;
        public string AcquisitionTypeName { get; set; } = string.Empty;
        public override string ElementType { get => "updateacquisition"; set; }
        public string DetectionName { get; set; } = string.Empty;

    }

    // =========================
    // ROBOT
    // =========================
    public abstract class RobotProgramArgument
    {
        public string ArgumentName { get; set; } = "";
        public string RobotProgramArgumentType { get; set; } = "";
    }

    public class DiscreteRobotProgramArgument : RobotProgramArgument
    {
        public string Argument { get; set; } = "";
    }

    public class ContinuousRobotProgramArgument : RobotProgramArgument
    {
        public double Argument { get; set; }
    }

    public class RobotProgramCallParameters
    {
        public string ProgramName { get; set; } = "";
        public List<RobotProgramArgument> Arguments { get; set; } = new();
    }

    public class RobotProgramParameters
    {
        // Reference to the actual selected robot, rather than
        // re-deriving it later from loose name strings.
        public RobotModel Robot { get; set; } = new();

        public RobotProgramCallParameters ProgramCallParameters { get; set; } = new();
    }

    public class ExecuteRobotProgramElement : MeasurementElementBase
    {
        public RobotProgramParameters ProgramParameters { get; set; } = new();
        public override string ElementType { get => "executerobotprogram"; set; }
    }



    // ---------------------------------------------------------
    // Detection Details (Stored in DefinedDetections Map)
    // ---------------------------------------------------------


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


    // =========================
    // EXTENSIONS
    // =========================

    public static class MeasurementElementExtensions
    {
        /// <summary>
        /// Determines if an element can have child elements.
        /// Uses IContainerElement interface for compile-time type safety.
        /// </summary>
        public static bool CanHaveChildren(this MeasurementElementBase element)
            => element is IContainerElement;

        /// <summary>
        /// Safely adds a child to a parent element.
        /// Throws InvalidOperationException if parent cannot have children.
        /// </summary>
        public static void AddChild(this MeasurementElementBase parent, MeasurementElementBase child)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            parent.Elements.Add(child);
        }

        /// <summary>
        /// Counts the total number of detections in the entire measurement tree.
        /// For container elements like DoTimes with NTotal=5 containing 2 detections,
        /// this returns 5 * 2 = 10. Recursively traverses all child elements.
        /// </summary>
        public static long CountTotalDetections(this MeasurementElementBase element)
        {
            if (element == null)
                return 0;

            // If this element itself is a detection, count it
            if (element is DetectionElement)
                return 1;

            // If this element can have children, recursively count detections in children
            if (element is IContainerElement containerElement && element.Elements.Count > 0)
            {
                long detectionCount = 0;
                long multiplier = 1;

                // Determine the multiplier based on element type
                if (element is DoTimesElement doTimes)
                    multiplier = doTimes.NTotal;
                else if (element is TimeLapseElement timeLapse)
                    multiplier = (long)timeLapse.NTotal;
                else if (element is StageLoopElement stageLoop)
                    multiplier = stageLoop.Positions.Count;
                else if (element is RelativeStageLoopElement relStageLoop)
                {
                    multiplier = (1 + relStageLoop.Params.AdditionalPlanesX[0] + relStageLoop.Params.AdditionalPlanesX[1]) *
                                 (1 + relStageLoop.Params.AdditionalPlanesY[0] + relStageLoop.Params.AdditionalPlanesY[1]) *
                                 (1 + relStageLoop.Params.AdditionalPlanesZ[0] + relStageLoop.Params.AdditionalPlanesZ[1]);
                }

                // Recursively count detections in all children
                foreach (var child in element.Elements)
                {
                    detectionCount += child.CountTotalDetections();
                }

                return detectionCount * multiplier;
            }

            return 0;
        }
    }
}

