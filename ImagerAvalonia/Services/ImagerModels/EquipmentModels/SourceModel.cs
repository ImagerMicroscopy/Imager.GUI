using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Services.ImagerModels.EquipmentModels
{
    public partial class Source : IEnableGated
    {
        public string EquipmentName { get; set; } = string.Empty;

        public string LightSourceName;

        public List<string> LightsourceChannel;
        public List<int> LightsourcePower;

        [JsonIgnore]
        public bool IsEnabled { get; set;  }

        [JsonIgnore]
        public bool allowmultiplechannels { get; set; }

        [JsonIgnore]
        public bool cancontrolpower { get; set; }

        [JsonIgnore]
        public List<string> AvailableChannels;

        [JsonConstructor]
        public Source(
            bool allowmultiplechannels,
            bool cancontrolpower,
            List<string> channels,
            string name)
        {
            this.allowmultiplechannels = allowmultiplechannels;
            this.cancontrolpower = cancontrolpower;

            this.LightSourceName = name;
            this.AvailableChannels = channels ?? new List<string>();

            this.LightsourceChannel = new List<string>();
            this.LightsourcePower = new List<int>();
        }

        public Source(Source old_source)
        {
            allowmultiplechannels = old_source.allowmultiplechannels;
            cancontrolpower = old_source.cancontrolpower;

            LightSourceName = old_source.LightSourceName;
            AvailableChannels = new List<string>(old_source.AvailableChannels);

            EquipmentName = old_source.EquipmentName;
            IsEnabled = old_source.IsEnabled;
            LightsourceChannel = old_source.LightsourceChannel
                .Select(x => x)
                .ToList();

            LightsourcePower = old_source.LightsourcePower
                .Select(x => x)
                .ToList();
        }
    }
}