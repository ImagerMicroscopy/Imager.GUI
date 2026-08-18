using Autofac;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using System;
using System.Collections.Generic;

namespace ImagerAvalonia.ViewModels.MeasurementViewModels
{
    public interface IMeasurementElementViewModelFactory
    {
        MeasurementElementViewModel Create(ExperimentElementType type);
    }

    public sealed class LoadContext
    {
        public required GlobalDefinedSettingsViewModel Settings { get; init; }
        public required IStageControl StageControl { get; init; }
        public required EquipmentWorkspace EquipmentWorkspace { get; init; }
        public required ExperimentManager ExperimentManager { get; init; }
        public required IReadOnlyDictionary<string, AcquisitionSettingsViewModel> AcquisitionNameMap { get; init; }
    }

    public class MeasurementElementViewModelFactory : IMeasurementElementViewModelFactory
    {
        private readonly IComponentContext _context;

        public MeasurementElementViewModelFactory(IComponentContext context)
        {
            _context = context;
        }

        public MeasurementElementViewModel Create(ExperimentElementType type)
        {
            return type switch
            {
                ExperimentElementType.Detection => _context.Resolve<DetectionElementViewModel>(),
                ExperimentElementType.DoTimes => _context.Resolve<DoTimesViewModel>(),
                ExperimentElementType.RelativeStageLoop => _context.Resolve<RelStageViewModel>(),
                ExperimentElementType.StageLoop => _context.Resolve<StageLoopViewModel>(),
                ExperimentElementType.WaitForTime => _context.Resolve<WaitViewModel>(),
                ExperimentElementType.TimeLapse => _context.Resolve<TimeLapseViewModel>(),
                ExperimentElementType.Irradiation => _context.Resolve<IrradiationPanelViewModel>(),
                ExperimentElementType.UpdateAcquisition => _context.Resolve<UpdateAcquisitionViewModel>(),
                ExperimentElementType.Robot => _context.Resolve<RobotControlViewModel>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static MeasurementElementViewModel Build(
                MeasurementElementBase model,
                LoadContext context)
        {
            MeasurementElementViewModel vm = model switch
            {
                DetectionElement => new DetectionElementViewModel(context.Settings),
                DoTimesElement => new DoTimesViewModel(context.Settings),
                TimeLapseElement => new TimeLapseViewModel(),
                WaitElement => new WaitViewModel(),
                IrradiationElement => new IrradiationPanelViewModel(context.Settings),
                UpdateAcquisition => new UpdateAcquisitionViewModel(context.Settings),
                RelativeStageLoopElement => new RelStageViewModel(context.Settings, context.StageControl),
                StageLoopElement => new StageLoopViewModel(context.StageControl),
                ExecuteRobotProgramElement => new RobotControlViewModel(context.EquipmentWorkspace),
                _ => throw new NotSupportedException($"Unknown element type '{model.ElementType}'")
            };

            vm.LoadFromModel(model, context);

            if (model is IContainerElement)
            {
                foreach (var childModel in model.Elements)
                {
                    var childVm = Build(childModel, context);
                    childVm.Parent = vm;
                    vm.Children.Add(childVm); 
                }
            }

            return vm;
        }
    }
}