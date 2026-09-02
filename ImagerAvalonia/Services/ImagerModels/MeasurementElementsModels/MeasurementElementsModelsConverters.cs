using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ImagerAvalonia.Services.MeasurementControl
{
    // =========================
    // LOWERCASE NAMING STRATEGY
    // =========================
    public class LowercaseNamingStrategy : NamingStrategy
    {
        protected override string ResolvePropertyName(string name) => name.ToLowerInvariant();
    }

    // =========================
    // SHARED HELPERS
    // =========================
    internal static class ConverterHelpers
    {
        // Builds a JObject property-by-property via reflection instead of JObject.FromObject(value, serializer).
        // This is deliberate: FromObject re-enters the serializer on `value` itself, which re-triggers any
        // converter whose CanConvert now matches subtypes (IsAssignableFrom), causing infinite recursion.
        // Walking properties individually only recurses into genuinely distinct nested objects (Elements,
        // Arguments, etc.), which terminates normally.
        public static JObject BuildJObject(object value, JsonSerializer serializer)
        {
            var jo = new JObject();
            var props = value.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            foreach (var prop in props)
            {
                object? propValue = prop.GetValue(value);
                string propName = ResolvePropertyName(serializer, prop.Name);

                // Do not serialize the Elements collection for measurement elements that cannot have children.
                // This keeps payloads smaller and avoids sending empty/unused child lists.
                if (prop.Name == nameof(MeasurementElementBase.Elements) && value is MeasurementElementBase me && !me.CanHaveChildren())
                    continue;

                if (propValue is null)
                {
                    if (serializer.NullValueHandling == NullValueHandling.Ignore)
                        continue;
                    jo[propName] = JValue.CreateNull();
                }
                else
                {
                    jo[propName] = JToken.FromObject(propValue, serializer);
                }
            }

            return jo;
        }

        public static string ResolvePropertyName(JsonSerializer serializer, string clrPropertyName)
            => serializer.ContractResolver is DefaultContractResolver resolver
                ? resolver.GetResolvedPropertyName(clrPropertyName)
                : clrPropertyName;
    }

    // =========================
    // ROBOT ARGUMENT CONVERTER
    // =========================
    public class RobotProgramArgumentConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => typeof(RobotProgramArgument).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);
            var type = jo.GetValue("RobotProgramArgumentType", StringComparison.OrdinalIgnoreCase)?.ToString()?.ToLowerInvariant();

            RobotProgramArgument target = type switch
            {
                "discrete" => new DiscreteRobotProgramArgument(),
                "continuous" => new ContinuousRobotProgramArgument(),
                _ => throw new JsonSerializationException($"Unknown RobotProgramArgumentType: {type ?? "(missing)"}")
            };

            serializer.Populate(jo.CreateReader(), target);
            target.RobotProgramArgumentType = type!;
            return target;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            ConverterHelpers.BuildJObject(value, serializer).WriteTo(writer);
        }
    }

    // =========================
    // ROBOT PROGRAM PARAMETERS CONVERTER
    // =========================
    // RobotProgramParameters.Robot holds a full Robots reference in memory
    // (robot programs, arguments, etc.) so ToModel() doesn't need to
    // re-derive it from strings, but only its identity - equipmentname/
    // robotname - belongs in a saved program. Without this converter,
    // BuildJObject's default reflection walk serializes the entire nested
    // Robots graph under a "robot" key instead.
    public class RobotProgramParametersConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(RobotProgramParameters);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);

            var result = new RobotProgramParameters
            {
                // Only identity is recoverable from a saved program; the
                // full Robots definition (programs/arguments) is looked up
                // separately against the currently connected equipment.
                Robot = new RobotModel
                {
                    EquipmentName = jo.GetValue("equipmentname", StringComparison.OrdinalIgnoreCase)?.ToString() ?? "",
                    RobotName = jo.GetValue("robotname", StringComparison.OrdinalIgnoreCase)?.ToString() ?? ""
                }
            };

            if (jo.GetValue("programcallparameters", StringComparison.OrdinalIgnoreCase) is JToken callParams)
            {
                result.ProgramCallParameters = callParams.ToObject<RobotProgramCallParameters>(serializer)
                    ?? new RobotProgramCallParameters();
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            var parameters = (RobotProgramParameters)value;

            writer.WriteStartObject();

            writer.WritePropertyName(ConverterHelpers.ResolvePropertyName(serializer, "EquipmentName"));
            writer.WriteValue(parameters.Robot.EquipmentName);

            writer.WritePropertyName(ConverterHelpers.ResolvePropertyName(serializer, "RobotName"));
            writer.WriteValue(parameters.Robot.RobotName);

            writer.WritePropertyName(ConverterHelpers.ResolvePropertyName(serializer, "ProgramCallParameters"));
            serializer.Serialize(writer, parameters.ProgramCallParameters);

            writer.WriteEndObject();
        }
    }

    // =========================
    // MEASUREMENT ELEMENT CONVERTER
    // =========================
    public class MeasurementElementConverter : JsonConverter
    {
        private static readonly (string Key, Type Type, Func<MeasurementElementBase> Factory)[] Registry = new[]
        {
            ("detection",           typeof(DetectionElement),           (Func<MeasurementElementBase>)(() => new DetectionElement())),
            ("dotimes",             typeof(DoTimesElement),             () => new DoTimesElement()),
            ("stageloop",           typeof(StageLoopElement),           () => new StageLoopElement()),
            ("timelapse",           typeof(TimeLapseElement),           () => new TimeLapseElement()),
            ("wait",                typeof(WaitElement),                () => new WaitElement()),
            ("irradiation",         typeof(IrradiationElement),         () => new IrradiationElement()),
            ("relativestageloop",   typeof(RelativeStageLoopElement),   () => new RelativeStageLoopElement()),
            ("executerobotprogram", typeof(ExecuteRobotProgramElement), () => new ExecuteRobotProgramElement()),
            ("updateacquisition",   typeof(UpdateAcquisition),          () => new UpdateAcquisition()),
        };

        private static readonly Dictionary<string, Func<MeasurementElementBase>> FactoriesByKey =
            Registry.ToDictionary(r => r.Key, r => r.Factory, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<Type, string> KeysByType =
            Registry.ToDictionary(r => r.Type, r => r.Key);

        public override bool CanConvert(Type objectType)
            => typeof(MeasurementElementBase).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);
            var type = jo.GetValue("ElementType", StringComparison.OrdinalIgnoreCase)?.ToString()?.ToLowerInvariant();

            if (type is null || !FactoriesByKey.TryGetValue(type, out var factory))
                throw new JsonSerializationException($"Unknown ElementType: {type ?? "(missing)"}");

            MeasurementElementBase target = factory();
            serializer.Populate(jo.CreateReader(), target);
            target.ElementType = type;
            return target;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            if (!KeysByType.TryGetValue(value.GetType(), out var key))
                throw new JsonSerializationException($"No ElementType registered for {value.GetType().Name}");

            var jo = ConverterHelpers.BuildJObject(value, serializer);
            jo[ConverterHelpers.ResolvePropertyName(serializer, nameof(MeasurementElementBase.ElementType))] = key;
            jo.WriteTo(writer);
        }
    }

    public class DetectionParamsDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(Dictionary<string, DetectionParams>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            // DetectionParams nests equipment-owned types (MovableComponent,
            // DetectorEquipment) — delegate to EquipmentSerializer's settings for
            // those rather than pulling MovableComponent* converters into
            // MeasurementSerializer, which shouldn't need to know they exist.
            var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.Settings);

            var result = new Dictionary<string, DetectionParams>();

            if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                foreach (var item in array)
                {
                    var name = item.Value<string>("name")
                        ?? item.Value<string>("Name")
                        ?? throw new JsonSerializationException("Legacy detection entry missing 'name'.");

                    var settingsToken = item["settings"] ?? item["Settings"];
                    var settings = settingsToken?.ToObject<DetectionParams>(equipmentSerializer) ?? new DetectionParams();

                    result[name] = settings;
                }

                return result;
            }

            var jo = JObject.Load(reader);
            foreach (var prop in jo.Properties())
            {
                result[prop.Name] = prop.Value.ToObject<DetectionParams>(equipmentSerializer) ?? new DetectionParams();
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.Settings);
            var dict = (Dictionary<string, DetectionParams>)value;

            writer.WriteStartObject();
            foreach (var kvp in dict)
            {
                writer.WritePropertyName(kvp.Key);
                equipmentSerializer.Serialize(writer, kvp.Value);
            }
            writer.WriteEndObject();
        }
    }

    // Same as DetectionParamsDictionaryConverter but uses
    // DetectionEquipmentSerializer.SettingsIncludingDisabled instead of
    // DetectionEquipmentSerializer.Settings, so a DefinedDetection's disabled
    // detectors are kept (not dropped from the list) when persisted to the
    // .imag project file. Used only by MeasurementSerializer.SettingsForStorage -
    // the measurement backend payload must keep using DetectionParamsDictionaryConverter.
    public class DetectionParamsDictionaryStorageConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(Dictionary<string, DetectionParams>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.SettingsIncludingDisabled);

            var result = new Dictionary<string, DetectionParams>();

            if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                foreach (var item in array)
                {
                    var name = item.Value<string>("name")
                        ?? item.Value<string>("Name")
                        ?? throw new JsonSerializationException("Legacy detection entry missing 'name'.");

                    var settingsToken = item["settings"] ?? item["Settings"];
                    var settings = settingsToken?.ToObject<DetectionParams>(equipmentSerializer) ?? new DetectionParams();

                    result[name] = settings;
                }

                return result;
            }

            var jo = JObject.Load(reader);
            foreach (var prop in jo.Properties())
            {
                result[prop.Name] = prop.Value.ToObject<DetectionParams>(equipmentSerializer) ?? new DetectionParams();
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.SettingsIncludingDisabled);
            var dict = (Dictionary<string, DetectionParams>)value;

            writer.WriteStartObject();
            foreach (var kvp in dict)
            {
                writer.WritePropertyName(kvp.Key);
                equipmentSerializer.Serialize(writer, kvp.Value);
            }
            writer.WriteEndObject();
        }
    }

    // =========================
    // MEASUREMENT SERIALIZER
    // =========================
    public static class MeasurementSerializer
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new LowercaseNamingStrategy()
            },
            Converters = new List<JsonConverter>
            {
                new MeasurementElementConverter(),
                new RobotProgramArgumentConverter(),
                new RobotProgramParametersConverter(),
                new DetectionParamsDictionaryConverter()

            }
        };

        // Same as Settings but with DetectionParamsDictionaryStorageConverter
        // instead of DetectionParamsDictionaryConverter, so disabled detectors
        // survive .imag save/load. Used only by FullEquipmentStateSerializer.
        public static readonly JsonSerializerSettings SettingsForStorage = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new LowercaseNamingStrategy()
            },
            Converters = new List<JsonConverter>
            {
                new MeasurementElementConverter(),
                new RobotProgramArgumentConverter(),
                new RobotProgramParametersConverter(),
                new DetectionParamsDictionaryStorageConverter()

            }
        };

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings)!;
        }

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, Settings);
        }
    }
}