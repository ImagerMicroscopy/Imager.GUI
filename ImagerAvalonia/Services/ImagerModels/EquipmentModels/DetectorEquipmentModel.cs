using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Services.ImagerModels.EquipmentModels
{
    public enum PropertyKind
    {
        numeric,
        discrete
    }

    [JsonConverter(typeof(DetectorEquipmentPropertiesConverter))]
    public abstract class DetectorEquipmentProperties
    {
        public string descriptor { get; set; } = string.Empty;
        public int propertycode { get; set; }

        [JsonIgnore]
        public abstract PropertyKind kind { get; }

        public abstract DetectorEquipmentProperties Clone();
    }

    public class NumericDetectorProperty : DetectorEquipmentProperties
    {
        [JsonConverter(typeof(RoundingDoubleJsonConverter))]
        public double value { get; set; }

        [JsonIgnore]
        public override PropertyKind kind => PropertyKind.numeric;

        public NumericDetectorProperty() { }

        public NumericDetectorProperty(string descriptor, int propertycode, double value)
        {
            this.descriptor = descriptor;
            this.propertycode = propertycode;
            this.value = value;
        }

        public override DetectorEquipmentProperties Clone() =>
            new NumericDetectorProperty(descriptor, propertycode, value);
    }

    public class CategoricDetectorProperty : DetectorEquipmentProperties
    {
        public string current { get; set; } = string.Empty;
        public List<string> availableoptions { get; set; } = new();

        [JsonIgnore]
        public override PropertyKind kind => PropertyKind.discrete;

        public CategoricDetectorProperty() { }

        public CategoricDetectorProperty(
            string descriptor,
            int propertycode,
            string value,
            List<string> options)
        {
            this.descriptor = descriptor;
            this.propertycode = propertycode;
            this.current = value;
            this.availableoptions = options;
        }

        public override DetectorEquipmentProperties Clone() =>
            new CategoricDetectorProperty(
                descriptor,
                propertycode,
                current,
                new List<string>(availableoptions));
    }



    public class DetectorEquipmentModel : IEnableGated
    {
        public List<DetectorEquipmentProperties> DetectorProperties { get; set; } = new();
        public string Detectorname { get; set; }
        public DetectorEquipmentModel() { }
        public double Framerate = 20;

        [JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonIgnore]
        private const string DetectorPropertiesKey = "detectorproperties";

        public DetectorEquipmentModel(string name)
        {
            Detectorname = name;
        }

        public DetectorEquipmentModel(string name, List<DetectorEquipmentProperties> detectorProperties)
        {
            Detectorname = name;
            DetectorProperties = detectorProperties;
        }

        public DetectorEquipmentModel(DetectorEquipmentModel detectorEquipment)
        {
            Detectorname = detectorEquipment.Detectorname;
            IsEnabled = detectorEquipment.IsEnabled;

            DetectorProperties = detectorEquipment.DetectorProperties
                .Select(p => p.Clone())
                .ToList();
        }



        [JsonIgnore]
        public string EquipmentName { get; set; } = string.Empty;

        public JObject Serialize()
        {
            return JObject.FromObject(this);
        }
    }
}