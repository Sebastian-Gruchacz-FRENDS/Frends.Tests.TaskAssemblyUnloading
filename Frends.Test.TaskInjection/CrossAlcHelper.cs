using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Frends.Test.TaskInjection
{
    public class SerializationHelper
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            IncludeFields = true
        };

        public SerializationHelper(JsonConverter converter)
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
        private readonly AssemblyLoadContext? _alc;

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            IncludeFields = true
        };

        public DeserializationHelper(JsonConverter converter, AssemblyLoadContext? alc = null)
        {
            _alc = alc;
            _options.Converters.Add(converter);
            if (alc != null)
            {
                _options.TypeInfoResolver = new AlcTypeResolver(alc);
            }
        }

        public object? DeserializeObject(string json, Type targetType)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            if (_alc != null)
            {
                var asm = _alc.TryLoadingAlcAssemblyForType(targetType);
                targetType = asm.GetType(targetType.FullName);

                // sanity check
                if (AssemblyLoadContext.GetLoadContext(targetType.Assembly) != _alc)
                {
                    throw new InvalidOperationException("Wrong context.");
                }
            }

            return System.Text.Json.JsonSerializer.Deserialize(json, targetType, _options);
        }
    }

    public class AlcTypeResolver : IJsonTypeInfoResolver
    {
        private readonly AssemblyLoadContext _alc;

        public AlcTypeResolver(AssemblyLoadContext alc)
        {
            _alc = alc ?? throw new ArgumentNullException(nameof(alc));
        }

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var alc = AssemblyLoadContext.GetLoadContext(type.Assembly);
            if (alc != _alc)
            {
                var alcAsm = _alc.TryLoadingAlcAssemblyForType(type);
                var targetType = alcAsm.GetType(type.FullName!)!;
                return JsonTypeInfo.CreateJsonTypeInfo(targetType, options);
            }

            return JsonTypeInfo.CreateJsonTypeInfo(type, options);
        }
    }

    public static class CrossAlcHelper
    {
        public static SerializationHelper GetSerialization<T>() where T : class
        {
            return new SerializationHelper(new AlcObjectConverter<T>());
        }

        public static SerializationHelper GetSerialization(Type type)
        {
            var genericType = typeof(AlcObjectConverter<>);
            var targetType = genericType.MakeGenericType([type]);
            return new SerializationHelper((JsonConverter)Activator.CreateInstance(targetType));
        }

        public static DeserializationHelper GetDeserialization<T>() where T : class
        {
            return new DeserializationHelper(new AlcObjectConverter<T>());
        }

        public static (object, MethodInfo) GetSerialization<T>(AssemblyLoadContext alcContext) where T : class
        {
            var serializer = alcContext.CreateInstanceInAlc<SerializationHelper>(
                alcContext.CreateInstanceInAlc<AlcObjectConverter<T>>());

            var methodInfo = serializer.GetType().GetMethod(nameof(SerializationHelper.SerializeObject), [typeof(object)])!;

            return (serializer, methodInfo);
        }

        public static (object, MethodInfo) GetDeserialization<T>(AssemblyLoadContext alcContext) where T : class
        {
            var deserializer = alcContext.CreateInstanceInAlc<DeserializationHelper>(
                alcContext.CreateInstanceInAlc<AlcObjectConverter<T>>(), alcContext);

            var methodInfo = deserializer.GetType().GetMethod(nameof(DeserializationHelper.DeserializeObject), [typeof(string), typeof(Type)])!;

            return (deserializer, methodInfo);
        }

        public static (object, MethodInfo) GetDeserialization(AssemblyLoadContext alcContext, Type type)
        {
            var genericType = typeof(AlcObjectConverter<>);
            var targetType = genericType.MakeGenericType([type]);

            var deserializer = alcContext.CreateInstanceInAlc<DeserializationHelper>(
                alcContext.CreateInstanceInAlc(targetType), alcContext);

            var methodInfo = deserializer.GetType().GetMethod(nameof(DeserializationHelper.DeserializeObject), [typeof(string), typeof(Type)])!;

            return (deserializer, methodInfo);
        }

        private static object? CreateInstanceInAlc<T>(this AssemblyLoadContext alcContext)
        {
            var type = typeof(T);
            var loadedAssembly = alcContext.TryLoadingAlcAssemblyForType(type);
            var activatorAssembly = alcContext.TryLoadingAlcAssemblyForType(typeof(Activator));
            var targetType = loadedAssembly.GetType(type.FullName!)!;
            var alcActivator = activatorAssembly.GetType(typeof(Activator).FullName!);
            var alcActivatorMethod = alcActivator!.GetMethod(nameof(Activator.CreateInstance),
                BindingFlags.Public | BindingFlags.Static,
                [typeof(Type)]);
            return alcActivatorMethod?.Invoke(null, [targetType]);
        }

        private static object? CreateInstanceInAlc<T>(this AssemblyLoadContext alcContext, params object?[] constructorArgs)
        {
            var type = typeof(T);

            return alcContext.CreateInstanceInAlc(type, constructorArgs);
        }

        private static object? CreateInstanceInAlc(this AssemblyLoadContext alcContext, Type type, params object?[] constructorArgs)
        {
            var loadedAssembly = alcContext.TryLoadingAlcAssemblyForType(type);
            var activatorAssembly = alcContext.TryLoadingAlcAssemblyForType(typeof(Activator));
            var targetType = loadedAssembly.GetType(type.FullName!)!;
            var alcActivator = activatorAssembly.GetType(typeof(Activator).FullName!);
            var alcActivatorMethod = alcActivator!.GetMethod(nameof(Activator.CreateInstance),
                BindingFlags.Public | BindingFlags.Static,
                [typeof(Type), typeof(object?[])]);

            return alcActivatorMethod?.Invoke(null, [targetType, constructorArgs]);
        }

        internal static Assembly TryLoadingAlcAssemblyForType(this AssemblyLoadContext alc, Type type)
        {
            var asm = type.Assembly;
            var existingAsm = (alc.Assemblies.FirstOrDefault(a => a.GetName().Equals(asm.GetName())));
            if (existingAsm != null)
            {
                return existingAsm;
            }

            if (IsFrameworkAssemblyName(asm.FullName))
            {
                return asm;
            }

            var loadedAssembly = alc.LoadFromAssemblyPath(asm.Location);
            if (AssemblyLoadContext.GetLoadContext(loadedAssembly) != alc)
            {
                throw new InvalidOperationException("Not loaded a new Assembly into ALC.");
            }

            return loadedAssembly;
        }

        private static bool IsFrameworkAssemblyName(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            // Simple heuristic: common framework / runtime assembly name prefixes or known names.
            // Extend list as needed for your environment.
            var normalized = assemblyName.Split(',')[0].Trim();
            if (string.Equals(normalized, "System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "mscorlib", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "netstandard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "System", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefixes = new[]
            {
                "System.", "Microsoft.", "WindowsBase", "PresentationCore", "PresentationFramework", "Accessibility", "Mono."
            };

            foreach (var p in prefixes)
            {
                if (normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
