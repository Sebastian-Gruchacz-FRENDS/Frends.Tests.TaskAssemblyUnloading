using System.Reflection;
using System.Runtime.Loader;

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

    // TODO: ExecuteAsync overload

    // TODO: ExecuteAsyncWithTimeout overload

    private static void ExecuteInternal(InvocationSpec spec)
    {
        var alc = new AssemblyLoadContext("TestContext", isCollectible: true);
        var weakRef = new WeakReference(alc);

        try
        {
            var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(spec.AssemblyPath));

            var type = asm.GetType(spec.TypeName, throwOnError: true)!;

            var method = ResolveMethod(type, spec.MethodName, spec.Arguments);
            var args = spec.Arguments;
            if (!args.Any())
            {
                args = TryBuildDefaultArguments(method);
            }

            // TODO: test with async / sync combinations, both for test method and Task method

            var result = method.Invoke(null, args);

            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }
        }
        finally
        {
            alc.Unload();
            ForceUnload(weakRef);
        }
    }

    private static object?[] TryBuildDefaultArguments(MethodInfo method)
    {
        var parameters = new List<object?>();
        foreach (var pInfo in method.GetParameters())
        {
            var pType = pInfo.ParameterType;
            object? value = Activator.CreateInstance(pType);
            parameters.Add(value);
        }

        return parameters.ToArray();
    }

    private static MethodInfo ResolveMethod(Type type, string methodName, object?[] args)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .ToArray();

        var matchedMethod = (args.Any())
            ? MatchMethodOnParameters(methods, args)
            : SelectSingleMethod(methods);
        
        return matchedMethod ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static MethodInfo? SelectSingleMethod(MethodInfo[] methods)
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

        return null;
    }

    private static MethodInfo? MatchMethodOnParameters(MethodInfo[] methods, object?[] args)
    {
        foreach (var m in methods)
        {
            var parameters = m.GetParameters();
            if (parameters.Length != args.Length)
                continue;

            bool match = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                    continue;

                if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                {
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
            throw new InvalidOperationException("AssemblyLoadContext failed to unload.");
    }
}