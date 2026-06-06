

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services;
using Avalonia.Controls.ApplicationLifetimes;
using ImagerAvalonia.Exceptions;


namespace ImagerAvalonia.ViewModels;






public partial class RelStageViewModel : MeasurementViewModel
{

    private readonly IStageControl _stageControl;


    [ObservableProperty] ObservableCollection<Stage> _Stages = new();
    public Stages AvailableStages {
        set {
            if (value.MotorizedStages != null) {
                Stages = new ObservableCollection<Stage>(value.MotorizedStages.Select(x => new Stage(x.EquipmentName, x.Name)).ToList());
            }
            foreach (var s in Stages) {
                s.IsEnabled = true;
            }
            if (Stages.Count > 0) {
                StageName = Stages[0].EquipmentName;
            }
        }
    }
    [ObservableProperty] private bool _ReturnToStartingPosition = false;

    [ObservableProperty] private decimal? _stepSizeX = 0;
    [ObservableProperty] private decimal? _stepSizeY = 0;
    [ObservableProperty] private decimal? _stepSizeZ = 0;

    [ObservableProperty] private decimal? _TilePositiveX = 0;
    [ObservableProperty] private decimal? _TilePositiveY = 0;
    [ObservableProperty] private decimal? _TilePositiveZ = 0;

    [ObservableProperty] private decimal? _TileNegativeX = 0;
    [ObservableProperty] private decimal? _TileNegativeY = 0;
    [ObservableProperty] private decimal? _TileNegativeZ = 0;

    [ObservableProperty] private bool? _isFieldviewenabled = false;

    public int NumStepsX = 0;

    public int NumStepsY = 0;

    public int NumStepsZ = 0;




    private void UpdateNumStepsX() {
        NumStepsX = (int)((TilePositiveX ?? 0) + (TileNegativeX ?? 0));
    }

    private void UpdateNumStepsY() {
        NumStepsY = (int)((TilePositiveY ?? 0) + (TileNegativeY ?? 0));
    }

    private void UpdateNumStepsZ() {
        NumStepsZ = (int)((TilePositiveZ ?? 0) + (TileNegativeZ ?? 0));
    }


    public int num_frames { get { return (int)((TilePositiveX + TileNegativeX) * (TilePositiveY + TileNegativeY) * (TilePositiveZ * TileNegativeZ)); } }

    private string _stageName = string.Empty;
    public string StageName {
        get => _stageName;
        set {
            if (_stageName != value) {
                _stageName = value;
                OnPropertyChanged();
                // Delegate to ExperimentBuilder to update state
                ExperimentBuilder?.UpdateStageLoopStageName(Elementid, _stageName);
            }
        }
    }

    public RelStageViewModel(SystemDefinedSettingsViewModel availableAcquisitions, IStageControl stageControl) {
        _stageControl = stageControl;
        
        _stageControl.InitializeStageInfo();
        AvailableStages = _stageControl.AvailableStages;
        DisplayedInfo = $"(X={TileNegativeX + TilePositiveX + 1},Y={TileNegativeY + TilePositiveY + 1},Z={TileNegativeZ + TilePositiveZ + 1}) ";


        this.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(TilePositiveX)) {
                UpdateNumStepsX();
            } else if (e.PropertyName == nameof(TileNegativeX)) {
                UpdateNumStepsX();
            }

            if (e.PropertyName == nameof(TilePositiveY)) {
                UpdateNumStepsY();
            } else if (e.PropertyName == nameof(TileNegativeY)) {
                UpdateNumStepsY();
            }

            if (e.PropertyName == nameof(TilePositiveZ)) {
                UpdateNumStepsZ();
            } else if (e.PropertyName == nameof(TileNegativeZ)) {
                UpdateNumStepsZ();
            }
            
            // Delegate parameter changes to ExperimentBuilder
            if (ExperimentBuilder != null && 
                (e.PropertyName == nameof(StepSizeX) ||
                 e.PropertyName == nameof(StepSizeY) ||
                 e.PropertyName == nameof(StepSizeZ) ||
                 e.PropertyName == nameof(TileNegativeX) ||
                 e.PropertyName == nameof(TilePositiveX) ||
                 e.PropertyName == nameof(TileNegativeY) ||
                 e.PropertyName == nameof(TilePositiveY) ||
                 e.PropertyName == nameof(TileNegativeZ) ||
                 e.PropertyName == nameof(TilePositiveZ) ||
                 e.PropertyName == nameof(ReturnToStartingPosition))) {
                ExperimentBuilder.UpdateRelativeStageLoopParams(
                    Elementid,
                    (double)(StepSizeX ?? 0),
                    (double)(StepSizeY ?? 0),
                    (double)(StepSizeZ ?? 0),
                    (int)(TileNegativeX ?? 0),
                    (int)(TilePositiveX ?? 0),
                    (int)(TileNegativeY ?? 0),
                    (int)(TilePositiveY ?? 0),
                    (int)(TileNegativeZ ?? 0),
                    (int)(TilePositiveZ ?? 0),
                    ReturnToStartingPosition);
            }
            
            DisplayedInfo = $"(X={TileNegativeX + TilePositiveX+1},Y={TileNegativeY + TilePositiveY+1},Z={TileNegativeZ + TilePositiveZ + 1}) ";
        };
    }



    public void OnStageSwitched(object sender) {
    }

    public override void Dispose() {
    }
}

public class RelStageParameterSnapshot
{
    public decimal? TilePositiveX { get; set; }
    public decimal? TileNegativeX { get; set; }
    public decimal? TilePositiveY { get; set; }
    public decimal? TileNegativeY { get; set; }
    public decimal? TilePositiveZ { get; set; }
    public decimal? TileNegativeZ { get; set; }

    public decimal? StepSizeX { get; set; }
    public decimal? StepSizeY { get; set; }
    public decimal? StepSizeZ { get; set; }


    public RelStageParameterSnapshot(RelStageViewModel model) {
        TilePositiveX = model.TilePositiveX;
        TileNegativeX = model.TileNegativeX;
        TilePositiveY = model.TilePositiveY;
        TileNegativeY = model.TileNegativeY;
        TilePositiveZ = model.TilePositiveZ;
        TileNegativeZ = model.TileNegativeZ;

        StepSizeX = model.StepSizeX;
        StepSizeY = model.StepSizeY;
        StepSizeZ = model.StepSizeZ;
    }

    public void UpdateFromProperty(string propertyName, object? value) {
        switch (propertyName) {
            case nameof(TilePositiveX):
                TilePositiveX = (decimal?)value;
                break;
            case nameof(TileNegativeX):
                TileNegativeX = (decimal?)value;
                break;
            case nameof(TilePositiveY):
                TilePositiveY = (decimal?)value;
                break;
            case nameof(TileNegativeY):
                TileNegativeY = (decimal?)value;
                break;
            case nameof(TilePositiveZ):
                TilePositiveZ = (decimal?)value;
                break;
            case nameof(TileNegativeZ):
                TileNegativeZ = (decimal?)value;
                break;
            case nameof(StepSizeX):
                StepSizeX = (decimal?)value;
                break;
            case nameof(StepSizeY):
                StepSizeY = (decimal?)value;
                break;
            case nameof(StepSizeZ):
                StepSizeZ = (decimal?)value;
                break;
        }
    }
}


