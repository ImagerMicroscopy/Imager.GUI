using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{

    public partial class StageLoopViewModel : MeasurementElementViewModel
    {
        [ObservableProperty]
        private ObservableCollection<Stage> stages;

        [ObservableProperty]
        private int currentSelectedIndex = -1;

        [ObservableProperty]
        private ObservableCollection<XYStagePosition> xYPositions;

        public readonly IStageControl StageControl;

        private string _stageName;
        private int current_id = 0;

        public Stages AvailableStages
        {
            set
            {
                if (value != null)
                {
                    Stages = new ObservableCollection<Stage>(
                        value.MotorizedStages.Select(x => new Stage(x.EquipmentName, x.Name)).ToList()
                    );

                    foreach (var s in Stages)
                    {
                        s.IsEnabled = true;
                    }

                    if (Stages.Count > 0)
                    {
                        _stageName = Stages[0].EquipmentName;
                    }
                }
            }
        }

        public int num_frames => XYPositions.Count();

        public StageLoopViewModel(IStageControl stageControl)
        {
            XYPositions = new ObservableCollection<XYStagePosition>();
            StageControl = stageControl;
            Header = "Stage Loop";


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
            XYStagePosition? xy_pos = StageControl.ReadStagePosition();
            if (xy_pos != null)
            {
                current_id++;
                xy_pos.Name = $"Pos{current_id}";
                return xy_pos;
            }
            return IStageControl.DefaultStagePosition;
        }

        public void AppendStagePosition(double x, double y, double z, bool isPSFEnabled, double pFSOffset, string name)
        {
            current_id++;
            XYStagePosition xy_pos = new XYStagePosition(pFSOffset, x, y, z, isPSFEnabled, name);
            XYPositions.Add(xy_pos);
        }

        public void AppendStagePosition(XYStagePosition xy_pos)
        {
            XYPositions.Add(xy_pos);
        }

        public void GetStagePosition()
        {
            if (_stageName != null)
            {
                var xy_pos = ReadStagePosition();
                XYPositions.Add(xy_pos);
            }
        }

        public void SetToCurrentStagePosition()
        {
            if (_stageName != null && CurrentSelectedIndex != -1 && CurrentSelectedIndex != XYPositions.Count + 1)
            {
                StageControl.StageName = _stageName;
                XYPositions[CurrentSelectedIndex] = ReadStagePosition();
            }
        }

        public void SetStagePosition()
        {
            if (_stageName != null && CurrentSelectedIndex != -1 && CurrentSelectedIndex != XYPositions.Count + 1)
            {
                try
                {
                    XYStagePosition selected_position = XYPositions[CurrentSelectedIndex];
                    StageControl.SetStagePosition(selected_position);
                }
                catch (Exception e)
                {
                    throw new Exception("Could not set stage position. An exception occured and was cause by the following exception:", e);
                }
            }
        }

        public void MoveUp()
        {
            if (CurrentSelectedIndex > 0)
            {
                int swap_indx = CurrentSelectedIndex;
                XYStagePosition xy_pos = XYPositions[swap_indx - 1];
                XYPositions[swap_indx - 1] = XYPositions[swap_indx];
                XYPositions[swap_indx] = xy_pos;

                CurrentSelectedIndex = swap_indx - 1;
            }
        }

        public void MoveDown()
        {
            if (CurrentSelectedIndex < XYPositions.Count - 1)
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

        public override MeasurementElementBase ToModel()
        {
            return new StageLoopElement
            {
                StageName = _stageName ?? "",
                Positions = XYPositions.ToList(),
                ElementId = Elementid.ToString(),
                SmartProgramId = SelectedProgramId?.SmartProgramID.ToString() ?? null
            };
        }

        public override void LoadFromModel(MeasurementElementBase measurementElement, LoadContext context)
        {
            var model = (StageLoopElement)measurementElement;

            if (Guid.TryParse(model.ElementId, out var parsedId))
            {
                Elementid = parsedId;
            }


            _stageName = model.StageName;

            XYPositions.Clear();
            foreach (var pos in model.Positions)
            {
                XYPositions.Add(pos);
            }


            current_id = XYPositions.Count;

            CurrentSelectedIndex = XYPositions.Count > 0 ? 0 : -1;

            if (model.SmartProgramId != null && Guid.TryParse(model.SmartProgramId, out var smartProgramId))
            {
                SelectedProgramId = SmartPrograms.FirstOrDefault(p => p.SmartProgramID == smartProgramId);
            }
        }
    }
}