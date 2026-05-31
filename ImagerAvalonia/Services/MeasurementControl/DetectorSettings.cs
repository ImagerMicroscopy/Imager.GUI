using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImagerAvalonia.Services.MeasurementControl {
    public enum PropertyKind {
        numeric,
        discrete
    }

    public class DetectorPropertyDiscriminator {
        public PropertyKind kind { get; set; }
    }

    public abstract class DetectorEquipmentProperties {
        public string descriptor { get; set; } = String.Empty;
        public int propertycode { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public abstract PropertyKind kind { get; }
    }


    public class NumericDetectorProperty : DetectorEquipmentProperties {
        [JsonConverter(typeof(RoundingDoubleConverter), 6)]
        public double value { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public override PropertyKind kind => PropertyKind.numeric;

        public NumericDetectorProperty() { }

        public NumericDetectorProperty(string descriptor, int propertycode, double value) {
            this.value = value;
            this.descriptor = descriptor;
            this.propertycode = propertycode;
        }
    }


    public class CategoricDetectorProperty : DetectorEquipmentProperties {
        public string current { get; set; }
        public List<string> availableoptions { get; set; }
        public override PropertyKind kind => PropertyKind.discrete;

        public CategoricDetectorProperty() {
            current = string.Empty;
            availableoptions = new List<string>();
        }

        public CategoricDetectorProperty(string descriptor, int propertycode, string value, List<string> options) {
            this.current = value;
            this.descriptor = descriptor;
            this.propertycode = propertycode;
            this.availableoptions = options;
        }
    }


    public class DetectorEquipment : IEquipment {
        public List<DetectorEquipmentProperties> DetectorProperties = new();
        public string Detectorname { get; set; }

        public double Framerate = 20;

        [JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonIgnore]
        private const string DetectorPropertiesKey = "detectorproperties";// Expected schema definition

        public DetectorEquipment(string name) {
            Detectorname = name;
        }

        public DetectorEquipment(string name, List<DetectorEquipmentProperties> detectorProperties) {
            Detectorname = name;
            DetectorProperties = detectorProperties;
        }

        public DetectorEquipment(DetectorEquipment detectorEquipment) {
            //Deep copies the detector equipment
            Detectorname = detectorEquipment.Detectorname;
            DetectorProperties = new();
            IsEnabled = detectorEquipment.IsEnabled;
            foreach (DetectorEquipmentProperties property in detectorEquipment.DetectorProperties) {
                switch (property.kind) {
                    case PropertyKind.numeric:
                        if (property is NumericDetectorProperty num_prop) {
                            DetectorProperties.Add(new NumericDetectorProperty(num_prop.descriptor, num_prop.propertycode, num_prop.value));
                        }
                        break;
                    case PropertyKind.discrete:
                        if (property is CategoricDetectorProperty dis_prop) {
                            DetectorProperties.Add(new CategoricDetectorProperty(dis_prop.descriptor, dis_prop.propertycode, dis_prop.current, dis_prop.availableoptions));
                        }
                        break;

                }
            }
        }

        public DetectorEquipment(string name, JToken equipment) {
            Detectorname = name;

            if (equipment[DetectorPropertiesKey] is not JArray detectorPropertiesArray) {
                throw new JsonException("Expected 'detectorproperties' to be a JArray.");
            }

            DetectorProperties = new List<DetectorEquipmentProperties>();

            foreach (JToken detectorToken in detectorPropertiesArray) {
                var discriminator = detectorToken.ToObject<DetectorPropertyDiscriminator>();

                var typeMapping = new Dictionary<PropertyKind, Type> {
                    { PropertyKind.numeric, typeof(NumericDetectorProperty) },
                    { PropertyKind.discrete, typeof(CategoricDetectorProperty) }
                };
                if (discriminator != null) {
                    if (!typeMapping.TryGetValue(discriminator.kind, out var targetType))
                        throw new JsonSerializationException($"Unknown kind: {discriminator.kind}");


                    if (detectorToken.ToObject(targetType) is DetectorEquipmentProperties property) {
                        if (property != null) {
                            DetectorProperties.Add(property);
                        }
                    }
                }
                else {
                    throw new NullReferenceException("Could not get the discriminator value for detector");
                }
            }
        }

        public DetectorEquipment(string name, System.Text.Json.JsonElement equipmentArray) {
            Detectorname = name;

            if (equipmentArray.ValueKind != System.Text.Json.JsonValueKind.Array) {
                throw new System.Text.Json.JsonException("Expected 'detectorproperties' to be a JsonArray.");
            }

            DetectorProperties = new List<DetectorEquipmentProperties>();

            foreach (var detectorElement in equipmentArray.EnumerateArray()) {
                var kindStr = detectorElement.GetProperty("kind").GetString();
                var descriptor = detectorElement.GetProperty("descriptor").GetString() ?? "";
                var propertycode = detectorElement.GetProperty("propertycode").GetInt32();
                
                if (kindStr == "numeric") {
                    var value = detectorElement.GetProperty("value").GetDouble();
                    DetectorProperties.Add(new NumericDetectorProperty(descriptor, propertycode, value));
                }
                else if (kindStr == "discrete") {
                    var current = detectorElement.GetProperty("current").GetString() ?? "";
                    var availableoptions = new List<string>();
                    foreach(var opt in detectorElement.GetProperty("availableoptions").EnumerateArray()) {
                        availableoptions.Add(opt.GetString() ?? "");
                    }
                    DetectorProperties.Add(new CategoricDetectorProperty(descriptor, propertycode, current, availableoptions));
                }
                else {
                    throw new System.Text.Json.JsonException($"Unknown kind: {kindStr}");
                }
            }
        }

        public void SetValueByName(string name, string value, double numeric_value) {
            foreach (DetectorEquipmentProperties detectorEquipmentProperties in DetectorProperties) {
                if (detectorEquipmentProperties is NumericDetectorProperty numProperty && detectorEquipmentProperties.descriptor == name) {
                    numProperty.value = numeric_value;
                    return;
                }
                if (detectorEquipmentProperties is CategoricDetectorProperty catProperty && detectorEquipmentProperties.descriptor == name) {
                    catProperty.current = value;
                    return;
                }
            }
        }

        public DetectorEquipmentProperties GetPropertyByName(string name) {
            foreach (DetectorEquipmentProperties detectorEquipmentProperties in DetectorProperties) {
                if (detectorEquipmentProperties is NumericDetectorProperty numProperty && detectorEquipmentProperties.descriptor == name) {
                    return numProperty;
                }
                if (detectorEquipmentProperties is CategoricDetectorProperty catProperty && detectorEquipmentProperties.descriptor == name) {
                    return catProperty;
                }
            }
            throw new ArgumentException($"Could not get property for name: {name}");
        }


        [JsonIgnore]
        public string EquipmentName { get; set; } = String.Empty;

        public JObject Serialize() {
            var settings = new JsonSerializerSettings {
                ContractResolver = new PrivateContractResolver(),
            };

            return JObject.FromObject(this, JsonSerializer.Create(settings));
        }
    }
}
