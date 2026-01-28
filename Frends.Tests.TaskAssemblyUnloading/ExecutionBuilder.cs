using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frends.Tests.TaskAssemblyUnloading;

public sealed class ExecutionBuilder
{
    private readonly InvocationSpec _spec;

    internal ExecutionBuilder(InvocationSpec spec)
    {
        _spec = spec;
    }

    /// <summary>
    /// Execute Task Method
    /// </summary>
    public void Execute()
    {
        ExecuteInternal(_spec);
    }

    private static void ExecuteInternal(InvocationSpec spec)
    {
        bool loadedAndExecuted = false;

        try
        {
            // cannot even keep ref to ALC in order to properly unload it
            loadedAndExecuted = ExecuteInternalWeak(spec, out var reference);
            if (loadedAndExecuted && reference != null) 
            {
                ForceUnload(reference);
            }
        }
        catch (Exception)
        {
            if (!loadedAndExecuted)
            {
                throw;
            }
        }
    }

    private static bool ExecuteInternalWeak(InvocationSpec spec, out WeakReference? weakReference)
    {
        var alc = new AssemblyLoadContext("TestContext" + Guid.NewGuid().ToString(), isCollectible: true);
        weakReference = new WeakReference(alc);
        bool loaded = false;
        bool executed = false;

        try
        {
            FindAndTryMethodExecution(spec, alc, ref loaded, ref executed);
        }
        catch (Exception ex)
        {
            UnloadDiagnostics.Log($"Execution failed: {ex}");
            throw;
        }
        finally
        {
            if (loaded && executed)
            {
                UnloadDiagnostics.DumpAssemblyLoadContext(alc);
            }
        }

        // test unloadability only if assembly was properly located, loaded and Task executed - to not hide other exceptions
        return (loaded && executed);
    }

    private static void FindAndTryMethodExecution(InvocationSpec spec, AssemblyLoadContext alc, ref bool loaded, ref bool executed)
    {
        UnloadDiagnostics.Log($"Loading: {spec.AssemblyPath}");
        var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(spec.AssemblyPath));
        loaded = true;

        var type = asm.GetType(spec.TypeName, throwOnError: true)!;

        var method = ResolveMethod(type, spec.MethodName, spec.Arguments, spec.UseSerializationIfNeeded, out var shouldSerialize);
        var args = spec.Arguments;

        if (!args.Any())
        {
            args = TryBuildDefaultArguments(method);
        }
        else
        {
            if (shouldSerialize)
            {
                var coreSerializer = typeof(JsonSerializer);
                var textAssembly = alc.LoadFromAssemblyPath(Path.GetFullPath(coreSerializer.Assembly.Location));
                var alcSerializer = textAssembly.GetType(coreSerializer.FullName!, true);

                args = SerializeArgumentsThroughAlcBoundary(coreSerializer, alcSerializer!, args, method);
            }
        }

        // TODO: test with async / sync combinations, both for test method and Task method
        UnloadDiagnostics.Log($"Invoking: {type.FullName}.{method.Name}");
        var result = method.Invoke(null, args);

        if (result is Task task)
        {
            task.GetAwaiter().GetResult();
        }

        executed = true;

        UnloadDiagnostics.Log("Unloading ALC");
        alc.Unload();
    }

    private static object?[] SerializeArgumentsThroughAlcBoundary(Type coreSerializer, Type alcSerializer, object?[] args, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var newArgs = new object?[args.Length];

        // locate serializer methods reflectively
        var coreSerialize = coreSerializer.GetMethod(nameof(JsonSerializer.Serialize),
            BindingFlags.Public | BindingFlags.Static, [typeof(object), typeof(Type), typeof(JsonSerializerOptions)]);

        var alcAssembly = alcSerializer.Assembly;
        var alcContextType = alcAssembly.GetType(typeof(JsonSerializerOptions).FullName!)!;
        var alcDeserialize = alcSerializer.GetMethod(nameof(JsonSerializer.Deserialize),
            BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(Type), alcContextType]);

        //JsonSerializer.Deserialize("dsdd", alcContextType, new JsonSerializerOptions());

        if (coreSerialize == null || alcDeserialize == null)
            throw new InvalidOperationException("Failed to locate JsonSerializer.Serialize/Deserialize methods via reflection.");

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var paramType = parameters[i].ParameterType;

            if (arg == null)
            {
                newArgs[i] = null;
                continue;
            }

            // If argument already matches the parameter type (unlikely across ALC boundary) just forward it
            if (paramType.IsInstanceOfType(arg))
            {
                newArgs[i] = arg;
                continue;
            }

            // Only attempt serialization when names match (type identity differs because of ALC)
            if (!paramType.FullName!.Equals(arg.GetType().FullName, StringComparison.Ordinal))
            {
                // types differ by name as well; cannot safely transfer across boundary
                throw new InvalidOperationException($"Cannot marshal argument of type '{arg.GetType().FullName}' to parameter type '{paramType.FullName}'.");
            }

            try
            {
                // Serialize in current (default) context
                var json = coreSerialize.Invoke(null, [arg, arg.GetType(), null]) as string;
                if (json == null)
                {
                    throw new InvalidOperationException("JsonSerializer.Serialize() returned null.");
                }

                // Deserialize inside ALC context to parameter type
                var deserialized = alcDeserialize.Invoke(null, [json, paramType, Activator.CreateInstance(alcContextType)]);
                newArgs[i] = deserialized;
            }
            catch (TargetInvocationException tie)
            {
                UnloadDiagnostics.Log($"Serialization/Deserialization failed for parameter {i}: {tie.InnerException ?? tie}");
                throw;
            }
            catch (Exception ex)
            {
                UnloadDiagnostics.Log($"Serialization/Deserialization failed for parameter {i}: {ex}");
                throw;
            }
        }

        return newArgs;
    }

    private static object?[] TryBuildDefaultArguments(MethodInfo method)
    {
        var parameters = new List<object?>();
        foreach (var pInfo in method.GetParameters())
        {
            var pType = pInfo.ParameterType;
            object? value = pInfo.ParameterType.IsValueType 
                ? Activator.CreateInstance(pType)
                : null;
            parameters.Add(value);
        }

        return parameters.ToArray();
    }

    private static MethodInfo ResolveMethod(Type type, string methodName, object?[] args, bool enableSerialization, out bool shouldSerialize)
    {
        shouldSerialize = false;

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .ToArray();

        var matchedMethod = (args.Any())
            ? MatchMethodOnParameters(methods, args, enableSerialization, out shouldSerialize)
            : SelectSingleMethod(methods, type, methodName);
        
        return matchedMethod ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static MethodInfo? SelectSingleMethod(MethodInfo[] methods, Type type, string methodName)
    {
        // TODO: perhaps could match one with [settings] or [options] decorations only...

        // most probably there are no such method that takes zero parameters, it has no sense as a Task
        methods = methods.Where(m => m.GetParameters().Length > 0)
            .OrderBy(m => m.GetParameters().Length)
            .ToArray();

        // TODO: return first? not single?

        if (methods.Length == 1)
        {
            return methods[0];
        }

        if (methods.Any())
        {
            throw new AmbiguousMatchException($"Multiple overloads found for '{methodName}', arguments required.");
        }
        else
        {
            throw new MissingMethodException(type.FullName, methodName);
        }
    }

    private static MethodInfo? MatchMethodOnParameters(MethodInfo[] methods, object?[] arguments, bool serializeBoundaryParameters, out bool shouldSerialize)
    {
        shouldSerialize = false;

        foreach (var m in methods)
        {
            var parameters = m.GetParameters();
            if (parameters.Length != arguments.Length)
                continue;

            bool match = true;
            
            for (int i = 0; i < parameters.Length; i++)
            {
                if (arguments[i] == null)
                    continue;

                if (!(parameters[i].ParameterType.IsInstanceOfType(arguments[i])))
                {
                    if (serializeBoundaryParameters &&
                        parameters[i].ParameterType.FullName!.Equals(arguments[i]?.GetType().FullName))
                    {
                        shouldSerialize = true;
                        continue;
                    }

                    match = false;
                    break;
                }
            }

            if (match)
                return m;
        }

        return null;
    }

    private static void ForceUnload(WeakReference weakRef)
    {
        // TODO: raw AI proposal, validate and compare with SDK samples


        for (int i = 0; weakRef.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if (weakRef.IsAlive)
        {
            throw new InvalidOperationException("AssemblyLoadContext failed to unload.");
        }
    }
}