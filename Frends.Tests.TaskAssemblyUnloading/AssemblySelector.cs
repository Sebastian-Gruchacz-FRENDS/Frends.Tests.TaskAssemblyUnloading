namespace Frends.Tests.TaskAssemblyUnloading;

public sealed class AssemblySelector
{
    private readonly string _assemblyPath;

    internal AssemblySelector(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(assemblyPath));
        }

        _assemblyPath = assemblyPath;
    }

    /// <summary>
    /// Specify class where Task Method is located in
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public TypeSelector Type(string typeName) => new(_assemblyPath, typeName);
}