using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Utils
{

    public class RoundingDoubleConverter : JsonConverter<double>
    {
        private readonly int _decimals;
        public RoundingDoubleConverter(int decimals)
        {
            _decimals = decimals;
        }

        public override double ReadJson(JsonReader reader, Type objectType, double existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
            {
                double value = Convert.ToDouble(reader.Value);
                return Math.Round(value, _decimals);
            }
            return 0; // or throw new JsonSerializationException("Expected number");
        }

        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            writer.WriteValue(value); // normal writing
        }
    }

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

}
