using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImagerAvalonia.Services.ImagerModels.SmartProgramModels
{
    public class ImportedInputFunctionModel
    {
        public string method_name { get; set; } = string.Empty;

        public List<string> method_params { get; set; } = new();
    }

    public enum InputParameterType
    {
        Integer,
        Scalar,
        Boolean,
        Text
    }


    public abstract class InputParameterBase : INotifyPropertyChanged
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public InputParameterType type { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public abstract class InputParameter<T> : InputParameterBase
    {
        private T _value;
        public T value
        {
            get => _value;
            set => SetField(ref _value, value);
        }

        private string _annotation = string.Empty;
        public string annotation
        {
            get => _annotation;
            set => SetField(ref _annotation, value);
        }

        public string variable;

        protected InputParameter(T value, string annotation, string variable_name)
        {
            _value = value;
            _annotation = annotation;
            variable = variable_name;
        }
    }


    public class IntegerVM : InputParameter<int>
    {
        public IntegerVM(int value, string annotation, string variable_name) : base(value, annotation, variable_name)
        {
            type = InputParameterType.Integer;
        }
    }

    public class ScalarVM : InputParameter<double>
    {
        public ScalarVM(double value, string annotation, string variable_name) : base(value, annotation, variable_name)
        {
            type = InputParameterType.Scalar;
        }
    }

    public class BooleanVM : InputParameter<bool>
    {
        public BooleanVM(bool value, string annotation, string variable_name) : base(value, annotation, variable_name)
        {
            type = InputParameterType.Boolean;
        }
    }

    public class TextVM : InputParameter<string>
    {
        public TextVM(string value, string annotation, string variable_name) : base(value, annotation, variable_name)
        {
            type = InputParameterType.Text;
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
            writer.WriteValue(value.type.ToString());

            writer.WritePropertyName("annotation");
            writer.WriteValue(value.annotation ?? string.Empty);

            writer.WritePropertyName("variable");
            writer.WriteValue(value.variable ?? string.Empty);

            writer.WritePropertyName("value");
            writer.WriteValue(value.value);
        }
    }
}