using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels
{

    public partial class RobotControlViewModel : MeasurementElementViewModel
    {
        [ObservableProperty]
        private ObservableCollection<RobotViewModel> robots = new();

        [ObservableProperty]
        private RobotViewModel? selectedRobot;

        public RobotControlViewModel(EquipmentWorkspace equipmentWorkspace)
        {
            Robots = new ObservableCollection<RobotViewModel>(
                equipmentWorkspace.AvailableRobots.Select(r => new RobotViewModel(r, this))
            );
            Header = "Robot";
        }

        partial void OnSelectedRobotChanged(RobotViewModel? value)
        {
            if (value != null)
            {
                DisplayedInfo = value.RobotDisplayName;
            }
        }


        public override MeasurementElementBase ToModel()
        {
            var robot_program = new ExecuteRobotProgramElement()
            {
                ProgramParameters = new RobotProgramParameters
                {
                    Robot = SelectedRobot?.Robot ?? new RobotModel(),
                    ProgramCallParameters = new RobotProgramCallParameters
                    {
                        ProgramName = SelectedRobot?.SelectedRobotProgram?.ProgramName ?? "",
                        Arguments = SelectedRobot?.SelectedRobotProgram?.ProgramArguments
                            .Select(a => a.ToModel())
                            .ToList() ?? new List<RobotProgramArgument>()
                    }
                },
                ElementId = Elementid.ToString()
            };
            return robot_program;
        }

        public override void LoadFromModel(MeasurementElementBase measurementElement, LoadContext context)
        {
            var model = (ExecuteRobotProgramElement)measurementElement;
            var programParams = model.ProgramParameters;

            if (Guid.TryParse(model.ElementId, out var parsedId))
            {
                Elementid = parsedId;
            }

            var robotVm = Robots.FirstOrDefault(r =>
                r.EquipmentName == programParams.Robot.EquipmentName &&
                r.RobotName == programParams.Robot.RobotName);

            if (robotVm == null)
                return;

            SelectedRobot = robotVm;

            var programVm = robotVm.RobotPrograms.FirstOrDefault(p =>
                p.ProgramName == programParams.ProgramCallParameters.ProgramName);

            if (programVm == null)
                return;

            robotVm.SelectedRobotProgram = programVm;

            foreach (var argModel in programParams.ProgramCallParameters.Arguments)
            {
                var argVm = programVm.ProgramArguments.FirstOrDefault(a =>
                    a.ProgramArgumentName == argModel.ArgumentName);

                if (argVm == null)
                    continue;

                switch (argModel)
                {
                    case DiscreteRobotProgramArgument discreteModel when argVm is DiscreteArgumentsViewModel discreteVm:
                        discreteVm.SelectedValue = discreteModel.Argument;
                        break;

                    case ContinuousRobotProgramArgument continuousModel when argVm is ContinuousArgumentsViewModel continuousVm:
                        continuousVm.SelectedValue = (float)continuousModel.Argument;
                        break;
                }
            }
        }
    }


    public partial class RobotViewModel : ViewModelBase
    {
        public RobotModel Robot { get; }

        public string EquipmentName { get; set; } = string.Empty;

        [ObservableProperty]
        private string robotDisplayName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<RobotProgramViewModel> robotPrograms = new();

        [ObservableProperty]
        private string _robotName = string.Empty;

        [ObservableProperty]
        private RobotProgramViewModel? selectedRobotProgram;

        private readonly RobotControlViewModel _parentViewModel;

        public RobotViewModel(RobotModel robot, RobotControlViewModel parentViewModel)
        {
            Robot = robot;
            _parentViewModel = parentViewModel;
            RobotName = robot.RobotName;
            EquipmentName = robot.EquipmentName;
            RobotDisplayName = $"{robot.EquipmentName}/{robot.RobotName}";

            RobotPrograms = new ObservableCollection<RobotProgramViewModel>(
                robot.RobotPrograms.Select(p => new RobotProgramViewModel(p, this))
            );
        }
    }


    public partial class RobotProgramViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ProgramArgumentsViewModelBase> programArguments = new();

        [ObservableProperty]
        private string programName = string.Empty;

        private readonly RobotViewModel _parentViewModel;

        public RobotProgramViewModel(RobotPrograms program, RobotViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel;
            ProgramName = program.ProgramName;

            ProgramArguments = new ObservableCollection<ProgramArgumentsViewModelBase>(
                program.ProgramArguments?.Select(x => CreateArgVm(x))
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
    }


    public abstract partial class ProgramArgumentsViewModelBase : ViewModelBase
    {
        [ObservableProperty]
        private string programArgumentName = string.Empty;

        public abstract RobotProgramArgument ToModel();
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

        public override RobotProgramArgument ToModel()
        {
            return new DiscreteRobotProgramArgument
            {
                ArgumentName = ProgramArgumentName,
                RobotProgramArgumentType = "discrete",
                Argument = SelectedValue
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

        public override RobotProgramArgument ToModel()
        {
            return new ContinuousRobotProgramArgument
            {
                ArgumentName = ProgramArgumentName,
                RobotProgramArgumentType = "continuous",
                Argument = SelectedValue
            };
        }
    }

}