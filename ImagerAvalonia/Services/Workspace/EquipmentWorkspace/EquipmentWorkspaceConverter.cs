using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace ImagerAvalonia.Services.Workspace
{
    public class EquipmentWorkspaceConverter : JsonConverter<EquipmentWorkspace>
    {
        public override void WriteJson(JsonWriter writer, EquipmentWorkspace? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();



            writer.WritePropertyName(nameof(EquipmentWorkspace.AvailableSources));
            serializer.Serialize(writer, value.AvailableSources);

            writer.WritePropertyName(nameof(EquipmentWorkspace.AvailableFilterWheels));
            serializer.Serialize(writer, value.AvailableFilterWheels);

            writer.WritePropertyName(nameof(EquipmentWorkspace.AvailableRobots));
            serializer.Serialize(writer, value.AvailableRobots);

            writer.WritePropertyName(nameof(EquipmentWorkspace.AvailableDetectors));
            serializer.Serialize(writer, value.AvailableDetectors);

            writer.WriteEndObject();
        }

        public override EquipmentWorkspace? ReadJson(
            JsonReader reader, Type objectType, EquipmentWorkspace? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);

            var workspace = new EquipmentWorkspace
            {
                DefaultAcquisition = jo[nameof(EquipmentWorkspace.DefaultAcquisition)]?.ToObject<DefinedDetection>(serializer),
                NumAcquisition = jo[nameof(EquipmentWorkspace.NumAcquisition)]?.Value<int>() ?? 1
            };

            var sources = jo[nameof(EquipmentWorkspace.AvailableSources)]?.ToObject<List<Source>>(serializer)
                ?? new List<Source>();
            var filterWheels = jo[nameof(EquipmentWorkspace.AvailableFilterWheels)]?.ToObject<List<MovableComponentModel>>(serializer)
                ?? new List<MovableComponentModel>();
            var robots = jo[nameof(EquipmentWorkspace.AvailableRobots)]?.ToObject<List<RobotModel>>(serializer)
                ?? new List<RobotModel>();
            var detectors = jo[nameof(EquipmentWorkspace.AvailableDetectors)]?.ToObject<List<DetectorEquipmentModel>>(serializer)
                ?? new List<DetectorEquipmentModel>();

            // Private setters — bypass via reflection since there's no public
            // way in without wiring up a full ImagerWorkspace for Initialize().
            SetPrivateSet(workspace, nameof(EquipmentWorkspace.AvailableSources), sources);
            SetPrivateSet(workspace, nameof(EquipmentWorkspace.AvailableFilterWheels), filterWheels);
            SetPrivateSet(workspace, nameof(EquipmentWorkspace.AvailableRobots), robots);
            SetPrivateSet(workspace, nameof(EquipmentWorkspace.AvailableDetectors), detectors);

            return workspace;
        }

        private static void SetPrivateSet<T>(EquipmentWorkspace workspace, string propertyName, T value)
        {
            var prop = typeof(EquipmentWorkspace).GetProperty(propertyName)
                ?? throw new JsonSerializationException($"Property '{propertyName}' not found on EquipmentWorkspace.");

            prop.SetValue(workspace, value);
        }
    }


    public static class FullEquipmentStateSerializer
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new LowercaseNamingStrategy()
            },
            Converters = new List<JsonConverter>
        {
            // measurement-side (already existed)
            new MeasurementElementConverter(),
            new RobotProgramArgumentConverter(),
            new RobotProgramParametersConverter(),
            new DetectionParamsDictionaryConverter(),

            // equipment-side
            new EquipmentWorkspaceConverter(),
            new SourceFullConverter(),
            new MovableComponentFullConverter(),
            new SelectedMovableComponentConverter(),
            new DetectorEquipmentPropertiesConverter(),
            new ProgramArgumentConverter(),
        }
        };

        public static FullEquipmentState Deserialize(string json)
        {
            var jo = JObject.Parse(json);
            var serializer = JsonSerializer.Create(Settings);

            var state = new FullEquipmentState
            {
                ApiVersion = jo.GetValue(nameof(FullEquipmentState.ApiVersion), StringComparison.OrdinalIgnoreCase)?.ToString() ?? "2.0",
                CurrentEquipment = jo.GetValue(nameof(FullEquipmentState.CurrentEquipment), StringComparison.OrdinalIgnoreCase)
                    ?.ToObject<EquipmentWorkspace>(serializer)
            };

           
            var smartProgramSerializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Converters = { new InputParameterConverter() }
            });
            var smartProgramsToken = jo.GetValue(nameof(FullEquipmentState.SmartPrograms), StringComparison.OrdinalIgnoreCase) as JArray;
            if (smartProgramsToken != null)
            {
                state.SmartPrograms = smartProgramsToken.ToObject<List<SmartProgramModel>>(smartProgramSerializer) ?? new List<SmartProgramModel>();
            }

            var programToken = jo.GetValue(nameof(FullEquipmentState.CurrentProgram), StringComparison.OrdinalIgnoreCase) as JObject;
            if (programToken != null)
            {
                var measurementSerializer = JsonSerializer.Create(MeasurementSerializer.SettingsForStorage);
                var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.SettingsIncludingDisabled);

                var programElementToken = programToken.GetValue(nameof(MeasurementProgram.Program), StringComparison.OrdinalIgnoreCase)
                    ?? throw new JsonSerializationException("Missing 'Program' in CurrentProgram JSON.");
                var programElement = programElementToken.ToObject<MeasurementElementBase>(measurementSerializer)
                    ?? throw new JsonSerializationException("Failed to deserialize 'Program'.");

                var detectionsToken = programToken.GetValue(nameof(MeasurementProgram.Detections), StringComparison.OrdinalIgnoreCase);
                var detections = detectionsToken?.ToObject<Dictionary<string, DetectionParams>>(equipmentSerializer)
                    ?? new Dictionary<string, DetectionParams>();

                state.CurrentProgram = new MeasurementProgram(programElement, detections)
                {
                    ApiVersion = programToken.GetValue(nameof(MeasurementProgram.ApiVersion), StringComparison.OrdinalIgnoreCase)?.ToString() ?? "2.0"
                };
            }

            return state;
        }

        public static string Serialize(FullEquipmentState state)
        {
            var serializer = JsonSerializer.Create(Settings);
            var measurementSerializer = JsonSerializer.Create(MeasurementSerializer.SettingsForStorage);
            var equipmentSerializer = JsonSerializer.Create(DetectionEquipmentSerializer.SettingsIncludingDisabled);

            var jo = new JObject
            {
                [ConverterHelpers.ResolvePropertyName(serializer, nameof(FullEquipmentState.ApiVersion))] = state.ApiVersion,

                [ConverterHelpers.ResolvePropertyName(serializer, nameof(FullEquipmentState.CurrentEquipment))]
                    = state.CurrentEquipment is null ? null : JObject.FromObject(state.CurrentEquipment, serializer),

                [ConverterHelpers.ResolvePropertyName(serializer, nameof(FullEquipmentState.CurrentProgram))]
                    = state.CurrentProgram is null ? null : new JObject
                    {
                        [ConverterHelpers.ResolvePropertyName(serializer, nameof(MeasurementProgram.Program))]
                            = JObject.FromObject(state.CurrentProgram.Program, measurementSerializer),

                        [ConverterHelpers.ResolvePropertyName(serializer, nameof(MeasurementProgram.Detections))]
                            = JObject.FromObject(state.CurrentProgram.Detections, equipmentSerializer),

                        [ConverterHelpers.ResolvePropertyName(serializer, nameof(MeasurementProgram.ApiVersion))]
                            = state.CurrentProgram.ApiVersion
                    },

                [ConverterHelpers.ResolvePropertyName(serializer, nameof(FullEquipmentState.SmartPrograms))]
                    = JArray.FromObject(state.SmartPrograms, JsonSerializer.Create(new JsonSerializerSettings
                    {
                        Converters = { new InputParameterConverter() }
                    }))
            };

            return jo.ToString(Formatting.Indented);
        }
    }


    public class FullEquipmentState
    {
        public MeasurementProgram CurrentProgram { get; set; }
        public EquipmentWorkspace CurrentEquipment { get; set; }
        public string ApiVersion { get; set; } = "2.0";
        public List<SmartProgramModel> SmartPrograms { get; set; } = new();


        [JsonConstructor]
        public FullEquipmentState() { }

        public JObject Serialize()
        {

            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Converters = { new EquipmentWorkspaceConverter() }
            });
            JObject serializedProgram = new JObject()
            {
                {"program", JObject.FromObject(CurrentProgram.Program , Newtonsoft.Json.JsonSerializer.Create(MeasurementSerializer.Settings))},
                {"detections", JObject.FromObject(CurrentProgram.Detections , Newtonsoft.Json.JsonSerializer.Create(DetectionEquipmentSerializer.Settings))},
                {"apiversion", CurrentProgram.ApiVersion}
            };
            JObject serialized = new JObject
            {
                { "currentprogram", serializedProgram },
                { "currentequipment", JToken.FromObject(CurrentEquipment, serializer) },
            };
            return serialized;
        }
    }
}
