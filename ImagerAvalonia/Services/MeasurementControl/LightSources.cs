using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.Services.MeasurementControl
{


    public partial class Source : IEquipment
    {

        public string EquipmentName { get; set; } = String.Empty;
        public string LightSourceName;
        public List<string> LightsourceChannel;
        public List<int> LightsourcePower;


        [JsonIgnore]
        public bool allowmultiplechannels { get; set; }
        [JsonIgnore]
        public bool cancontrolpower { get; set; }
        [JsonIgnore]
        public List<string> AvailableChannels;



        [JsonConstructor]
        public Source(bool allowmultiplechannels, bool cancontrolpower, List<string> channels, string name)
        {
            this.allowmultiplechannels = allowmultiplechannels;
            this.cancontrolpower = cancontrolpower;

            this.LightSourceName = name;
            this.AvailableChannels = channels;

            this.LightsourceChannel = new();
            this.LightsourcePower = new();
        }


        public Source(Source old_source)
        {
            this.allowmultiplechannels = old_source.allowmultiplechannels;
            this.cancontrolpower = old_source.cancontrolpower;

            this.LightSourceName = old_source.LightSourceName;
            this.AvailableChannels = old_source.AvailableChannels;

            this.EquipmentName = old_source.EquipmentName;

            this.LightsourceChannel = old_source.LightsourceChannel.Select(x => x).ToList();
            this.LightsourcePower = old_source.LightsourcePower.Select(x => x).ToList();
        }


        public JObject Serialize()
        {

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new PrivateContractResolver(),
            };

            return JObject.FromObject(this, JsonSerializer.Create(settings));
        }

    }

}
