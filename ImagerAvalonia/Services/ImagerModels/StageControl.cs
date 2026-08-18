using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace ImagerAvalonia.Services.MeasurementControl
{

    public class Stages
    {
        public List<Stage> MotorizedStages = new List<Stage>();
        public Stages() { }

    }

    public class Stage : IEquipment
    {
        public string Name;

        public string EquipmentName { get; set; }

        public bool IsEnabled;

        public Stage(string equipmentName, string name)
        {
            Name = name;
            EquipmentName = equipmentName;
            IsEnabled = false;
        }

        public JObject Serialize()
        {
            throw new NotImplementedException("Stage does not support serialization");
        }
    }


    public interface IStageControl
    {
        XYStagePosition? ReadStagePosition();

        public static XYStagePosition DefaultStagePosition = 
            new XYStagePosition(0, float.MaxValue, float.MaxValue, float.MaxValue, false, "<None>");


        public XYStagePosition PinnedPosition { get; set; }

        void SetStagePosition(XYStagePosition StagePosition);

        Stages AvailableStages { get; }

        string StageName { get; set; }

        Task InitializeStageInfo();

        bool IsStageAvailable { get;  }

    }

    public class StageControl : IStageControl
    {

        public Stages? AvailableStages { get; private set; }
        public string StageName { get; set; } = string.Empty;
        public XYStagePosition PinnedPosition { get; set; } = IStageControl.DefaultStagePosition;
        public bool IsStageAvailable { get; private set; } = false;
        private readonly IImagerCommunicationManager _communicationManager;

        public StageControl()
        {
            _communicationManager = ImagerCommunicationManager.Instance;

        }

        public async Task InitializeStageInfo()
        {
            this.AvailableStages = new Stages() { };

            try
            {
                
                var eq = await _communicationManager.ListAvailableEquipmentAsync();
                foreach (var equipment in eq)
                {
                    if (equipment.hasmotorizedstage)
                    {
                        this.StageName = equipment.motorizedstageName;
                        this.AvailableStages.MotorizedStages.Add(new Stage(equipment.motorizedstageName, equipment.name));
                        IsStageAvailable = true;
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }





        public XYStagePosition? ReadStagePosition()
        {
            if (string.IsNullOrEmpty(StageName))
            {
                InitializeStageInfo();
            }

            if (!string.IsNullOrEmpty(StageName))
            {
                var pos = System.Threading.Tasks.Task.Run(() => _communicationManager.GetMotorizedStagePositionAsync(StageName)).GetAwaiter().GetResult();
                return new
                    XYStagePosition(pos.Coordinates, StageName );

            }
            return null;
        }

        public void SetStagePosition(XYStagePosition selected_position)
        {
            if (string.IsNullOrEmpty(StageName))
            {
                InitializeStageInfo();
            }
            if (!string.IsNullOrEmpty(StageName))
            {
                StageCoordinates pos = new StageCoordinates(
                    selected_position.Coordinates.hardwareautofocusoffset,
                    selected_position.Coordinates.usinghardwareautofocus,
                    selected_position.Coordinates.x,
                    selected_position.Coordinates.y,
                    selected_position.Coordinates.z  
                    );

                System.Threading.Tasks.Task.Run(() => _communicationManager.SetMotorizedStagePositionAsync(StageName, pos)).GetAwaiter().GetResult();
            }
        }
    }
}
