using System.Text.Json;
using System.Text.Json.Serialization;

namespace Custom.Framework.Helpers
{
    /// <summary>
    /// JsonConverter from string to int
    /// </summary>
    public class ApiStringToIntConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException();

            var stringValue = reader.GetString();
            if (!int.TryParse(stringValue, out int value))
                throw new JsonException($"Unable to parse '{stringValue}' to an integer.");

            return value;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    /// <summary>
    /// JsonConverter from List<string> to List<int>, and ignore the non-parsed-string type
    /// </summary>
    public class ApiStringListToIntListConverter : JsonConverter<List<int>>
    {
        public override List<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            var intList = new List<int>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return intList;

                if (reader.TokenType == JsonTokenType.String)
                {
                    if (int.TryParse(reader.GetString(), out int value))
                    {
                        intList.Add(value);
                    }
                    else
                    {
                        //throw new JsonException("Unable to convert string to integer.");
                    }
                }
                else
                {
                    //throw new JsonException("Unexpected token type.");
                }
            }

            throw new JsonException("Unexpected end of JSON input.");
        }

        public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var intValue in value)
            {
                writer.WriteStringValue(intValue.ToString());
            }

            writer.WriteEndArray();
        }
    }
}
