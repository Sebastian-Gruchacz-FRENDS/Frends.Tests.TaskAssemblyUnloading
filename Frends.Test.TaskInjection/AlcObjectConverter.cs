using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frends.Test.TaskInjection
{
    public sealed class AlcObjectConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return ReadPrimitive(typeToConvert, ref reader);
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("$t", out var typeProp))
            {
                return root;
            }

            var typeName = typeProp.GetString();
            var type = Type.GetType(typeName!, throwOnError: true)!;

            var value = root.GetProperty("$v");
            return value.Deserialize(type, options);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var type = value.GetType();

            writer.WriteStartObject();
            writer.WriteString("$t", type.AssemblyQualifiedName);
            writer.WritePropertyName("$v");
            JsonSerializer.Serialize(writer, value, type, options);
            writer.WriteEndObject();
        }

        private object? ReadPrimitive(Type typeToConvert, ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetDouble(), // todo: expand to other numeric types if needed
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Unsupported token type: {reader.TokenType}"),
            };
        }
    }
}
