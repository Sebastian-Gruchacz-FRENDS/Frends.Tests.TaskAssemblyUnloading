using System.Runtime.Loader;
using System.Text.Json;

namespace Frends.Test.TaskInjection
{
    public class SerializationHelper
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new AlcObjectConverter() }
        };

        public SerializationHelper(AlcObjectConverter converter)
        {
            _options.Converters.Add(converter);
        }

        public string SerializeObject(object value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value, _options);
        }
    }

    public class DeserializationHelper
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new AlcObjectConverter() }
        };

        public DeserializationHelper(AlcObjectConverter converter)
        {
            _options.Converters.Add(converter);
        }

        public object? DeserializeObject(string json, Type targetType)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            return System.Text.Json.JsonSerializer.Deserialize(json, targetType, _options);
        }
    }

    public static class CrossAlcHelper
    {
        public static SerializationHelper GetSerialization()
        {
            return new SerializationHelper(new AlcObjectConverter());
        }

        public static DeserializationHelper GetDeserializationMethod()
        {
            return new DeserializationHelper(new AlcObjectConverter());
        }

        public static SerializationHelper GetSerialization(AssemblyLoadContext alcContext)
        {
            return alcContext.CreateInstanceInAlc<SerializationHelper>(alcContext.CreateInstanceInAlc<AlcObjectConverter>());
        }

        public static DeserializationHelper GetDeserializationMethod(AssemblyLoadContext alcContext)
        {
            return alcContext.CreateInstanceInAlc<DeserializationHelper>(alcContext.CreateInstanceInAlc<AlcObjectConverter>());
        }

        private static T CreateInstanceInAlc<T>(this AssemblyLoadContext alcContext)
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            var loadedAssembly = alcContext.LoadFromAssemblyName(assembly.GetName());
            var targetType = loadedAssembly.GetType(type.FullName!)!;
            return (T)Activator.CreateInstance(targetType)!;
        }

        private static T CreateInstanceInAlc<T>(this AssemblyLoadContext alcContext, params object?[] constructorArgs)
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            var loadedAssembly = alcContext.LoadFromAssemblyName(assembly.GetName());
            var targetType = loadedAssembly.GetType(type.FullName!)!;

            return (T)Activator.CreateInstance(targetType, constructorArgs)!;
        }
    }
}
