namespace Frends.Tests.TaskAssemblyUnloading;

public sealed class TypeSelector
{
    private readonly string _assemblyPath;
    private readonly string _typeName;

    internal TypeSelector(string assemblyPath, string typeName)
    {
        _assemblyPath = assemblyPath;
        _typeName = typeName;
    }

    /// <summary>
    /// Specify Task Method to execute
    /// </summary>
    /// <param name="methodName"></param>
    /// <returns></returns>
    public MethodSelector TaskMethod(string methodName) => new(_assemblyPath, _typeName, methodName);
}