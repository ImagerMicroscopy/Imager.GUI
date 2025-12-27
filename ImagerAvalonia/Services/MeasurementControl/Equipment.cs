using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;


namespace ImagerAvalonia.Services.MeasurementControl
{
    public class PrivateContractResolver : DefaultContractResolver
    {
        protected override System.Collections.Generic.IList<JsonProperty> CreateProperties(
            System.Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);

            foreach (var property in properties)
            {
                property.Writable = true;
                property.Readable = true;
            }

            return properties;
        }
        protected override string ResolvePropertyName(string propertyName)
        {
            return propertyName.ToLower();
        }
    }


    public class MovableComponentConverter : JsonConverter<MovableComponentType>
    {


        public override MovableComponentType ReadJson(JsonReader reader, Type objectType, MovableComponentType existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var stringValue = reader.Value?.ToString();
            MovableComponentType result;
            Enum.TryParse<MovableComponentType>(stringValue, out result);
            return result;
        }


        public override void WriteJson(JsonWriter writer, MovableComponentType value, Newtonsoft.Json.JsonSerializer serializer)
        {
            switch (value)
            {
                case MovableComponentType.continuousmovablecomponent:
                    writer.WriteValue("continuousmovablesetting");
                    break;
                case MovableComponentType.discretemovablecomponent:
                    writer.WriteValue("discretemovablesetting");
                    break;
                default:
                    writer.WriteValue(value.ToString());
                    break;
            }
        }
    }

    public static class AcquisitionSettingsFactory
    {



        public static AcquisitionSettings CopyFromDeserializedModel(AcquisitionSettings defaultSettings, AcquisitionSettingsDeserializationModel modelSettings)
        {
            // Set the detector properties from deserialized model into the model with current available hardware parameters
            defaultSettings.Detector.ForEach(d => d.IsEnabled = false);
            foreach (var detectormodel in modelSettings.Detectors)
            {
                var found_detector = defaultSettings.Detector.Find(x => x.Detectorname == detectormodel.Detectorname);
                if (found_detector != null)
                {
                    found_detector.IsEnabled = true;

                    for (int id = 0; id < found_detector.DetectorProperties.Count(); id++)
                    {
                        var detectorproperty = found_detector.DetectorProperties[id];
                        var property = modelSettings.GetDetectorEquipmentPropertyByName(detectorproperty.descriptor, detectormodel.Detectorname);
                        switch (detectorproperty.kind)
                        {
                            case PropertyKind.numeric:
                                if (detectorproperty is NumericDetectorProperty num_prop && property is NumericDetectorProperty numeric_value)
                                {
                                    found_detector.DetectorProperties[id] = new NumericDetectorProperty(num_prop.descriptor, num_prop.propertycode, numeric_value.value);
                                }
                                break;
                            case PropertyKind.discrete:
                                if (detectorproperty is CategoricDetectorProperty dis_prop && property is CategoricDetectorProperty cat_value)
                                {
                                    found_detector.DetectorProperties[id] = new CategoricDetectorProperty(dis_prop.descriptor, dis_prop.propertycode, cat_value.current, dis_prop.availableoptions);
                                }
                                break;
                        }
                    }
                }
            }
            // Set the movable component  properties from deserialized model into the model with current available hardware parameters

            foreach (var movablecomponentmodel in modelSettings.MovableComponents)
            {
                var found_movablecomponent = defaultSettings.FilterWheels.Find(x => x.equipmentname == movablecomponentmodel.equipmentname);
                if (found_movablecomponent != null)
                {
                    foreach (var movablecomponentpart in movablecomponentmodel.movablecomponentsettings)
                    {
                        switch(movablecomponentpart.movablecomponent.Type)
                        {
                            case MovableComponentType.discretemovablecomponent:
                                if (movablecomponentpart.movablecomponent is DiscreteMovableComponentPartProperties discrete_property) {
                                    found_movablecomponent.SetValueByName(movablecomponentpart.Name, discrete_property.desiredsetting);
                                }
                                break;
                            case MovableComponentType.continuousmovablecomponent:
                                if (movablecomponentpart.movablecomponent is ContinuousMovableComponentPartProperties continuous_property)
                                {
                                    found_movablecomponent.SetValueByName(movablecomponentpart.Name, continuous_property.desiredsetting.ToString());
                                }
                                break;
                        }
                    }
                }
            }
            foreach(var irradiationmodel in modelSettings.Irradiation)
            {
                var found_irradiation = defaultSettings.Sources.Find(x => x.EquipmentName == irradiationmodel.equipmentname);
                if(found_irradiation!=null)
                {
                    for (int id = 0; id < irradiationmodel.lightsourcechannel.Count(); id++)
                    {
                        found_irradiation.LightsourceChannel.Add(irradiationmodel.lightsourcechannel[id]);
                        found_irradiation.LightsourcePower.Add((int)irradiationmodel.lightsourcepower[id]);
                    }
                }
            }





            return defaultSettings;
        }

        public static AcquisitionSettings FromDetectorNames(string name, List<string> detectorNames)
        {
            var settings = new AcquisitionSettings(name);
            settings.Detector = detectorNames.Select(n => new DetectorEquipment(n) { IsEnabled = true }).ToList();
            return settings;
        }

        public static AcquisitionSettings FromComponents(string name,
            List<Source> sources, List<MovableComponent> filterWheels, List<DetectorEquipment> detectors)
        {
            var settings = new AcquisitionSettings(name);
            settings.Sources = sources.Select(x => new Source(x)).ToList();
            settings.FilterWheels = filterWheels.Select(x => new MovableComponent(x)).ToList();
            settings.Detector = detectors.Select(x => new DetectorEquipment(x)).ToList();

            foreach (var detector in settings.Detector)
            {
                if (detector.IsEnabled)
                {
                    settings.acqDetPairs.Add(new AcqDetPair(settings, detector.Detectorname));
                }
            }

            return settings;
        }

        public static AcquisitionSettings CloneWithName(string name, AcquisitionSettings original)
        {
            var settings = new AcquisitionSettings(name);
            settings.Sources = original.Sources.Select(x => new Source(x)).ToList();
            settings.FilterWheels = original.FilterWheels.Select(x => new MovableComponent(x)).ToList();
            settings.Detector = original.Detector.Select(x => new DetectorEquipment(x)).ToList();
            return settings;
        }

        public static AcquisitionSettings FromName(string name)
        {
            return new AcquisitionSettings(name);   
        }
    }



    public partial class AcquisitionSettings : ObservableObject
    {

        public int AcquisitionSettingsID;

        public List<Source> Sources;

        public List<MovableComponent> FilterWheels;

        public List<DetectorEquipment> Detector;

        [ObservableProperty] private string _name;

        public List<AcqDetPair> acqDetPairs = new();

        public List<Stage> Stages = new();

        public AcquisitionSettings(string name)
        {
            Name = name;
            Sources = new();
            FilterWheels = new();
            Detector = new();
        }


        public JObject SerializeAcquisition()
        {

            JObject serialized_acquisition_settings = new JObject();
            List<DetectorEquipment> enabled_detectors = new List<DetectorEquipment> { };
            foreach (DetectorEquipment d in Detector)
            {
                if (d.IsEnabled)
                {
                    enabled_detectors.Add(d);
                }
            }

            List<Source> enabled_sources = new List<Source> { };
            foreach (Source s in Sources)
            {
                if(s.LightsourceChannel.Count>0)
                {
                    enabled_sources.Add(s);
                }

            }



            serialized_acquisition_settings["detectors"] = JArray.FromObject(enabled_detectors.Select(x => x.Serialize()).ToList());
            serialized_acquisition_settings["movablecomponents"] = JArray.FromObject(FilterWheels.Select(x => x.Serialize()).ToList());
            serialized_acquisition_settings["irradiation"] = JArray.FromObject(enabled_sources.Select(x => x.Serialize()).ToList());

            //JObject final_acquisition = new JObject();
            //final_acquisition[Name] = serialized_acquisition_settings;
            return serialized_acquisition_settings;
        }
    }
    

    public class Equipment
    {
        public List<MovableComponentPart> availablemovablecomponents { get; set; } = new();
        public List<Source> availablelightsources { get; set; } = new();
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








    public partial class Source :  IEquipment
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
            this.LightsourcePower = old_source.LightsourcePower.Select(x =>x).ToList();
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




    public enum MovableComponentType
    {
        discretemovablecomponent,
        continuousmovablecomponent
    }

    public enum MovableSettingType
    {
        discretemovablesetting,
        continuousmovablesetting,
        nosettingavailable
    }

    public abstract class MovableComponentPartProperties
    {
        [JsonConverter(typeof(MovableComponentConverter))]
        public abstract MovableComponentType Type { get; }

        public string ComponentName { get; set; } = String.Empty;
    }

    public class ContinuousMovableComponentPartProperties : MovableComponentPartProperties
    {
        [JsonIgnore]
        public double MinValue { get; set; }

        [JsonIgnore]
        public double MaxValue { get; set; }

        public double? desiredsetting;

        public double increment;

        [JsonConverter(typeof(MovableComponentConverter))]
        public override MovableComponentType Type  => MovableComponentType.continuousmovablecomponent;

        public ContinuousMovableComponentPartProperties(string name, double minvalue, double maxvalue, double incrementval,double? desiredsetting)
        {
            ComponentName = name;
            MinValue = minvalue;
            MaxValue = maxvalue;
            if (desiredsetting is not null)
            {
                this.desiredsetting = desiredsetting;
            }
            else
            {
                this.desiredsetting = minvalue;
            }
            increment = incrementval;
        }
    }

    public class DiscreteMovableComponentPartProperties : MovableComponentPartProperties
    {
        [JsonIgnore]
        public List<string> PossibleSettings { get; set; }

        public string desiredsetting { get; set; }

        [JsonConverter(typeof(MovableComponentConverter))]
        public override MovableComponentType Type => MovableComponentType.discretemovablecomponent;

        public DiscreteMovableComponentPartProperties(string name, List<string> settings, string? desiredsetting) 
        {
            ComponentName = name;
            PossibleSettings = settings;
            if (desiredsetting is null)
            {
                if (settings.Count > 0)
                {
                    this.desiredsetting = settings[0];
                }
                else
                {
                    throw new Exception($"Settings for {name} contain no elements.");
                }
            }
            else
            {
                this.desiredsetting = desiredsetting;
            }
        }
    }

    public class MovableComponent
    {
        [JsonIgnore]
        public List<MovableComponentPart> movablecomponents { get; set; } = new();

        public List<MovableComponentPartProperties?> movablecomponentsettings { get; set; } = new();
        public string equipmentname;

        public MovableComponent(List<MovableComponentPart> movablecomponentsettings, string name)
        {
            this.movablecomponents = movablecomponentsettings;
            this.equipmentname = name;
        }


        public MovableComponent(MovableComponent movablecomponent)
        {
            this.movablecomponents = movablecomponent.movablecomponents.Select(x => new MovableComponentPart(x)).ToList();

            this.equipmentname = movablecomponent.equipmentname;

            

        }
        public void SetValueByName(string componentname, string? value)
        {
            foreach(var component in movablecomponents)
            {
                if(component.Name == componentname)
                {
                    if (value != null)
                    {
                        if (component.movablecomponent is ContinuousMovableComponentPartProperties continuousProp)
                        {
                            continuousProp.desiredsetting = Convert.ToDouble(value);
                        }
                        if (component.movablecomponent is DiscreteMovableComponentPartProperties discreteProp)
                        {
                            discreteProp.desiredsetting = value;
                        }
                    }
                }
            }
        }

        public JObject Serialize()
        {
            movablecomponentsettings = movablecomponents.Select(x => x.movablecomponent).ToList();

            JObject movableSettings = new();

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new PrivateContractResolver(),
            };
            return JObject.FromObject(this, JsonSerializer.Create(settings));
        }
    }
    public partial class MovableSettingPart : IEquipment
    {
        public MovableComponentPartProperties? movablecomponent { get; set; }
        public string Name;
        public string EquipmentName { get; set; } = String.Empty;



        [JsonConstructor]
        public MovableSettingPart(string componentname, string type, string increment, string desiredsetting)
        {
            Name = componentname;
            if (!Enum.TryParse<MovableSettingType>(type, out var setting_type))
            {
                setting_type = MovableSettingType.nosettingavailable;
            }
            switch (setting_type)
            {
                case MovableSettingType.discretemovablesetting:
                    movablecomponent = new DiscreteMovableComponentPartProperties(componentname, new List<string>(), desiredsetting);
                    return;
                case MovableSettingType.continuousmovablesetting:

                    movablecomponent = new ContinuousMovableComponentPartProperties(componentname, 0, 0, Convert.ToDouble(increment),
                            desiredsetting == null ? (double?)null : double.Parse(desiredsetting));
                    return;

            }
        }

        public JObject Serialize()
        {
            throw new NotImplementedException();
        }
    }



    public partial class MovableComponentPart : IEquipment
    {
        public MovableComponentPartProperties? movablecomponent { get; set; } 
        public string Name;
        public string EquipmentName { get; set; } = String.Empty;
        public List<string> FilterNames = new();

        [JsonConstructor]
        public MovableComponentPart(string componentname, List<string> possiblesettings, string type,string maxvalue, string minvalue, string increment, string desiredsetting )
        {
            Name = componentname;
            if (!Enum.TryParse<MovableComponentType>(type, out var component_type))
            {
                throw new Exception($"Invalid component type encountered when parsing movablecomponent {componentname}");
            }

            switch (component_type)
            {
                case MovableComponentType.discretemovablecomponent:
                    movablecomponent = new DiscreteMovableComponentPartProperties(componentname, possiblesettings, desiredsetting);
                    break;
                case MovableComponentType.continuousmovablecomponent:

                    movablecomponent = new ContinuousMovableComponentPartProperties(componentname, Convert.ToDouble(minvalue), Convert.ToDouble(maxvalue), Convert.ToDouble(increment),
                            desiredsetting == null ? (double?)null : double.Parse(desiredsetting));
                    break;
            }
        }

        public MovableComponentPart(MovableComponentPart src)
        {
            FilterNames = src.FilterNames;
            EquipmentName = src.EquipmentName;
            Name = src.Name;


            movablecomponent = src.movablecomponent switch
            {
                DiscreteMovableComponentPartProperties d => new DiscreteMovableComponentPartProperties(d.ComponentName, new List<string>(d.PossibleSettings), d.desiredsetting),

                ContinuousMovableComponentPartProperties c => new ContinuousMovableComponentPartProperties(c.ComponentName, c.MinValue, c.MaxValue, c.increment, c.desiredsetting),

                _ => throw new NotSupportedException("Unknown component type.")
            };
        }
        public JObject Serialize()
        {
            throw new NotImplementedException();
        }

    }



    public partial class Stages 
    {
        public List<Stage> MotorizedStages = new List<Stage>();
        public Stages() { }

    }

    public partial class Stage : IEquipment
    {
        public string Name;

        public string EquipmentName { get; set; }

        public bool IsEnabled;

        public Stage(string equipmentName, string name)
        {
            Name = name;
            EquipmentName = equipmentName;
            IsEnabled = false;
        }

        public JObject Serialize()
        {
            throw new NotImplementedException("Stage does not support serialization");
        }
    }




#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    public class AcqDetPair
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        private AcquisitionSettings acqNameSource;

        public string acqName
        {
            get => acqNameSource.Name;
            set => acqNameSource.Name = value;
        }

        public string detName;

        public AcqDetPair(AcquisitionSettings acqName, string detName)
        {
            this.acqNameSource = acqName;
            this.detName = detName;
        }

        public AcqDetPair(string acqName, string detName)
        {
            acqNameSource = new AcquisitionSettings(acqName);
            this.detName = detName;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is AcqDetPair other)
            {
                return (other.acqName == this.acqName && other.detName == this.detName);
            }
            return base.Equals(obj);
        }
    }


    public enum PropertyKind
    {
        numeric,
        discrete
    }

    public class DetectorPropertyDiscriminator
    {
        public PropertyKind kind { get; set; }
    }

    public abstract class DetectorEquipmentProperties
    {
        public virtual string descriptor { get; set; } = String.Empty;
        public virtual int propertycode { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public abstract PropertyKind kind { get; }
    }


    public class NumericDetectorProperty : DetectorEquipmentProperties
    {
        [JsonConverter(typeof(RoundingDoubleConverter), 6)]
        public double value { get; set; }

        public override string descriptor { get; set; }
        public override int propertycode { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public override PropertyKind kind => PropertyKind.numeric;


        public NumericDetectorProperty(string descriptor, int propertycode, double value)
        {

            this.value = value;
            this.descriptor = descriptor;
            this.propertycode = propertycode;

        }
    }


    public class CategoricDetectorProperty : DetectorEquipmentProperties
    {
        public string current { get; set; }
        public override string descriptor { get; set; }
        public override int propertycode { get; set; }
        public List<string> availableoptions { get; set; }
        public override PropertyKind kind => PropertyKind.discrete;

       

        public CategoricDetectorProperty(string descriptor, int propertycode, string value, List<string> options)
        {
            this.current = value;
            this.descriptor = descriptor;
            this.propertycode = propertycode;
            this.availableoptions = options;
        }
    }


    public partial class DetectorEquipment : IEquipment
    {

        public List<DetectorEquipmentProperties> DetectorProperties = new();

        public string Detectorname { get; set; }

        public double Framerate = 20;

        [JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonIgnore]
        private const string DetectorPropertiesKey = "detectorproperties";// Expected schema definition


        public DetectorEquipment(string name)
        {
            Detectorname = name;
        }

        public DetectorEquipment(string name, List<DetectorEquipmentProperties> detectorProperties)
        {
            Detectorname = name;
            DetectorProperties = detectorProperties;
        }

        public DetectorEquipment(DetectorEquipment detectorEquipment)
        {
            //Deep copies the detector equipment
            Detectorname = detectorEquipment.Detectorname;
            DetectorProperties = new();
            IsEnabled = detectorEquipment.IsEnabled;
            foreach (DetectorEquipmentProperties property in detectorEquipment.DetectorProperties)
            {
                switch (property.kind)
                {
                    case PropertyKind.numeric:
                        if (property is NumericDetectorProperty num_prop)
                        {
                            DetectorProperties.Add(new NumericDetectorProperty(num_prop.descriptor, num_prop.propertycode, num_prop.value));
                        }
                        break;
                    case PropertyKind.discrete:
                        if (property is CategoricDetectorProperty dis_prop)
                        {
                            DetectorProperties.Add(new CategoricDetectorProperty(dis_prop.descriptor,dis_prop.propertycode,dis_prop.current, dis_prop.availableoptions));
                        }
                        break;

                }
            }
        }

        public DetectorEquipment(string name, JToken equipment)
        {
            Detectorname = name;

            if (equipment[DetectorPropertiesKey] is not JArray detectorPropertiesArray)
            {
                throw new JsonException("Expected 'detectorproperties' to be a JArray.");
            }

            DetectorProperties = new List<DetectorEquipmentProperties>();

            foreach (JToken detectorToken in detectorPropertiesArray)
            {
                var discriminator = detectorToken.ToObject<DetectorPropertyDiscriminator>();

                var typeMapping = new Dictionary<PropertyKind, Type>
                {
                    { PropertyKind.numeric, typeof(NumericDetectorProperty) },
                    { PropertyKind.discrete, typeof(CategoricDetectorProperty) }
                };
                if (discriminator != null)
                {
                    if (!typeMapping.TryGetValue(discriminator.kind, out var targetType))
                        throw new JsonSerializationException($"Unknown kind: {discriminator.kind}");


                    if(detectorToken.ToObject(targetType) is DetectorEquipmentProperties property)
                    {
                        if (property != null)
                        {
                            DetectorProperties.Add(property);
                        }
                    }
                }
                else
                {
                    throw new NullReferenceException("Could not get the discriminator value for detector");
                }
            }
        }

        public void SetValueByName(string name, string value, double numeric_value)
        {
            foreach (DetectorEquipmentProperties detectorEquipmentProperties in DetectorProperties)
            {
                if (detectorEquipmentProperties is NumericDetectorProperty numProperty && detectorEquipmentProperties.descriptor == name)
                {
                    numProperty.value = numeric_value;
                    return;
                }
                if (detectorEquipmentProperties is CategoricDetectorProperty catProperty && detectorEquipmentProperties.descriptor == name)
                {
                    catProperty.current = value;
                    return;
                }
            }
        }

        public DetectorEquipmentProperties GetPropertyByName(string name)
        {
            foreach (DetectorEquipmentProperties detectorEquipmentProperties in DetectorProperties)
            {
                if (detectorEquipmentProperties is NumericDetectorProperty numProperty && detectorEquipmentProperties.descriptor == name)
                {
                    return numProperty;
                }
                if (detectorEquipmentProperties is CategoricDetectorProperty catProperty && detectorEquipmentProperties.descriptor == name)
                {
                    return catProperty;
                }
            }
            throw new ArgumentException($"Could not get property for name: {name}");
        }


        [JsonIgnore]
        public string EquipmentName { get; set; } = String.Empty;   

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
