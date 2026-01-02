namespace Frends.Tests.TaskAssemblyUnloading;

internal class InvocationSpec
{
    public InvocationSpec(string assemblyPath, string typeName, string methodName, object?[]? args = null)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(assemblyPath));
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(typeName));
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(methodName));
        }

        AssemblyPath = assemblyPath;
        TypeName = typeName;
        MethodName = methodName;
        Arguments = args ?? [];
    }

    public string AssemblyPath { get; set; }
    public string TypeName { get; set; }
    public string MethodName { get; set; }
    public object?[] Arguments { get; set; }
}