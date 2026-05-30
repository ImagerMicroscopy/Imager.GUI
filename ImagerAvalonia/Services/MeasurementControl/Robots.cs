using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ImagerAvalonia.Services.MeasurementControl
{
    public partial class Robots : IEquipment
    {
        public string robotname { get; set; } = String.Empty;
        public List<RobotPrograms> robotPrograms { get; set; } = new();
        public string EquipmentName { get; set; } = String.Empty;
        public JObject Serialize()
        {
            return new JObject();
        }

        public Robots() {}

        public Robots(System.Text.Json.JsonElement element)
        {
            robotname = element.GetProperty("robotname").GetString() ?? "";
            if (element.TryGetProperty("robotprograms", out var rps) && rps.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var rp in rps.EnumerateArray()) {
                    robotPrograms.Add(new RobotPrograms(rp));
                }
            }
        }
    }

    [JsonConverter(typeof(ProgramArgumentConverter))]
    public abstract class ProgramArgumentsSettingsBase
    {
        public abstract RobotProgramArgumentType Type { get; }
        public string ProgramArgumentName { get; set; } = string.Empty;
    }

    public class DiscreterProgramArgumentSetting : ProgramArgumentsSettingsBase
    {
        public override RobotProgramArgumentType Type => RobotProgramArgumentType.discreteargument;
        public List<string> PermissibleValues { get; set; } = new();
    }

    public class ContinuousProgramArgumentSetting : ProgramArgumentsSettingsBase
    {
        public override RobotProgramArgumentType Type => RobotProgramArgumentType.continuousargument;
        public float Increment { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
    }

    public class RobotPrograms
    {
        public ProgramArguments? programArguments { get; set; }
        public string programname = String.Empty;

        public RobotPrograms() {}

        public RobotPrograms(System.Text.Json.JsonElement element) {
            programname = element.GetProperty("programname").GetString() ?? "";
            var list = new List<ProgramArgumentsSettingsBase>();
            if (element.TryGetProperty("programarguments", out var pas) && pas.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var pa in pas.EnumerateArray()) {
                    var type = pa.GetProperty("type").GetString() ?? "";
                    var name = pa.GetProperty("programargumentname").GetString() ?? "";
                    if (type == "discreteargument") {
                        var values = new List<string>();
                        if (pa.TryGetProperty("permissiblevalues", out var pvs)) {
                            foreach (var v in pvs.EnumerateArray()) values.Add(v.GetString() ?? "");
                        }
                        list.Add(new DiscreterProgramArgumentSetting { ProgramArgumentName = name, PermissibleValues = values });
                    } else if (type == "continuousargument") {
                        var min = (float)pa.GetProperty("minvalue").GetDouble();
                        var max = (float)pa.GetProperty("maxvalue").GetDouble();
                        var inc = (float)pa.GetProperty("increment").GetDouble();
                        list.Add(new ContinuousProgramArgumentSetting { ProgramArgumentName = name, MinValue = min, MaxValue = max, Increment = inc });
                    }
                }
            }
            programArguments = new ProgramArguments(list);
        }
    }


    public class ProgramArguments : IEnumerable<ProgramArgumentsSettingsBase>
    {
        private readonly List<ProgramArgumentsSettingsBase> _items;

        [JsonConstructor]
        public ProgramArguments(List<ProgramArgumentsSettingsBase>? programArguments)
        {
            _items = programArguments ?? new();
        }

        public IEnumerator<ProgramArgumentsSettingsBase> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public enum RobotProgramArgumentType
    {
        discreteargument,
        continuousargument
    }

    public class ProgramArgumentConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(ProgramArgumentsSettingsBase);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);

            var typeString = jo["type"]?.ToString();

            if (!Enum.TryParse<RobotProgramArgumentType>(typeString, true, out var type))
                throw new Exception($"Unknown argument type: {typeString}");

            ProgramArgumentsSettingsBase target = type switch
            {
                RobotProgramArgumentType.discreteargument => new DiscreterProgramArgumentSetting(),
                RobotProgramArgumentType.continuousargument => new ContinuousProgramArgumentSetting(),
                _ => throw new NotSupportedException(type.ToString())
            };

            serializer.Populate(jo.CreateReader(), target);
            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JObject jo = JObject.FromObject(value, serializer);
            jo.WriteTo(writer);
        }
    }

}
