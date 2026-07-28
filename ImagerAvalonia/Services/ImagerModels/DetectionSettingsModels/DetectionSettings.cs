using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Services.MeasurementControl
{

    public class DefinedDetection
    {
        [JsonIgnore]
        public string Name { get; set; } = string.Empty;
        public DetectionParams Settings { get; set; } = new();

        public DefinedDetection Clone()
        {
            return new DefinedDetection
            {
                Name = Name,
                Settings = Settings.Clone()
            };
        }
    }

    // ---------------------------------------------------------
    // Detection Details (Stored in DefinedDetections Map)
    // ---------------------------------------------------------

    public class DetectionParams
    {
        public List<DetectorEquipmentModel> Detectors { get; set; } = new();
        public List<Source> Irradiation { get; set; } = new();
        public List<MovableComponentModel> MovableComponents { get; set; } = new();

        public DetectionParams Clone()
        {
            return new DetectionParams
            {
                Detectors = Detectors
                    .Select(d => new DetectorEquipmentModel(d))
                    .ToList(),

                Irradiation = Irradiation
                    .Select(s => new Source(s))
                    .ToList(),

                MovableComponents = MovableComponents
                    .Select(m => new MovableComponentModel(m))
                    .ToList()
            };
        }
    }
    

    public class DetectionSettingsFactory
    {
        public static DefinedDetection FromComponents(string name,
            List<Source> sources, List<MovableComponentModel> filterWheels, List<DetectorEquipmentModel> detectors)
        {
            var settings = new DefinedDetection();
            settings.Settings = new DetectionParams()
            {
                Irradiation = sources.Select(x => new Source(x)).ToList(),
                MovableComponents = filterWheels.Select(x => new MovableComponentModel(x)).ToList(),
                Detectors = detectors.Select(x => new DetectorEquipmentModel(x)).ToList()
            };

            settings.Name = name;

            return settings;
        }
    }
}
