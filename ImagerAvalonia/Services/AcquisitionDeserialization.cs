using Avalonia.Styling;
using DynamicData;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services
{

    public class SourceComponentModel
    {
        public string lightsourcename;
        public string equipmentname;
        public List<string> lightsourcechannel;
        public List<double> lightsourcepower;
    }


    public class MovableComponentModel
    {
        public List<MovableSettingPart> movablecomponentsettings;
        public string equipmentname;
    }


    public class AcquisitionSettingsDeserializationModel
    {
        public string Name { get; set; } = string.Empty;
        public List<DetectorEquipment> Detectors { get; set; } = new();
        public List<MovableComponentModel> MovableComponents { get; set; } = new();
        public List<SourceComponentModel> Irradiation { get; set; } = new();

        public DetectorEquipmentProperties GetDetectorEquipmentPropertyByName(string propertyname, string detectorname)
        {
            var detector = Detectors.Find(x => x.Detectorname == detectorname);
            var property  = detector.DetectorProperties.Find(x => x.descriptor == propertyname);
            return property;
        }
    }


    public class DefinedDetectionsConverter : JsonConverter<List<AcquisitionSettingsDeserializationModel>>
    {
        public override List<AcquisitionSettingsDeserializationModel> ReadJson(
            JsonReader reader,
            Type objectType,
            List<AcquisitionSettingsDeserializationModel> existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            try
            {
                var root = JObject.Load(reader);
                var results = new List<AcquisitionSettingsDeserializationModel>();

                if (root["defineddetections"] is JObject detectionsObj)
                {
                    foreach (var property in detectionsObj.Properties())
                    {
                        if (property.Value is not JObject detectionObj)
                            continue;

                        var availableDetectors = new List<DetectorEquipment>();
                        foreach (var detector in detectionObj["detectors"])
                        {
                            availableDetectors.Add(new DetectorEquipment(detector["detectorname"].ToString(), detector));
                        }

                        var availableComponents = new List<MovableComponentModel>();
                        foreach (var movablecomponent in detectionObj["movablecomponents"])
                        {
                            availableComponents.Add(movablecomponent.ToObject<MovableComponentModel>());
                        }

                        var availableSources = new List<SourceComponentModel>();
                        foreach (var sourcecomponent in detectionObj["irradiation"])
                        {
                            availableSources.Add(sourcecomponent.ToObject<SourceComponentModel>());
                        }

                        var model = new AcquisitionSettingsDeserializationModel
                        {
                            Name = property.Name,
                            Detectors = availableDetectors,
                            MovableComponents = availableComponents,
                            Irradiation = availableSources
                        };

                        results.Add(model);
                    }
                }
                return results;

            }
            catch
            {
                var results = new List<AcquisitionSettingsDeserializationModel>();
                return results;
            }

        }

        public override void WriteJson(JsonWriter writer, List<AcquisitionSettingsDeserializationModel> value, JsonSerializer serializer)
        {
         
        }
    }
}
