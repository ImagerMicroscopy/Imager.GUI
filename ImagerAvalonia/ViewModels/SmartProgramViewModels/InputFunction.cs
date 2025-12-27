using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{



    public class InputFunction
    {
        public string method_name { get; set; } = string.Empty;

        public List<string> method_params { get; set; }  = new ();
    }

    public enum InputParameterType
    {
        Integer,
        Scalar,
        Boolean,
        Text
    }

    public partial class InputParameter<T> : InputParameterBase
    {
        [ObservableProperty] T _value;
        [ObservableProperty] string _annotation;
        public string VariableName;
        public InputParameter(T value, string annotation, string variable_name)
        {
            Value = value;
            Annotation = annotation;
            VariableName = variable_name;
        }

    }

    public partial class InputParameterBase : ViewModelBase
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public InputParameterType Type { get; set; }
    }

    public partial class IntegerVM : InputParameter<int>
    {
        public IntegerVM(int value, string annotation, string variable_name) : base(value, annotation, variable_name) { 
            Type = InputParameterType.Integer;
        }
    }

    public partial class ScalarVM : InputParameter<double>
    {
        public ScalarVM(double value, string annotation, string variable_name) : base(value, annotation, variable_name) {
            Type = InputParameterType.Scalar;
        }
    }

    public partial class BooleanVM : InputParameter<bool>
    {
        public BooleanVM(bool value, string annotation, string variable_name) : base(value, annotation, variable_name) {
            Type = InputParameterType.Boolean;
        }
    }

    public partial class TextVM : InputParameter<string>
    {
        public TextVM(string value, string annotation, string variable_name) : base(value, annotation, variable_name) {
            Type = InputParameterType.Text;
        }
    }

    public class InputParameterConverter : JsonConverter<InputParameterBase>
    {
        public override InputParameterBase? ReadJson(JsonReader reader, Type objectType,
            InputParameterBase? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject obj = JObject.Load(reader);
            InputParameterType type = (InputParameterType)Enum.Parse(typeof(InputParameterType), obj["type"].ToString());


            var annotation = obj["annotation"]?.ToObject<string>(serializer) ?? string.Empty;
            var variable_name = obj["variable"]?.ToObject<string>(serializer) ?? string.Empty;

            switch (type)
            {
                case InputParameterType.Scalar:
                    {
                        double value = obj["value"]?.ToObject<double?>(serializer) ?? 0.0;
                        return new ScalarVM(value, annotation, variable_name);
                    }
                case InputParameterType.Text:
                    {
                        string value = obj["value"]?.ToObject<string>(serializer) ?? string.Empty;
                        return new TextVM(value, annotation, variable_name);
                    }
                case InputParameterType.Boolean:
                    {
                        bool value = obj["value"]?.ToObject<bool?>(serializer) ?? false;
                        return new BooleanVM(value, annotation, variable_name);
                    }
                case InputParameterType.Integer:
                    {
                        int value = obj["value"]?.ToObject<int?>(serializer) ?? 0;
                        return new IntegerVM(value, annotation, variable_name);
                    }
                default:
                    return null;
            }
        }

        public override void WriteJson(JsonWriter writer, InputParameterBase value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteStartObject();

            switch (value)
            {
                case ScalarVM scalar:
                    WriteInputParameters<double>(scalar, writer);
                    break;
                case TextVM text:
                    WriteInputParameters<string>(text, writer);
                    break;
                case BooleanVM boolean:
                    WriteInputParameters<bool>(boolean, writer);
                    break;
                case IntegerVM integer:
                    WriteInputParameters<int>(integer, writer);
                    break;
                default:
                    writer.WriteNull();
                    break;
            }

            writer.WriteEndObject();

        }
        private void WriteInputParameters<T>(InputParameter<T> value, JsonWriter writer)
        {
            writer.WritePropertyName("type");
            writer.WriteValue(value.Type.ToString());

            writer.WritePropertyName("annotation");
            writer.WriteValue(value.Annotation ?? string.Empty);

            writer.WritePropertyName("variable");
            writer.WriteValue(value.VariableName ?? string.Empty);
            writer.WritePropertyName("value");
            writer.WriteValue(value.Value);

        }
    }
}