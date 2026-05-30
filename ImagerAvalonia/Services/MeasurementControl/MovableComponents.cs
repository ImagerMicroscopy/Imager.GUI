using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.Services.MeasurementControl
{

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
        public override MovableComponentType Type => MovableComponentType.continuousmovablecomponent;

        public ContinuousMovableComponentPartProperties(string name, double minvalue, double maxvalue, double incrementval, double? desiredsetting)
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
            foreach (var component in movablecomponents)
            {
                if (component.Name == componentname)
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
            if (Enum.TryParse<MovableSettingType>(type, out var setting_type))
            {

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
            else
            {
                throw new Exception("Unknown Movable Setting Type");
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
        public MovableComponentPart(string componentname, List<string> possiblesettings, string type, string maxvalue, string minvalue, string increment, string desiredsetting)
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

        public MovableComponentPart(System.Text.Json.JsonElement element)
        {
            var componentname = element.GetProperty("componentname").GetString() ?? "";
            var type = element.GetProperty("type").GetString() ?? "";
            
            Name = componentname;
            if (!Enum.TryParse<MovableComponentType>(type, out var component_type))
            {
                throw new Exception($"Invalid component type encountered when parsing movablecomponent {componentname}");
            }

            switch (component_type)
            {
                case MovableComponentType.discretemovablecomponent:
                    var possiblesettings = new List<string>();
                    var settingsArr = element.GetProperty("possiblesettings");
                    foreach (var s in settingsArr.EnumerateArray()) possiblesettings.Add(s.GetString() ?? "");
                    var dsetting = element.TryGetProperty("desiredsetting", out var dsn) ? dsn.GetString() : null;
                    movablecomponent = new DiscreteMovableComponentPartProperties(componentname, possiblesettings, dsetting);
                    break;
                case MovableComponentType.continuousmovablecomponent:
                    var minvalue = element.GetProperty("minvalue").GetDouble();
                    var maxvalue = element.GetProperty("maxvalue").GetDouble();
                    var increment = element.GetProperty("increment").GetDouble();
                    double? csetting = null;
                    if (element.TryGetProperty("desiredsetting", out var csn) && csn.ValueKind == System.Text.Json.JsonValueKind.Number) {
                        csetting = csn.GetDouble();
                    }
                    movablecomponent = new ContinuousMovableComponentPartProperties(componentname, minvalue, maxvalue, increment, csetting);
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

    public enum MovableComponentType
    {
        discretemovablecomponent,
        continuousmovablecomponent
    }

    public enum MovableSettingType
    {
        discretemovablesetting,
        continuousmovablesetting,
    }
}
