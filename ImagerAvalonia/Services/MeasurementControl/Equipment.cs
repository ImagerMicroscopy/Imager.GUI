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

    

    public class Equipment
    {
        public List<MovableComponentPart> availablemovablecomponents { get; set; } = new();
        public List<Source> availablelightsources { get; set; } = new();
        public List<Robots> availablerobots { get;set;  } = new();
        public bool hasmotorizedstage { get; set; } 
        public bool hasrobot { get; set; }
        public string motorizedstageName { get; set; } = String.Empty;
        public string name { get; set; } = String.Empty;
        public string robotname { get; set; } = String.Empty;



    }

    public partial interface IEquipment
    {
        public string EquipmentName { get; set; }
        public JObject Serialize();
    }

}
