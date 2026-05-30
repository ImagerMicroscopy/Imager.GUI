using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Linq;

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

        public static XYStagePosition DefaultXYStagePosition = new XYStagePosition(float.MaxValue, float.MaxValue, float.MaxValue, false, 0, "<None>");


        public XYStagePosition PinnedPosition { get; set; }

        void SetStagePosition(XYStagePosition StagePosition);

        Stages AvailableStages { get; }

        string StageName { get; set; }

        void InitializeStageInfo();

        bool IsStageAvailable { get;  }

    }

    public class StageControl : IStageControl
    {

        public Stages? AvailableStages { get; private set; }
        public string StageName { get; set; } = string.Empty;
        public XYStagePosition PinnedPosition { get; set; } = IStageControl.DefaultXYStagePosition;
        public bool IsStageAvailable { get; private set; }
        private readonly IImagerCommunicationManager _communicationManager;

        public StageControl()
        {
            _communicationManager = ImagerCommunicationManager.Instance;

        }

        public async void InitializeStageInfo()
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

            catch (SocketException socketEx)
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    string message = socketEx.Message;

                    await ExceptionWindowHandler.ShowDialogAsync("Socket connection error when initializing stage", message, socketEx.StackTrace, mainWindow);


                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    string message = ex.Message;

                    await ExceptionWindowHandler.ShowDialogAsync("Error in getting stage information", message, ex.StackTrace, mainWindow);
                }
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
                return new XYStagePosition((float)pos.X, (float)pos.Y, (float)pos.Z, pos.UsingHardwareAutofocus, (float)pos.HardwareAutofocusOffset, StageName);
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
                StagePosition pos = new StagePosition(
                    HardwareAutofocusOffset: selected_position.PFSOffset,
                    UsingHardwareAutofocus: selected_position.IsPFSEnabled,
                    X: selected_position.XPos,
                    Y: selected_position.YPos,
                    Z: selected_position.ZPos
                );

                System.Threading.Tasks.Task.Run(() => _communicationManager.SetMotorizedStagePositionAsync(StageName, pos)).GetAwaiter().GetResult();
            }
        }
    }


    public class XYStagePosition
    {
        [JsonProperty("x")]
        public float XPos { get; set; } = 0;

        [JsonProperty("y")]
        public float YPos { get; set; } = 0;

        [JsonProperty("z")]
        public float ZPos { get; set; } = 0;

        [JsonProperty("usinghardwareautofocus")]
        public bool IsPFSEnabled { get; set; } = false;

        [JsonProperty("hardwareautofocusoffset")]
        public float PFSOffset { get; set; } = 0;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;    

        public XYStagePosition(float xPos, float yPos, float zPos, bool isPSFEnabled, float pFSOffset, string name)
        {
            XPos = xPos;
            YPos = yPos;
            ZPos = zPos;
            IsPFSEnabled = isPSFEnabled;
            PFSOffset = pFSOffset;
            Name = name;
        }

        public XYStagePosition() { }

        private const float Tolerance = 0.0001f;

        public bool IsEqual(XYStagePosition ref_positions)
        {
            if (ref_positions == null)
                return false;

            return Math.Abs(XPos - ref_positions.XPos) < Tolerance &&
                   Math.Abs(YPos - ref_positions.YPos) < Tolerance &&
                   Math.Abs(ZPos - ref_positions.ZPos) < Tolerance &&
                   Name == ref_positions.Name;
        }
    }
}
