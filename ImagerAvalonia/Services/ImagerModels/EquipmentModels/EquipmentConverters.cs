using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ImagerAvalonia.Services
{

    public class LowercaseNamingStrategy : NamingStrategy
    {
        protected override string ResolvePropertyName(string name)
            => name.ToLowerInvariant();
    }

    public class RoundingDoubleJsonConverter : JsonConverter
    {
        private const int Digits = 6;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(double) || objectType == typeof(double?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            return Convert.ToDouble(reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            double d = Convert.ToDouble(value);
            writer.WriteValue(Math.Round(d, Digits));
        }
    }
    public class DetectorEquipmentPropertiesConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(DetectorEquipmentProperties).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);

            string kind = jo["kind"]?.Value<string>();

            DetectorEquipmentProperties target = kind switch
            {
                "numeric" => new NumericDetectorProperty(),
                "discrete" => new CategoricDetectorProperty(),
                _ => throw new JsonSerializationException($"Unknown kind '{kind}'")
            };

            serializer.Populate(jo.CreateReader(), target);
            return target;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            switch (value)
            {
                case NumericDetectorProperty n:
                    writer.WritePropertyName("descriptor");
                    writer.WriteValue(n.descriptor);

                    writer.WritePropertyName("propertycode");
                    writer.WriteValue(n.propertycode);

                    writer.WritePropertyName("value");
                    serializer.Serialize(writer, n.value);

                    writer.WritePropertyName("kind");
                    writer.WriteValue("numeric");
                    break;

                case CategoricDetectorProperty c:
                    writer.WritePropertyName("descriptor");
                    writer.WriteValue(c.descriptor);

                    writer.WritePropertyName("propertycode");
                    writer.WriteValue(c.propertycode);

                    writer.WritePropertyName("current");
                    writer.WriteValue(c.current);

                    writer.WritePropertyName("availableoptions");
                    serializer.Serialize(writer, c.availableoptions);

                    writer.WritePropertyName("kind");
                    writer.WriteValue("discrete");
                    break;
            }

            writer.WriteEndObject();
        }
    }

    public interface IEnableGated
    {
        bool IsEnabled { get; }
    }



    public class EnableGatedListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => typeof(System.Collections.IEnumerable).IsAssignableFrom(objectType)
               && objectType.IsGenericType
               && typeof(IEnableGated).IsAssignableFrom(objectType.GetGenericArguments()[0]);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in (System.Collections.IEnumerable)value!)
            {
                if (item is IEnableGated gated && !gated.IsEnabled)
                    continue; // skip disabled items entirely

                serializer.Serialize(writer, item);
            }
            writer.WriteEndArray();
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;


            var elementType = objectType.GetGenericArguments()[0];

            JArray array = JArray.Load(reader);

            var list = (System.Collections.IList)Activator.CreateInstance(objectType)!;
            foreach (var token in array)
            {
                var item = token.ToObject(elementType, serializer);
                list.Add(item);
            }

            return list;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
    }

    public class ProgramArgumentConverter : JsonConverter
    {
        private const string TypePropertyName = "type";

        private static readonly JsonSerializerSettings InnerSettings = new()
        {
            ContractResolver = DetectionEquipmentSerializer.Settings.ContractResolver,
            Converters = DetectionEquipmentSerializer.Settings.Converters
                .Where(c => c is not ProgramArgumentConverter)
                .ToList()
        };

        public override bool CanConvert(Type objectType)
            => typeof(ProgramArgumentsSettingsBase).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            var typeString = jo[TypePropertyName]?.ToString();

            if (!Enum.TryParse(typeString, ignoreCase: true, out RobotProgramArgumentType type))
                throw new JsonSerializationException($"Unknown argument type: {typeString}");

            ProgramArgumentsSettingsBase target = type switch
            {
                RobotProgramArgumentType.discreteargument => new DiscreterProgramArgumentSetting(),
                RobotProgramArgumentType.continuousargument => new ContinuousProgramArgumentSetting(),
                _ => throw new NotSupportedException(type.ToString())
            };

            var nestedSerializer = JsonSerializer.CreateDefault(InnerSettings);
            nestedSerializer.Populate(jo.CreateReader(), target);
            return target;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not ProgramArgumentsSettingsBase arg)
                throw new JsonSerializationException("Expected a ProgramArgumentsSettingsBase instance.");

            JObject jo = JObject.FromObject(value, JsonSerializer.CreateDefault(InnerSettings));
            jo[TypePropertyName] = arg.Type.ToString();
            jo.WriteTo(writer);
        }
    }


    public class RobotProgramParametersConverter : JsonConverter<RobotProgramParameters>
    {
        public override void WriteJson(JsonWriter writer, RobotProgramParameters? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("equipmentname");
            writer.WriteValue(value.Robot.EquipmentName);

            writer.WritePropertyName("robotname");
            writer.WriteValue(value.Robot.RobotName);

            writer.WritePropertyName("programcallparameters");
            serializer.Serialize(writer, value.ProgramCallParameters);

            writer.WriteEndObject();
        }

        public override RobotProgramParameters ReadJson(JsonReader reader, Type objectType, RobotProgramParameters? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);

            var result = new RobotProgramParameters
            {
                Robot = new RobotModel
                {
                    EquipmentName = jo["equipmentname"]?.ToString() ?? "",
                    RobotName = jo["robotname"]?.ToString() ?? ""
                }
            };

            if (jo["programcallparameters"] is JToken callParams)
            {
                result.ProgramCallParameters = callParams.ToObject<RobotProgramCallParameters>(serializer)
                    ?? new RobotProgramCallParameters();
            }

            return result;
        }
    }

    // =========================
    // MOVABLE COMPONENT PART PROPERTIES CONVERTER
    // =========================
    // MovableComponentPartProperties is abstract (Discrete/Continuous are the
    // two concrete subclasses), and neither subclass has a parameterless
    // constructor Newtonsoft can use by default — their real constructors
    // require componentname/desiredsetting/etc, and several members
    // (PossibleSettings, MinValue, MaxValue) are [JsonIgnore]d. This
    // converter picks the right subclass from the "type" field and builds
    // it explicitly on both read and write.
    public class MovableComponentPartPropertiesConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => typeof(MovableComponentPartProperties).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);
            var typeToken = jo.GetValue("Type", StringComparison.OrdinalIgnoreCase);

            if (typeToken is null)
                throw new JsonSerializationException("Missing MovableComponentType 'Type' field.");

       
            var type = typeToken.ToObject<MovableComponentType>(serializer);

            var componentName = jo.GetValue("ComponentName", StringComparison.OrdinalIgnoreCase)?.ToString() ?? "";

            switch (type)
            {
                case MovableComponentType.discretemovablecomponent:
                    var desiredDiscrete = jo.GetValue("desiredsetting", StringComparison.OrdinalIgnoreCase)?.ToString() ?? "";
                    return new DiscreteMovableComponentPartProperties(componentName, new List<string>(), desiredDiscrete);

                case MovableComponentType.continuousmovablecomponent:
                    double? desiredContinuous = jo.GetValue("desiredsetting", StringComparison.OrdinalIgnoreCase)?.ToObject<double?>();
                    double increment = jo.GetValue("increment", StringComparison.OrdinalIgnoreCase)?.ToObject<double?>() ?? 0;
                    return new ContinuousMovableComponentPartProperties(componentName, 0, 0, increment, desiredContinuous);

                default:
                    throw new JsonSerializationException($"Unhandled MovableComponentType: {type}");
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            void WriteProp(string name, object? val)
            {
                writer.WritePropertyName(ConverterHelpers.ResolvePropertyName(serializer, name));
                serializer.Serialize(writer, val);
            }

            switch (value)
            {
                case DiscreteMovableComponentPartProperties d:
                    WriteProp("Type", d.Type);   // goes through MovableComponentConverter
                    WriteProp("ComponentName", d.ComponentName);
                    WriteProp("desiredsetting", d.desiredsetting);
                    break;

                case ContinuousMovableComponentPartProperties c:
                    WriteProp("Type", c.Type);   // goes through MovableComponentConverter
                    WriteProp("ComponentName", c.ComponentName);
                    WriteProp("desiredsetting", c.desiredsetting);
                    WriteProp("increment", c.increment);
                    break;

                default:
                    throw new JsonSerializationException($"Unknown component properties type: {value.GetType().Name}");
            }

            writer.WriteEndObject();
        }
    }


    public static class DetectionEquipmentSerializer
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new LowercaseNamingStrategy()
            },
            Converters = new List<JsonConverter>
        {
            new RoundingDoubleJsonConverter(),
            new DetectorEquipmentPropertiesConverter(),
            new EnableGatedListConverter(),
            new SelectedMovableComponentConverter(),
            new ProgramArgumentConverter(),
            new RobotProgramParametersConverter(),
            new MovableComponentPartPropertiesConverter()   // ← must be present

        }
        };
    }


    public class SelectedMovableComponentConverter : JsonConverter<MovableComponentType>
    {
        public override MovableComponentType ReadJson(
            JsonReader reader,
            Type objectType,
            MovableComponentType existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var str = reader.Value?.ToString();

            return str switch
            {
                "continuousmovablesetting" => MovableComponentType.continuousmovablecomponent,
                "discretemovablesetting" => MovableComponentType.discretemovablecomponent,
                _ => Enum.TryParse(str, true, out MovableComponentType result) ? result : default
            };
        }

        public override void WriteJson(
            JsonWriter writer,
            MovableComponentType value,
            JsonSerializer serializer)
        {
            writer.WriteValue(value switch
            {
                MovableComponentType.continuousmovablecomponent => "continuousmovablesetting",
                MovableComponentType.discretemovablecomponent => "discretemovablesetting",
                _ => value.ToString()
            });
        }
    }

    public class SourceFullConverter : JsonConverter<Source>
    {
        public override void WriteJson(JsonWriter writer, Source value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(Source.EquipmentName));
            writer.WriteValue(value.EquipmentName);

            writer.WritePropertyName(nameof(Source.LightSourceName));
            writer.WriteValue(value.LightSourceName);

            writer.WritePropertyName(nameof(Source.LightsourceChannel));
            serializer.Serialize(writer, value.LightsourceChannel);

            writer.WritePropertyName(nameof(Source.LightsourcePower));
            serializer.Serialize(writer, value.LightsourcePower);

            writer.WritePropertyName(nameof(Source.IsEnabled));
            writer.WriteValue(value.IsEnabled);

            writer.WritePropertyName(nameof(Source.allowmultiplechannels));
            writer.WriteValue(value.allowmultiplechannels);

            writer.WritePropertyName(nameof(Source.cancontrolpower));
            writer.WriteValue(value.cancontrolpower);

            writer.WritePropertyName(nameof(Source.AvailableChannels));
            serializer.Serialize(writer, value.AvailableChannels);

            writer.WriteEndObject();
        }

        public override Source ReadJson(JsonReader reader, Type objectType, Source existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var jo = Newtonsoft.Json.Linq.JObject.Load(reader);

            var source = new Source(
                allowmultiplechannels: jo[nameof(Source.allowmultiplechannels)]?.Value<bool>() ?? false,
                cancontrolpower: jo[nameof(Source.cancontrolpower)]?.Value<bool>() ?? false,
                channels: jo[nameof(Source.AvailableChannels)]?.ToObject<List<string>>(),
                name: jo[nameof(Source.LightSourceName)]?.Value<string>()
            );

            source.EquipmentName = jo[nameof(Source.EquipmentName)]?.Value<string>() ?? string.Empty;
            source.IsEnabled = jo[nameof(Source.IsEnabled)]?.Value<bool>() ?? false;
            source.LightsourceChannel = jo[nameof(Source.LightsourceChannel)]?.ToObject<List<string>>() ?? new List<string>();
            source.LightsourcePower = jo[nameof(Source.LightsourcePower)]?.ToObject<List<int>>() ?? new List<int>();

            return source;
        }
    }

    public class MovableComponentFullConverter : JsonConverter<MovableComponentModel>
    {
        // ---- shared helpers for MovableComponentPartProperties ----

        private static void WriteProperties(JsonWriter writer, MovableComponentPartProperties? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(MovableComponentPartProperties.Type));
            serializer.Serialize(writer, value.Type);   // goes through MovableComponentConverter

            writer.WritePropertyName(nameof(MovableComponentPartProperties.ComponentName));
            writer.WriteValue(value.ComponentName);

            switch (value)
            {
                case DiscreteMovableComponentPartProperties d:
                    writer.WritePropertyName(nameof(DiscreteMovableComponentPartProperties.desiredsetting));
                    writer.WriteValue(d.desiredsetting);

                    writer.WritePropertyName(nameof(DiscreteMovableComponentPartProperties.PossibleSettings));
                    serializer.Serialize(writer, d.PossibleSettings);
                    break;

                case ContinuousMovableComponentPartProperties c:
                    writer.WritePropertyName(nameof(ContinuousMovableComponentPartProperties.desiredsetting));
                    writer.WriteValue(c.desiredsetting);

                    writer.WritePropertyName(nameof(ContinuousMovableComponentPartProperties.increment));
                    writer.WriteValue(c.increment);

                    writer.WritePropertyName(nameof(ContinuousMovableComponentPartProperties.MinValue));
                    writer.WriteValue(c.MinValue);

                    writer.WritePropertyName(nameof(ContinuousMovableComponentPartProperties.MaxValue));
                    writer.WriteValue(c.MaxValue);
                    break;

                default:
                    throw new JsonSerializationException($"Unknown component properties type: {value.GetType().Name}");
            }

            writer.WriteEndObject();
        }

        private static MovableComponentPartProperties? ReadProperties(JToken? token, JsonSerializer serializer)
        {
            if (token is null || token.Type == JTokenType.Null)
                return null;

            var jo = (JObject)token;
            var typeToken = jo.GetValue(nameof(MovableComponentPartProperties.Type), StringComparison.OrdinalIgnoreCase);

            if (typeToken is null)
                throw new JsonSerializationException("Missing MovableComponentType 'Type' field.");

            var type = typeToken.ToObject<MovableComponentType>(serializer);
            var componentName = jo.GetValue(nameof(MovableComponentPartProperties.ComponentName), StringComparison.OrdinalIgnoreCase)?.ToString() ?? "";

            switch (type)
            {
                case MovableComponentType.discretemovablecomponent:
                    var desiredDiscrete = jo.GetValue(nameof(DiscreteMovableComponentPartProperties.desiredsetting), StringComparison.OrdinalIgnoreCase)?.ToString() ?? "";
                    var possibleSettings = jo.GetValue(nameof(DiscreteMovableComponentPartProperties.PossibleSettings), StringComparison.OrdinalIgnoreCase)
                        ?.ToObject<List<string>>(serializer) ?? new List<string>();

                    return new DiscreteMovableComponentPartProperties(componentName, possibleSettings, desiredDiscrete);

                case MovableComponentType.continuousmovablecomponent:
                    double? desiredContinuous = jo.GetValue(nameof(ContinuousMovableComponentPartProperties.desiredsetting), StringComparison.OrdinalIgnoreCase)?.ToObject<double?>();
                    double increment = jo.GetValue(nameof(ContinuousMovableComponentPartProperties.increment), StringComparison.OrdinalIgnoreCase)?.ToObject<double?>() ?? 0;
                    double minValue = jo.GetValue(nameof(ContinuousMovableComponentPartProperties.MinValue), StringComparison.OrdinalIgnoreCase)?.ToObject<double?>() ?? 0;
                    double maxValue = jo.GetValue(nameof(ContinuousMovableComponentPartProperties.MaxValue), StringComparison.OrdinalIgnoreCase)?.ToObject<double?>() ?? 0;

                    return new ContinuousMovableComponentPartProperties(componentName, minValue, maxValue, increment, desiredContinuous);

                default:
                    throw new JsonSerializationException($"Unhandled MovableComponentType: {type}");
            }
        }

        // ---- WriteJson ----

        public override void WriteJson(JsonWriter writer, MovableComponentModel? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("equipmentname");
            writer.WriteValue(value.equipmentname);

            // movablecomponents (normally [JsonIgnore]d) — written in full here
            writer.WritePropertyName(nameof(MovableComponentModel.movablecomponents));
            writer.WriteStartArray();
            foreach (var part in value.movablecomponents)
            {
                writer.WriteStartObject();

                writer.WritePropertyName(nameof(MovableComponentPart.Name));
                writer.WriteValue(part.Name);

                writer.WritePropertyName(nameof(MovableComponentPart.EquipmentName));
                writer.WriteValue(part.EquipmentName);

                writer.WritePropertyName(nameof(MovableComponentPart.FilterNames));
                serializer.Serialize(writer, part.FilterNames);

                writer.WritePropertyName(nameof(MovableComponentPart.movablecomponent));
                WriteProperties(writer, part.movablecomponent, serializer);

                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            // movablecomponentsettings
            writer.WritePropertyName(nameof(MovableComponentModel.movablecomponentsettings));
            writer.WriteStartArray();
            foreach (var settings in value.movablecomponentsettings)
            {
                WriteProperties(writer, settings, serializer);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        // ---- ReadJson ----

        public override MovableComponentModel? ReadJson(
            JsonReader reader, Type objectType, MovableComponentModel? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);

            var result = new MovableComponentModel
            {
                equipmentname = jo["equipmentname"]?.Value<string>() ?? string.Empty
            };

            if (jo[nameof(MovableComponentModel.movablecomponents)] is JArray partsArray)
            {
                foreach (var token in partsArray)
                {
                    var partObj = (JObject)token;

                    var part = new MovableComponentPart()
                    {
                        Name = partObj[nameof(MovableComponentPart.Name)]?.Value<string>() ?? string.Empty,
                        EquipmentName = partObj[nameof(MovableComponentPart.EquipmentName)]?.Value<string>() ?? string.Empty,
                        FilterNames = partObj[nameof(MovableComponentPart.FilterNames)]?.ToObject<List<string>>(serializer) ?? new List<string>(),
                        movablecomponent = ReadProperties(partObj[nameof(MovableComponentPart.movablecomponent)], serializer)
                    };

                    result.movablecomponents.Add(part);
                }
            }

            if (jo[nameof(MovableComponentModel.movablecomponentsettings)] is JArray settingsArray)
            {
                foreach (var token in settingsArray)
                {
                    result.movablecomponentsettings.Add(ReadProperties(token, serializer));
                }
            }

            return result;
        }
    }
}