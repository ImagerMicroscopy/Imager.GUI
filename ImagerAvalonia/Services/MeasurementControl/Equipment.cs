using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;


namespace ImagerAvalonia.Services.MeasurementControl
{

    

    public class Equipment {
        public List<MovableComponentPart> availablemovablecomponents { get; set; } = new();
        public List<Source> availablelightsources { get; set; } = new();
        public List<Robots> availablerobots { get;set;  } = new();
        public bool hasmotorizedstage { get; set; } 
        public bool hasrobot { get; set; }
        public string motorizedstageName { get; set; } = String.Empty;
        public string name { get; set; } = String.Empty;
        public string robotname { get; set; } = String.Empty;

        public Equipment() {}

        public Equipment(System.Text.Json.JsonElement element) {
            name = element.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            hasmotorizedstage = element.TryGetProperty("hasmotorizedstage", out var hs) && hs.GetBoolean();
            hasrobot = element.TryGetProperty("hasrobot", out var hr) && hr.GetBoolean();
            motorizedstageName = element.TryGetProperty("motorizedstageName", out var msn) ? msn.GetString() ?? "" : "";
            robotname = element.TryGetProperty("robotname", out var rn) ? rn.GetString() ?? "" : "";
            
            if (element.TryGetProperty("availablelightsources", out var lsArray) && lsArray.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var ls in lsArray.EnumerateArray()) {
                    availablelightsources.Add(new Source(ls));
                }
            }
            if (element.TryGetProperty("availablemovablecomponents", out var mcArray) && mcArray.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var mc in mcArray.EnumerateArray()) {
                    availablemovablecomponents.Add(new MovableComponentPart(mc));
                }
            }
            if (element.TryGetProperty("availablerobots", out var rArray) && rArray.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var r in rArray.EnumerateArray()) {
                    availablerobots.Add(new Robots(r));
                }
            }
        }
    }

    public partial interface IEquipment
    {
        public string EquipmentName { get; set; }
        public JObject Serialize();
    }

}
