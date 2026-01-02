namespace Frends.Tests.TaskAssemblyUnloading;

public sealed class MethodSelector
{
    private readonly string _assemblyPath;
    private readonly string _typeName;
    private readonly string _methodName;

    internal MethodSelector(string assemblyPath, string typeName, string methodName)
    {
        _assemblyPath = assemblyPath;
        _typeName = typeName;
        _methodName = methodName;
    }

    /// <summary>
    /// Add Task execution arguments (optional)
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public ExecutionBuilder WithArgs(params object?[] args) => new(new InvocationSpec(
        _assemblyPath,
        _typeName,
        _methodName,
        args));

    /// <summary>
    /// Execute using using default parameters
    /// </summary>
    public void Execute()
    {
        new ExecutionBuilder(
                new InvocationSpec(
                _assemblyPath,
                _typeName,
                _methodName))
            .Execute();
    }
}