using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frends.Test.TaskInjection
{
    public sealed class AlcObjectConverter<T> : JsonConverter<T> where T : class
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return ReadPrimitive(typeToConvert, ref reader) as T;
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("$t", out var typeProp))
            {
                return Activator.CreateInstance<T>();
            }

            var typeName = typeProp.GetString();
            var type = Type.GetType(typeName!, throwOnError: true)!;

            var value = root.GetProperty("$v");
            return value.Deserialize(type, options) as T;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
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
