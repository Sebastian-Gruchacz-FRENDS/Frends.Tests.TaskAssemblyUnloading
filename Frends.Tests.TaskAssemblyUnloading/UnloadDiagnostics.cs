using System.Runtime.Loader;
using NUnit.Framework;

namespace Frends.Tests.TaskAssemblyUnloading;

/// <summary>
/// Simple logging during tests, expecting do be run only during tests, hence no structural logging.
/// </summary>
internal static class UnloadDiagnostics
{
    public static void Log(string message)
    {
        TestContext.WriteLine("[UnloadTest] " + message);
    }

    /// <summary>
    /// This will dump ALC state in case Unload fails for further debugging
    /// </summary>
    /// <param name="alc"></param>
    public static void DumpAssemblyLoadContext(AssemblyLoadContext alc)
    {
        Log($"ALC '{alc.Name}' state dump:");

        try
        {
            foreach (var asm in alc.Assemblies)
            {
                Log($"  Loaded: {asm.FullName}");
            }
        }
        catch (Exception ex)
        {
            Log($"  Failed to enumerate assemblies: {ex}");
        }
    }
}