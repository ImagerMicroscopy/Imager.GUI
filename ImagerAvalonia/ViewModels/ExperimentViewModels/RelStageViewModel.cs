

using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;


namespace ImagerAvalonia.ViewModels;






public partial class RelStageViewModel : MeasurementElementViewModel
{

    private readonly IStageControl _stageControl;
    private string StageName;

    [ObservableProperty] ObservableCollection<Stage> _Stages = new();
    public Stages AvailableStages {
        set {
            if (value != null)
            {
                if (value.MotorizedStages != null)
                {
                    Stages = new ObservableCollection<Stage>(value.MotorizedStages.Select(x => new Stage(x.EquipmentName, x.Name)).ToList());
                }
                foreach (var s in Stages)
                {
                    s.IsEnabled = true;
                }
                if (Stages.Count > 0)
                {
                    StageName = Stages[0].EquipmentName;
                }
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


    public RelStageViewModel(GlobalDefinedSettingsViewModel availableAcquisitions, IStageControl stageControl) {
        _stageControl = stageControl;
        
        AvailableStages = _stageControl.AvailableStages;
        DisplayedInfo = $"(X={TileNegativeX + TilePositiveX + 1},Y={TileNegativeY + TilePositiveY + 1},Z={TileNegativeZ + TilePositiveZ + 1}) ";
        Header = "Relative Stage Loop";


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
            

            
            DisplayedInfo = $"(X={TileNegativeX + TilePositiveX+1},Y={TileNegativeY + TilePositiveY+1},Z={TileNegativeZ + TilePositiveZ + 1}) ";
        };
    }



    public void OnStageSwitched(object sender) {
    }



    public override MeasurementElementBase ToModel()
    {
        int planesX = (int)((TileNegativeX ?? 0) + (TilePositiveX ?? 0) + 1);
        int planesY = (int)((TileNegativeY ?? 0) + (TilePositiveY ?? 0) + 1);
        int planesZ = (int)((TileNegativeZ ?? 0) + (TilePositiveZ ?? 0) + 1);

        var element = new RelativeStageLoopElement
        {
            StageName = StageName ?? string.Empty,
            ElementId = Elementid.ToString()
        };

        element.Params.AdditionalPlanesX[0] = (int)((TileNegativeX ?? 0));
        element.Params.AdditionalPlanesY[0] = (int)((TileNegativeY ?? 0));
        element.Params.AdditionalPlanesZ[0] = (int)((TileNegativeZ ?? 0));

        element.Params.AdditionalPlanesX[1] = (int)((TilePositiveX ?? 0));
        element.Params.AdditionalPlanesY[1] = (int)((TilePositiveY ?? 0));
        element.Params.AdditionalPlanesZ[1] = (int)((TilePositiveZ ?? 0));

        element.Params.DeltaX = (double)(StepSizeX ?? 0);
        element.Params.DeltaY = (double)(StepSizeY ?? 0);
        element.Params.DeltaZ = (double)(StepSizeZ ?? 0);

        element.Params.ReturnToStartingPosition = ReturnToStartingPosition;

        if (SelectedProgramId is not null)
        {
            element.SmartProgramId = SelectedProgramId.SmartProgramID.ToString();
        }
        else
        {
            element.SmartProgramId = null;
        }

        return element;
    }
    public override void LoadFromModel(MeasurementElementBase model, LoadContext context)
    {
        if (model is not RelativeStageLoopElement relStage)
            throw new ArgumentException($"Expected {nameof(RelativeStageLoopElement)}", nameof(model));

        base.LoadFromModel(model, context);

        StageName = relStage.StageName;

        TileNegativeX = relStage.Params.AdditionalPlanesX[2];
        TilePositiveX = relStage.Params.AdditionalPlanesX[3];
        TileNegativeY = relStage.Params.AdditionalPlanesY[2];
        TilePositiveY = relStage.Params.AdditionalPlanesY[3];
        TileNegativeZ = relStage.Params.AdditionalPlanesZ[2];
        TilePositiveZ = relStage.Params.AdditionalPlanesZ[3];

        StepSizeX = (decimal)relStage.Params.DeltaX;
        StepSizeY = (decimal)relStage.Params.DeltaY;
        StepSizeZ = (decimal)relStage.Params.DeltaZ;

        ReturnToStartingPosition = relStage.Params.ReturnToStartingPosition;

        LoadSmartProgramBinding(relStage.SmartProgramId);
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


