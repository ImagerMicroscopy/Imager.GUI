using Newtonsoft.Json;
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
    
}
