using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ImagerAvalonia.Services.ImagerModels.EquipmentModels
{
    public class EquipmentContainer
    {
        public List<MovableComponentPart> availablemovablecomponents { get; set; } = new();
        public List<Source> availablelightsources { get; set; } = new();
        public List<RobotModel> availablerobots { get; set; } = new();

        public bool hasmotorizedstage { get; set; }
        public bool hasrobot { get; set; }

        public string motorizedstageName { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string robotname { get; set; } = string.Empty;

        public EquipmentContainer() { }
    }

    public partial interface IEquipment
    {
        public string EquipmentName { get; set; }
        public JObject Serialize();
    }
}