using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels
{

    public partial class RobotControlViewModel : MeasurementViewModel
    {
        [ObservableProperty]
        private ObservableCollection<RobotViewModel> robots = new();

        [ObservableProperty]
        private RobotViewModel? selectedRobot;

        public RobotControlViewModel(List<Robots> robots)
        {
            Robots = new ObservableCollection<RobotViewModel>(
                robots.Select(r => new RobotViewModel(r))
            );
        }

        internal JToken? Serialize()
        {
            return SelectedRobot?.ToJson();
        }

        partial void OnSelectedRobotChanged(RobotViewModel? value)
        {
            if (value != null)
            {
                DisplayedInfo = value.RobotDisplayName;
            }
        }
    }


    public partial class RobotViewModel : ViewModelBase
    {
        public string EquipmentName { get; set; } = string.Empty;
        public string RobotName { get; private set; } = string.Empty;

        [ObservableProperty]
        private string robotDisplayName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<RobotProgramViewModel> robotPrograms = new();

        [ObservableProperty]
        private string _robotName = string.Empty;

        [ObservableProperty]
        private RobotProgramViewModel? selectedRobotProgram;

        public RobotViewModel(Robots robot)
        {
            RobotName = robot.robotname;
            EquipmentName = robot.EquipmentName;
            RobotDisplayName = $"{robot.EquipmentName}/{robot.robotname}";

            RobotPrograms = new ObservableCollection<RobotProgramViewModel>(
                robot.robotPrograms.Select(p => new RobotProgramViewModel(p))
            );
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["equipmentname"] = EquipmentName,
                ["robotname"] = RobotName,
                ["programcallparameters"] = SelectedRobotProgram?.ToJson()
            };
        }
    }


    public partial class RobotProgramViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ProgramArgumentsViewModelBase> programArguments = new();

        [ObservableProperty]
        private string programName = string.Empty;

        public RobotProgramViewModel(RobotPrograms program)
        {
            ProgramName = program.programname;

            ProgramArguments = new ObservableCollection<ProgramArgumentsViewModelBase>(
                program.programArguments?.Select(x => CreateArgVm(x))
                ?? Enumerable.Empty<ProgramArgumentsViewModelBase>()
            );
        }

        private ProgramArgumentsViewModelBase CreateArgVm(ProgramArgumentsSettingsBase arg)
        {
            return arg.Type switch
            {
                RobotProgramArgumentType.discreteargument =>
                    new DiscreteArgumentsViewModel((DiscreterProgramArgumentSetting)arg),

                RobotProgramArgumentType.continuousargument =>
                    new ContinuousArgumentsViewModel((ContinuousProgramArgumentSetting)arg),

                _ => throw new NotSupportedException()
            };
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["programname"] = ProgramName,
                ["arguments"] = new JArray(
                    ProgramArguments.Select(a => a.ToJson())
                )
            };
        }
    }


    public abstract partial class ProgramArgumentsViewModelBase : ViewModelBase
    {
        [ObservableProperty]
        private string programArgumentName = string.Empty;

        public abstract JObject ToJson();
    }

    public partial class DiscreteArgumentsViewModel : ProgramArgumentsViewModelBase
    {
        public ObservableCollection<string> PermissibleValues { get; }

        [ObservableProperty]
        private string selectedValue = string.Empty;

        public DiscreteArgumentsViewModel(DiscreterProgramArgumentSetting setting)
        {
            ProgramArgumentName = setting.ProgramArgumentName;
            PermissibleValues = new ObservableCollection<string>(setting.PermissibleValues);
            SelectedValue = PermissibleValues.FirstOrDefault() ?? "";
        }

        public override JObject ToJson()
        {
            return new JObject
            {
                ["argumentname"] = ProgramArgumentName,
                ["robotprogramargumenttype"] = "discrete",
                ["argument"] = SelectedValue
            };
        }
    }


    public partial class ContinuousArgumentsViewModel : ProgramArgumentsViewModelBase
    {
        [ObservableProperty] private float minValue;
        [ObservableProperty] private float maxValue;
        [ObservableProperty] private float incriment;
        [ObservableProperty] private float selectedValue;

        public ContinuousArgumentsViewModel(ContinuousProgramArgumentSetting setting)
        {
            ProgramArgumentName = setting.ProgramArgumentName;
            MinValue = setting.MinValue;
            MaxValue = setting.MaxValue;
            Incriment = setting.Increment;
        }

        public override JObject ToJson()
        {
            return new JObject
            {
                ["argumentname"] = ProgramArgumentName,
                ["robotprogramargumenttype"] = "continuous",
                ["argument"] = SelectedValue
            };
        }
    }
}