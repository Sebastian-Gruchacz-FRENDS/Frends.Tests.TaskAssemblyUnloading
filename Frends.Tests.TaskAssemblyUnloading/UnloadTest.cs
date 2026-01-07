namespace Frends.Tests.TaskAssemblyUnloading;

public static class UnloadTest
{
    /// <summary>
    /// Start building Unload Test using fluent syntax
    /// </summary>
    /// <param name="assemblyPath"></param>
    /// <returns></returns>
    public static AssemblySelector From(string assemblyPath) => new(assemblyPath);

    /// <summary>
    /// Build Unload Test using compact syntax
    /// </summary>
    /// <param name="assemblyPath"></param>
    /// <param name="typeName"></param>
    /// <param name="methodName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static ExecutionBuilder Invoke(
        string assemblyPath,
        string typeName,
        string methodName,
        params object?[] args)
    {
        return new ExecutionBuilder(new InvocationSpec(assemblyPath, typeName, methodName, args));
    }

    public static TypeSelector FromType(Type targetClass)
    {
        var asmName = targetClass.Assembly.Location;
        return new TypeSelector(asmName, targetClass.FullName!);
    }
}