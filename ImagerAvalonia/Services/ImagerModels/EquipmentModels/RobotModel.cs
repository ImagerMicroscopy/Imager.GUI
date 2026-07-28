using System.Collections.Generic;

namespace ImagerAvalonia.Services.ImagerModels.EquipmentModels
{
    public partial class RobotModel
    {
        public string RobotName { get; set; } = string.Empty;

        public List<RobotPrograms> RobotPrograms { get; set; } = new();

        public string EquipmentName { get; set; } = string.Empty;
    }

    public class RobotPrograms
    {
        public string ProgramName { get; set; } = string.Empty;

        public List<ProgramArgumentsSettingsBase> ProgramArguments { get; set; } = new();
    }

    public abstract class ProgramArgumentsSettingsBase
    {
        public abstract RobotProgramArgumentType Type { get; }

        public string ProgramArgumentName { get; set; } = string.Empty;
    }

    public class DiscreterProgramArgumentSetting : ProgramArgumentsSettingsBase
    {
        public override RobotProgramArgumentType Type
            => RobotProgramArgumentType.discreteargument;

        public List<string> PermissibleValues { get; set; } = new();
    }

    public class ContinuousProgramArgumentSetting : ProgramArgumentsSettingsBase
    {
        public override RobotProgramArgumentType Type
            => RobotProgramArgumentType.continuousargument;

        public float Increment { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
    }

    public enum RobotProgramArgumentType
    {
        discreteargument,
        continuousargument
    }
}