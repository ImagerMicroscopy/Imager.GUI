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
        private readonly ComUtils _messages;

        public StageControl(ComUtils messages)
        {
            _messages = messages;

        }

        public async void InitializeStageInfo()
        {
            //ComUtils messages = new ComUtils();
            this.AvailableStages = new Stages() { };

            try
            {
                _messages.SendDataRequest(_messages.listavailableequipment, _messages.availableequipment, message_response =>
                {
                    var info = JObject.Parse(message_response);
                    var equipments = info["equipment"];
                    if (equipments != null)
                    {
                        List<Equipment> eq = equipments?.ToObject<List<Equipment>>() ?? new List<Equipment>();

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



                }, null);
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
            //XYStagePosition xy_pos = new XYStagePosition();

            if (StageName == null)
            {
                InitializeStageInfo();
            }



            if (StageName != null)
            {

                JObject response = new JObject();

                _messages.SendDataRequest(
                ComUtils.get_stageposition(StageName), "", message_response => { response = JObject.Parse(message_response); }, null);


                if(response.TryGetValue("position", out var xy_properties) )
                {
                    var xy_pos = JsonConvert.DeserializeObject<XYStagePosition>(xy_properties.ToString());
                    return StageName != null ? xy_pos : null;

                }

                else
                {
                    throw new NotImplementedException("Response contains no position key");
                }

            }
            return null;

        }




        public void SetStagePosition(XYStagePosition selected_position)
        {


            if (StageName == null)
            {
                InitializeStageInfo();
            }
            if (StageName != null)
            {

                ComUtils.SendSingleMessage(
                ComUtils.set_stageposition(StageName,
                                            selected_position.PFSOffset.ToString("F1"),
                                            selected_position.IsPFSEnabled.ToString(),
                                            selected_position.XPos.ToString("F1"),
                                            selected_position.YPos.ToString("F1"),
                                            selected_position.ZPos.ToString("F1")));

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
