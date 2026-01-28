namespace Frends.Tests.TaskAssemblyUnloading;

internal class InvocationSpec
{
    public InvocationSpec(string assemblyPath, string typeName, string methodName, bool useSerializationIfNeeded, object?[]? args = null)
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
        UseSerializationIfNeeded = useSerializationIfNeeded;
    }

    public bool UseSerializationIfNeeded { get; private set; }
    public string AssemblyPath { get; private set; }
    public string TypeName { get; private set; }
    public string MethodName { get; private set; }
    public object?[] Arguments { get; private set; }
}