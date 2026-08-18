using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.ViewModels;

public partial class StageControlPanelViewModel : ViewModelBase
{
    // Observable properties

    [ObservableProperty] private bool _isStageAvailable = false;

    private double? _xyStep = 0.0;
    public double? XYStep
    {
        get => _xyStep;
        set
        {
            if (value < 0 || value == null)
            {
                _xyStep = 0;
            }
            else
            {
                _xyStep = value;
            }

            OnPropertyChanged();
        }
    }
    private double? _zStep = 0.0;
    public double? ZStep
    {
        get => _zStep;
        set
        {
            if (value < 0 || value == null)
            {
                _zStep = 0;
            }
            else
            {
                _zStep = value;
            }

            OnPropertyChanged();
        }
    }

    // Dependencies and internal state
    private readonly IStageControl _stageController;
  

    // Events
   
    public StageControlPanelViewModel(IStageControl stageController) 
    { 
        _stageController = stageController; 
        IsStageAvailable = _stageController.IsStageAvailable;

    }

    #region Movement Controls
    public void MoveNorth() => MoveStage(0, (float)(XYStep ?? 0), 0);
    public void MoveSouth() => MoveStage(0, (float)-(XYStep ?? 0), 0);
    public void MoveEast() => MoveStage((float)(XYStep ?? 0), 0, 0);
    public void MoveWest() => MoveStage((float)-(XYStep ?? 0), 0, 0);
    public void MoveUp() => MoveStage(0, 0, (float)(ZStep ?? 0));
    public void MoveDown() => MoveStage(0, 0, (float)-(ZStep ?? 0));
    private void MoveStage(float xDelta, float yDelta, float zDelta)
    {
        var pos = _stageController.ReadStagePosition();
        if (pos != null)
        {
            pos.Coordinates.x += xDelta;
            pos.Coordinates.y += yDelta;
            pos.Coordinates.z += zDelta;
            _stageController.SetStagePosition(pos);
        }
    }

    #endregion
    public override void Dispose()
    {

    }



}