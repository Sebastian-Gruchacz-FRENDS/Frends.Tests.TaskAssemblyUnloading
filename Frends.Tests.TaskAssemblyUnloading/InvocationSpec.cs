namespace Frends.Tests.TaskAssemblyUnloading;

internal class InvocationSpec
{
    public InvocationSpec(string assemblyPath, string typeName, string methodName, bool useSerializationIfNeeded, object?[]? args = null, AlcArgumentList? alcArguments = null)
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
        AlcArguments = alcArguments;
    }

    public bool UseSerializationIfNeeded { get; private set; }
    public string AssemblyPath { get; private set; }
    public string TypeName { get; private set; }
    public string MethodName { get; private set; }
    public object?[] Arguments { get; private set; }
    
    /// <summary>
    /// Builder-based arguments that will be materialized inside the target ALC.
    /// When set, takes precedence over Arguments.
    /// </summary>
    public AlcArgumentList? AlcArguments { get; private set; }
}