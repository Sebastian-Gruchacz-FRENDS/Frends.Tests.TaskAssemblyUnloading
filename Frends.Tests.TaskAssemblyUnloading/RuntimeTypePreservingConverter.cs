using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frends.Tests.TaskAssemblyUnloading;

/// <summary>
/// JSON converter factory that creates converters for preserving runtime type information.
/// Works without requiring [JsonDerivedType] attributes on external code.
/// Handles collections and dictionaries with polymorphic types.
/// 
/// Supported scenarios (common in Task parameters):
/// - List&lt;T&gt;, IEnumerable&lt;T&gt;, ICollection&lt;T&gt;, IList&lt;T&gt; where T is class or object
/// - Arrays of class types or object (T[], object[])
/// - Dictionary&lt;TKey, object&gt; with any JSON-serializable key type
/// 
/// Deliberately scoped to real-world Task parameter patterns.
/// </summary>
public class RuntimeTypePreservingConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Handle Lists, Arrays, and IEnumerables of class types
        if (typeToConvert.IsGenericType)
        {
            var genericDef = typeToConvert.GetGenericTypeDefinition();
            
            // Handle collections
            if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>) || 
                genericDef == typeof(ICollection<>) || genericDef == typeof(IList<>))
            {
                var elementType = typeToConvert.GetGenericArguments()[0];
                // Handle class types (potential polymorphism) including object
                return (elementType.IsClass || elementType == typeof(object)) && elementType != typeof(string);
            }
            
            // Handle Dictionary<TKey, object> - common for config/parameters
            // Support any key type that JSON can serialize as property name
            if (genericDef == typeof(Dictionary<,>))
            {
                var valueType = typeToConvert.GetGenericArguments()[1];
                // Handle if value type is object (polymorphic values)
                return valueType == typeof(object);
            }
        }
        
        if (typeToConvert.IsArray)
        {
            var elementType = typeToConvert.GetElementType();
            // Handle class types including object, but not strings
            return elementType != null && 
                   ((elementType.IsClass || elementType == typeof(object)) && elementType != typeof(string));
        }

        return false;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert.IsArray)
        {
            var elementType = typeToConvert.GetElementType()!;
            var converterType = typeof(PolymorphicCollectionConverter<>).MakeGenericType(elementType);
            return (JsonConverter?)Activator.CreateInstance(converterType);
        }
        
        if (typeToConvert.IsGenericType)
        {
            var genericDef = typeToConvert.GetGenericTypeDefinition();
            
            // Handle Dictionary<TKey, object>
            if (genericDef == typeof(Dictionary<,>))
            {
                var keyType = typeToConvert.GetGenericArguments()[0];
                var valueType = typeToConvert.GetGenericArguments()[1];
                
                if (valueType == typeof(object))
                {
                    // Create generic converter for any key type
                    // Note: Key type must be JSON-serializable as property name (string, int, etc.)
                    var converterType = typeof(PolymorphicDictionaryConverter<>).MakeGenericType(keyType);
                    return (JsonConverter?)Activator.CreateInstance(converterType);
                }
            }
            
            // Handle collections
            var elementType = typeToConvert.GetGenericArguments()[0];
            var collectionConverterType = typeof(PolymorphicCollectionConverter<>).MakeGenericType(elementType);
            return (JsonConverter?)Activator.CreateInstance(collectionConverterType);
        }

        return null;
    }
}

/// <summary>
/// Converter for collections that preserves runtime type information for each element.
/// </summary>
public class PolymorphicCollectionConverter<T> : JsonConverter<List<T>> where T : class
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array");
        }

        var list = new List<T>();
        
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return list;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                // Check for embedded type information
                if (root.TryGetProperty("$type", out var typeProperty))
                {
                    var typeName = typeProperty.GetString();
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        var actualType = ResolveType(typeName);
                        if (actualType != null && root.TryGetProperty("$value", out var valueElement))
                        {
                            var item = (T?)JsonSerializer.Deserialize(valueElement.GetRawText(), actualType, options);
                            if (item != null)
                            {
                                list.Add(item);
                            }
                            continue;
                        }
                    }
                }

                // No type information, deserialize as declared type
                var defaultItem = JsonSerializer.Deserialize<T>(root.GetRawText(), options);
                if (defaultItem != null)
                {
                    list.Add(defaultItem);
                }
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        foreach (var item in value)
        {
            if (item == null)
            {
                writer.WriteNullValue();
                continue;
            }

            var actualType = item.GetType();

            // If runtime type differs from declared type, embed type information
            if (actualType != typeof(T))
            {
                writer.WriteStartObject();
                writer.WriteString("$type", actualType.AssemblyQualifiedName);
                writer.WritePropertyName("$value");
                JsonSerializer.Serialize(writer, item, actualType, options);
                writer.WriteEndObject();
            }
            else
            {
                // Same type, serialize normally
                JsonSerializer.Serialize(writer, item, actualType, options);
            }
        }

        writer.WriteEndArray();
    }

    private Type? ResolveType(string typeName)
    {
        try
        {
            // Try full type resolution
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            // Try without assembly version
            var parts = typeName.Split(',');
            if (parts.Length > 1)
            {
                var simpleTypeName = $"{parts[0]}, {parts[1]}";
                type = Type.GetType(simpleTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return Type.GetType(parts[0].Trim());
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Converter for Dictionary&lt;TKey, object&gt; that preserves runtime type information for values.
/// Handles mixed value types like strings, ints, bools, doubles in config dictionaries.
/// Supports any key type that JSON can serialize as a property name.
/// </summary>
public class PolymorphicDictionaryConverter<TKey> : JsonConverter<Dictionary<TKey, object>> where TKey : notnull
{
    public override Dictionary<TKey, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for dictionary");
        }

        var dictionary = new Dictionary<TKey, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var keyString = reader.GetString()!;
            
            // Convert key from string to TKey
            TKey key;
            if (typeof(TKey) == typeof(string))
            {
                key = (TKey)(object)keyString;
            }
            else if (typeof(TKey) == typeof(int))
            {
                key = (TKey)(object)int.Parse(keyString);
            }
            else if (typeof(TKey) == typeof(long))
            {
                key = (TKey)(object)long.Parse(keyString);
            }
            else if (typeof(TKey) == typeof(Guid))
            {
                key = (TKey)(object)Guid.Parse(keyString);
            }
            else
            {
                // Fallback: try Convert.ChangeType
                key = (TKey)Convert.ChangeType(keyString, typeof(TKey))!;
            }
            
            reader.Read(); // Move to value

            // Read the value
            object? value = ReadValue(ref reader, options);
            if (value != null)
            {
                dictionary[key] = value;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, object> value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            // Convert key to string for JSON property name
            var keyString = kvp.Key.ToString()!;
            writer.WritePropertyName(keyString);
            WriteValue(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intVal))
                    return intVal;
                if (reader.TryGetInt64(out var longVal))
                    return longVal;
                return reader.GetDouble();
            
            case JsonTokenType.True:
                return true;
            
            case JsonTokenType.False:
                return false;
            
            case JsonTokenType.Null:
                return null;
            
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    var root = doc.RootElement;
                    
                    // Check for embedded type information
                    if (root.TryGetProperty("$type", out var typeProperty))
                    {
                        var typeName = typeProperty.GetString();
                        if (!string.IsNullOrEmpty(typeName))
                        {
                            var actualType = ResolveType(typeName);
                            if (actualType != null && root.TryGetProperty("$value", out var valueElement))
                            {
                                return JsonSerializer.Deserialize(valueElement.GetRawText(), actualType, options);
                            }
                        }
                    }
                    
                    // No type information, return as dictionary
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText(), options);
                }
            
            case JsonTokenType.StartArray:
                // Arrays in dictionary values
                return JsonSerializer.Deserialize<List<object>>(ref reader, options);
            
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        var actualType = value.GetType();

        // Handle primitives directly
        if (actualType == typeof(string))
        {
            writer.WriteStringValue((string)value);
        }
        else if (actualType == typeof(int))
        {
            writer.WriteNumberValue((int)value);
        }
        else if (actualType == typeof(long))
        {
            writer.WriteNumberValue((long)value);
        }
        else if (actualType == typeof(double))
        {
            writer.WriteNumberValue((double)value);
        }
        else if (actualType == typeof(float))
        {
            writer.WriteNumberValue((float)value);
        }
        else if (actualType == typeof(bool))
        {
            writer.WriteBooleanValue((bool)value);
        }
        else if (actualType == typeof(decimal))
        {
            writer.WriteNumberValue((decimal)value);
        }
        else
        {
            // For complex types, embed type information
            writer.WriteStartObject();
            writer.WriteString("$type", actualType.AssemblyQualifiedName);
            writer.WritePropertyName("$value");
            JsonSerializer.Serialize(writer, value, actualType, options);
            writer.WriteEndObject();
        }
    }

    private Type? ResolveType(string typeName)
    {
        try
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var parts = typeName.Split(',');
            if (parts.Length > 1)
            {
                var simpleTypeName = $"{parts[0]}, {parts[1]}";
                type = Type.GetType(simpleTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return Type.GetType(parts[0].Trim());
        }
        catch
        {
            return null;
        }
    }
}




