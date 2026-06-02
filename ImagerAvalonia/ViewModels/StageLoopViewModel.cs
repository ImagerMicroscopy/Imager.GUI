
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;




public partial class StageLoopViewModel : MeasurementViewModel
{

    [ObservableProperty] ObservableCollection<Stage> _Stages;
    [ObservableProperty] int _CurrentSelectedIndex = -1;

    public readonly IStageControl StageControl;
    public Stages AvailableStages
    {
        set
        {
            Stages = new ObservableCollection<Stage>(value.MotorizedStages.Select(x => new Stage(x.EquipmentName, x.Name)).ToList());

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
    private int current_id = 0;



    [ObservableProperty]
    public ObservableCollection<XYStagePosition> _XYPositions;

    private string _stageName = string.Empty;
    public string StageName
    {
        get => _stageName;
        set
        {
            if (_stageName != value)
            {
                _stageName = value;
                OnPropertyChanged();
                // Delegate to ExperimentBuilder to update state
                ExperimentBuilder?.UpdateStageLoopStageName(Elementid, _stageName);
            }
        }
    }
    public int num_frames { get { return XYPositions.Count(); } }




    public StageLoopViewModel(IStageControl stageControl)
    {

        XYPositions = new ObservableCollection<XYStagePosition>(new List<XYStagePosition>
        {

        });
        StageControl = stageControl;
        StageControl.InitializeStageInfo();

        AvailableStages = StageControl.AvailableStages;
        DisplayedInfo = "(0 positions)";
        XYPositions.CollectionChanged += XYPositions_CollectionChanged;
    }

    public void XYPositions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DisplayedInfo = $"({XYPositions.Count} positions)";
    }

    public void DeleteSelectedItem()
    {
        if (CurrentSelectedIndex >= 0 && XYPositions.Count != 0)
        {
            XYPositions.RemoveAt(CurrentSelectedIndex);
        }
        if (XYPositions.Count == 0)
        {
            CurrentSelectedIndex = -1;
        }
    }
    public XYStagePosition ReadStagePosition()
    {
        //_stageControl.StageName = StageName;
        XYStagePosition? xy_pos = StageControl.ReadStagePosition();
        if (xy_pos != null)
        {
            current_id++;
            xy_pos.Name = $"Pos{current_id}";
            return xy_pos;
        }
        return IStageControl.DefaultXYStagePosition;

    }

    public void AppendStagePosition(float xPos, float yPos, float zPos, bool isPSFEnabled, float pFSOffset,string name)
    {
        current_id++;
        //string name = $"Pos{current_id}";
        XYStagePosition xy_pos = new XYStagePosition(xPos, yPos, zPos, isPSFEnabled, pFSOffset, name);
        XYPositions.Add(xy_pos);
    }

    public void AppendStagePosition(XYStagePosition xy_pos)
    {
        XYPositions.Add(xy_pos);
    }

    public void GetStagePosition()
    {
        //_stageControl.StageName = StageName;
        if (StageName != null)
        {
            var xy_pos = ReadStagePosition();
            XYPositions.Add(xy_pos);

        }

    }
    public void SetToCurrentStagePosition()
    {

        if (StageName != null && CurrentSelectedIndex != -1 && CurrentSelectedIndex!=XYPositions.Count+1)
        {
            StageControl.StageName = StageName;
            
            XYPositions[CurrentSelectedIndex] = this.ReadStagePosition();
        }
    }


    public void SetStagePosition()
    {
        if (StageName != null && CurrentSelectedIndex != -1 && CurrentSelectedIndex != XYPositions.Count + 1)
        {
            try
            {
                XYStagePosition selected_position = XYPositions[CurrentSelectedIndex];
                StageControl.SetStagePosition(selected_position);

          
            }
            catch (Exception e) { throw new Exception("Could not set stage position. An exception occured and was cause by the following exception:",e); }
        }
    }

    public void MoveUp()
    {
        if(CurrentSelectedIndex>0 ) 
        {
            int swap_indx = CurrentSelectedIndex;
            XYStagePosition xy_pos = XYPositions[swap_indx - 1];
            XYPositions[swap_indx - 1] = XYPositions[swap_indx];
            XYPositions[swap_indx] = xy_pos;

            CurrentSelectedIndex = swap_indx-1;
        }
    }

    public void MoveDown()
    {
        if (CurrentSelectedIndex < XYPositions.Count-1)
        {
            int swap_indx = CurrentSelectedIndex;
            if (swap_indx + 1 > 0 && swap_indx + 1 < XYPositions.Count)
            {
                XYStagePosition xy_pos = XYPositions[swap_indx + 1];
                XYPositions[swap_indx + 1] = XYPositions[swap_indx];
                XYPositions[swap_indx] = xy_pos;

                CurrentSelectedIndex = swap_indx + 1;
            }
        }
    }
}

