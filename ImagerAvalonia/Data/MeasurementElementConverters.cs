using System;
using System.Collections.Generic;
using System.Linq;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Data.Measurements;

public static class MeasurementElementConverters
{
    public static MEDoTimes ToMeasurementElement(this DoTimesViewModel vm)
    {
        return new MEDoTimes
        {
            ElementId = vm.Elementid,
            NumIterationsTotal = vm.NumRepeats,
            SmartProgramId = vm.SelectedProgramId?.SmartProgramID.ToString(),
            Elements = new List<MeasurementElement>()
        };
    }

    public static METimeLapse ToMeasurementElement(this TimeLapseViewModel vm)
    {
        return new METimeLapse
        {
            ElementId = vm.Elementid,
            NumIterationsTotal = (int)(vm.NTimes ?? 1),
            WaitDurationInSeconds = (double)(vm.TimeDelta ?? 0.001m),
            SmartProgramId = vm.SelectedProgramId?.SmartProgramID.ToString(),
            Elements = new List<MeasurementElement>()
        };
    }

    public static MEWait ToMeasurementElement(this WaitViewModel vm)
    {
        return new MEWait
        {
            ElementId = vm.Elementid,
            DurationInSeconds = vm.WaitPeriod
        };
    }

    public static MEStageLoop ToMeasurementElement(this StageLoopViewModel vm)
    {
        return new MEStageLoop
        {
            ElementId = vm.Elementid,
            StageName = vm.StageName ?? string.Empty,
            SmartProgramId = vm.SelectedProgramId?.SmartProgramID.ToString(),
            Positions = vm.XYPositions.Select(p => new PositionNameAndCoords
            {
                Name = p.Name,
                Coordinates = new StagePosition
                {
                    X = (double)p.XPos,
                    Y = (double)p.YPos,
                    Z = (double)p.ZPos
                }
            }).ToList(),
            Elements = new List<MeasurementElement>()
        };
    }

    public static MERelativeStageLoop ToMeasurementElement(this RelStageViewModel vm)
    {
        return new MERelativeStageLoop
        {
            ElementId = vm.Elementid,
            StageName = vm.StageName ?? string.Empty,
            SmartProgramId = vm.SelectedProgramId?.SmartProgramID.ToString(),
            Params = new RelativeStageLoopParams
            {
                DeltaX = (double)(vm.StepSizeX ?? 0),
                DeltaY = (double)(vm.StepSizeY ?? 0),
                DeltaZ = (double)(vm.StepSizeZ ?? 0),
                AdditionalPlanesX = ((int)(vm.TileNegativeX ?? 0), (int)(vm.TilePositiveX ?? 0)),
                AdditionalPlanesY = ((int)(vm.TileNegativeY ?? 0), (int)(vm.TilePositiveY ?? 0)),
                AdditionalPlanesZ = ((int)(vm.TileNegativeZ ?? 0), (int)(vm.TilePositiveZ ?? 0)),
                ReturnToStartingPosition = vm.ReturnToStartingPosition
            },
            Elements = new List<MeasurementElement>()
        };
    }

    public static MEIrradiation ToMeasurementElement(this IrradiationPanelViewModel vm)
    {
        var irradiations = new List<IrradiationParams>();
        foreach (var sourceVm in vm.SourcesViewModels)
        {
            if (sourceVm.Channels == null) continue;
            var enabledChannels = sourceVm.Channels.Where(c => c.IsEnabled).ToList();
            if (enabledChannels.Count == 0) continue;
            irradiations.Add(new IrradiationParams
            {
                EquipmentName = sourceVm.EquipmentName,
                LightSourceName = sourceVm.LightSource.LightSourceName,
                LightSourceChannels = enabledChannels.Select(c => c.Name).ToList(),
                Powers = enabledChannels.Select(c => (double)c.PowerLevel).ToList()
            });
        }
        return new MEIrradiation
        {
            ElementId = vm.Elementid,
            DurationInSeconds = vm.IrradiationTimes,
            Irradiation = irradiations
        };
    }

    public static MEExecuteRobotProgram ToMeasurementElement(this RobotControlViewModel vm)
    {
        if (vm.SelectedRobot == null)
        {
            return new MEExecuteRobotProgram
            {
                ElementId = vm.Elementid,
                ProgramParameters = new RobotProgramExecutionParams()
            };
        }
        var programParams = new RobotProgramCallParams
        {
            ProgramName = vm.SelectedRobot.SelectedRobotProgram?.ProgramName ?? string.Empty,
            Arguments = ConvertRobotArguments(vm.SelectedRobot.SelectedRobotProgram)
        };
        return new MEExecuteRobotProgram
        {
            ElementId = vm.Elementid,
            ProgramParameters = new RobotProgramExecutionParams
            {
                EquipmentName = vm.SelectedRobot.EquipmentName,
                RobotName = vm.SelectedRobot.RobotName,
                ProgramCallParameters = programParams
            }
        };
    }

    public static MEUpdateAcquisition ToMeasurementElement(this UpdateAcquisitionViewModel vm)
    {
        var detectionName = vm.ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name;
        return new MEUpdateAcquisition
        {
            ElementId = vm.Elementid,
            SmartProgramId = vm.SelectedProgramId?.SmartProgramID.ToString(),
            DetectionName = detectionName
        };
    }

    public static MEDetection ToMeasurementElement(this AcquisitionPanelViewModel vm)
    {
        var detectionNames = vm.IsAquisitionEnabled
            .Where(ea => ea.IsEnabled && ea.acquisition?.Name != null)
            .Select(ea => ea.acquisition.Name)
            .ToList();
        var smartProgramIds = new List<string>();
        if (vm.SelectedProgramId != null)
        {
            smartProgramIds.Add(vm.SelectedProgramId.SmartProgramID.ToString());
        }
        return new MEDetection
        {
            ElementId = vm.Elementid,
            DetectionNames = detectionNames,
            SmartProgramIds = smartProgramIds
        };
    }

    public static void UpdateFromMeasurementElement(this DoTimesViewModel vm, MEDoTimes element)
    {
        vm.Elementid = element.ElementId;
        vm.NumRepeats = element.NumIterationsTotal;
    }

    public static void UpdateFromMeasurementElement(this TimeLapseViewModel vm, METimeLapse element)
    {
        vm.Elementid = element.ElementId;
        vm.NTimes = element.NumIterationsTotal;
        vm.TimeDelta = (decimal)element.WaitDurationInSeconds;
    }

    public static void UpdateFromMeasurementElement(this WaitViewModel vm, MEWait element)
    {
        vm.Elementid = element.ElementId;
        vm.WaitPeriod = element.DurationInSeconds;
    }

    public static void UpdateFromMeasurementElement(this StageLoopViewModel vm, MEStageLoop element)
    {
        vm.Elementid = element.ElementId;
        vm.StageName = element.StageName;
        var positions = element.Positions.Select(p => new XYStagePosition
        {
            Name = p.Name,
            XPos = (float)p.Coordinates.X,
            YPos = (float)p.Coordinates.Y,
            ZPos = (float)p.Coordinates.Z,
            IsPFSEnabled = false,
            PFSOffset = 0.0f
        }).ToList();
        vm.XYPositions = new System.Collections.ObjectModel.ObservableCollection<XYStagePosition>(positions);
    }

    public static void UpdateFromMeasurementElement(this RelStageViewModel vm, MERelativeStageLoop element)
    {
        vm.Elementid = element.ElementId;
        vm.StageName = element.StageName;
        vm.StepSizeX = (decimal)element.Params.DeltaX;
        vm.StepSizeY = (decimal)element.Params.DeltaY;
        vm.StepSizeZ = (decimal)element.Params.DeltaZ;
        vm.TileNegativeX = element.Params.AdditionalPlanesX.RangeNegative;
        vm.TilePositiveX = element.Params.AdditionalPlanesX.RangePositive;
        vm.TileNegativeY = element.Params.AdditionalPlanesY.RangeNegative;
        vm.TilePositiveY = element.Params.AdditionalPlanesY.RangePositive;
        vm.TileNegativeZ = element.Params.AdditionalPlanesZ.RangeNegative;
        vm.TilePositiveZ = element.Params.AdditionalPlanesZ.RangePositive;
        vm.ReturnToStartingPosition = element.Params.ReturnToStartingPosition;
    }

    public static void UpdateFromMeasurementElement(this IrradiationPanelViewModel vm, MEIrradiation element)
    {
        vm.Elementid = element.ElementId;
        vm.IrradiationTimes = element.DurationInSeconds;
    }

    public static void UpdateFromMeasurementElement(this RobotControlViewModel vm, MEExecuteRobotProgram element)
    {
        vm.Elementid = element.ElementId;
        if (element.ProgramParameters == null) return;
        foreach (var robotVm in vm.Robots)
        {
            if (robotVm.EquipmentName == element.ProgramParameters.EquipmentName &&
                robotVm.RobotName == element.ProgramParameters.RobotName)
            {
                vm.SelectedRobot = robotVm;
                if (element.ProgramParameters.ProgramCallParameters != null)
                {
                    foreach (var programVm in robotVm.RobotPrograms)
                    {
                        if (programVm.ProgramName == element.ProgramParameters.ProgramCallParameters.ProgramName)
                        {
                            robotVm.SelectedRobotProgram = programVm;
                            UpdateRobotArguments(programVm, element.ProgramParameters.ProgramCallParameters.Arguments);
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    public static void UpdateFromMeasurementElement(this UpdateAcquisitionViewModel vm, MEUpdateAcquisition element)
    {
        vm.Elementid = element.ElementId;
        if (element.DetectionName != null)
        {
            foreach (var acq in vm.ToUpdateAcquisitions)
            {
                if (acq.Name == element.DetectionName)
                {
                    acq.Enabledupdate = true;
                    break;
                }
            }
        }
    }

    public static void UpdateFromMeasurementElement(this AcquisitionPanelViewModel vm, MEDetection element)
    {
        vm.Elementid = element.ElementId;
        foreach (var acq in vm.IsAquisitionEnabled)
        {
            acq.IsEnabled = element.DetectionNames.Contains(acq.Name);
        }
    }

    public static void UpdateViewModelFromState(object viewModel, MeasurementElement element)
    {
        if (viewModel is DoTimesViewModel vm1 && element is MEDoTimes e1)
            vm1.UpdateFromMeasurementElement(e1);
        else if (viewModel is TimeLapseViewModel vm2 && element is METimeLapse e2)
            vm2.UpdateFromMeasurementElement(e2);
        else if (viewModel is WaitViewModel vm3 && element is MEWait e3)
            vm3.UpdateFromMeasurementElement(e3);
        else if (viewModel is StageLoopViewModel vm4 && element is MEStageLoop e4)
            vm4.UpdateFromMeasurementElement(e4);
        else if (viewModel is RelStageViewModel vm5 && element is MERelativeStageLoop e5)
            vm5.UpdateFromMeasurementElement(e5);
        else if (viewModel is IrradiationPanelViewModel vm6 && element is MEIrradiation e6)
            vm6.UpdateFromMeasurementElement(e6);
        else if (viewModel is RobotControlViewModel vm7 && element is MEExecuteRobotProgram e7)
            vm7.UpdateFromMeasurementElement(e7);
        else if (viewModel is UpdateAcquisitionViewModel vm8 && element is MEUpdateAcquisition e8)
            vm8.UpdateFromMeasurementElement(e8);
        else if (viewModel is AcquisitionPanelViewModel vm9 && element is MEDetection e9)
            vm9.UpdateFromMeasurementElement(e9);
    }

    public static MeasurementElement? ToMeasurementElementTree(this NodeBase node)
    {
        if (node.NodeViewModel == null) return null;
        MeasurementElement? element = null;
        if (node.NodeViewModel is DoTimesViewModel vm1) element = vm1.ToMeasurementElement();
        else if (node.NodeViewModel is TimeLapseViewModel vm2) element = vm2.ToMeasurementElement();
        else if (node.NodeViewModel is WaitViewModel vm3) element = vm3.ToMeasurementElement();
        else if (node.NodeViewModel is StageLoopViewModel vm4) element = vm4.ToMeasurementElement();
        else if (node.NodeViewModel is RelStageViewModel vm5) element = vm5.ToMeasurementElement();
        else if (node.NodeViewModel is IrradiationPanelViewModel vm6) element = vm6.ToMeasurementElement();
        else if (node.NodeViewModel is RobotControlViewModel vm7) element = vm7.ToMeasurementElement();
        else if (node.NodeViewModel is UpdateAcquisitionViewModel vm8) element = vm8.ToMeasurementElement();
        else if (node.NodeViewModel is AcquisitionPanelViewModel vm9) element = vm9.ToMeasurementElement();
        
        if (element == null) return null;
        
        if (element is MEDoTimes doTimes)
        {
            doTimes.Elements = node.Children.Select(c => ToMeasurementElementTree(c)).Where(e => e != null).Cast<MeasurementElement>().ToList();
        }
        else if (element is METimeLapse timeLapse)
        {
            timeLapse.Elements = node.Children.Select(c => ToMeasurementElementTree(c)).Where(e => e != null).Cast<MeasurementElement>().ToList();
        }
        else if (element is MEStageLoop stageLoop)
        {
            stageLoop.Elements = node.Children.Select(c => ToMeasurementElementTree(c)).Where(e => e != null).Cast<MeasurementElement>().ToList();
        }
        else if (element is MERelativeStageLoop relStageLoop)
        {
            relStageLoop.Elements = node.Children.Select(c => ToMeasurementElementTree(c)).Where(e => e != null).Cast<MeasurementElement>().ToList();
        }
        return element;
    }

    public static List<RobotProgramArgument> ConvertRobotArguments(RobotProgramViewModel? programVm)
    {
        if (programVm == null) return new List<RobotProgramArgument>();
        var args = new List<RobotProgramArgument>();
        foreach (var arg in programVm.ProgramArguments)
        {
            if (arg is DiscreteArgumentsViewModel d)
                args.Add(new DiscreteRobotProgramArgument { ArgumentName = d.ProgramArgumentName, ArgumentValue = d.SelectedValue });
            else if (arg is ContinuousArgumentsViewModel c)
                args.Add(new ContinuousRobotProgramArgument { ArgumentName = c.ProgramArgumentName, ArgumentValue = (double)c.SelectedValue });
        }
        return args;
    }

    private static void UpdateRobotArguments(RobotProgramViewModel programVm, List<RobotProgramArgument> arguments)
    {
        if (programVm == null || arguments == null) return;
        foreach (var arg in arguments)
        {
            foreach (var argVm in programVm.ProgramArguments)
            {
                if (argVm.ProgramArgumentName == arg.ArgumentName)
                {
                    if (arg is DiscreteRobotProgramArgument d && argVm is DiscreteArgumentsViewModel dVm)
                        dVm.SelectedValue = d.ArgumentValue;
                    else if (arg is ContinuousRobotProgramArgument c && argVm is ContinuousArgumentsViewModel cVm)
                        cVm.SelectedValue = (float)c.ArgumentValue;
                    break;
                }
            }
        }
    }
}

public static class DetectionConverters
{
    public static Dictionary<string, DetectionParams> ToDetectionParamsDictionary(this List<DefinedDetection> definedDetections)
    {
        return definedDetections.ToDictionary(dd => dd.Name, dd => dd.Settings);
    }
    public static List<DefinedDetection> ToDefinedDetectionList(this Dictionary<string, DetectionParams> detectionParams)
    {
        return detectionParams.Select(kvp => new DefinedDetection { Name = kvp.Key, Settings = kvp.Value }).ToList();
    }
}

public static class MeasurementElementFactory
{
    public static MeasurementElement Create(string elementType, Guid? elementId = null)
    {
        MeasurementElement element = elementType switch
        {
            "detection" => new MEDetection(),
            "irradiation" => new MEIrradiation(),
            "wait" => new MEWait(),
            "executerobotprogram" => new MEExecuteRobotProgram(),
            "dotimes" => new MEDoTimes(),
            "timelapse" => new METimeLapse(),
            "stageloop" => new MEStageLoop(),
            "relativestageloop" => new MERelativeStageLoop(),
            "updateacquisition" => new MEUpdateAcquisition(),
            _ => throw new ArgumentException($"Unknown measurement element type: {elementType}")
        };
        if (elementId.HasValue) element.ElementId = elementId.Value;
        return element;
    }
}

public static class MeasurementElementTypeInfo
{
    public static string GetElementType(this MeasurementElement element)
    {
        return element switch
        {
            MEDetection _ => "detection",
            MEIrradiation _ => "irradiation",
            MEWait _ => "wait",
            MEExecuteRobotProgram _ => "executerobotprogram",
            MEDoTimes _ => "dotimes",
            METimeLapse _ => "timelapse",
            MEStageLoop _ => "stageloop",
            MERelativeStageLoop _ => "relativestageloop",
            MEUpdateAcquisition _ => "updateacquisition",
            _ => throw new ArgumentException($"Unknown measurement element type: {element.GetType().Name}")
        };
    }
    public static bool IsContainerElement(this MeasurementElement element)
    {
        return element is MEDoTimes or METimeLapse or MEStageLoop or MERelativeStageLoop;
    }
}
