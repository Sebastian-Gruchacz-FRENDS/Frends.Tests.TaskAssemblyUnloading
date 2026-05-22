using System.Reflection;
using System.Runtime.Loader;

namespace Frends.Tests.TaskAssemblyUnloading;

/// <summary>
/// Alternative test execution API that provides cleaner syntax for ALC testing.
/// 
/// **IMPORTANT LIMITATION**: Due to how .NET ALCs work, true isolation requires reflection.
/// Delegates capture types from the calling context, so this approach is primarily for cleaner API.
/// For true isolation, use the reflection-based UnloadTest.Invoke() approach.
/// 
/// This API is useful when:
/// - You want cleaner syntax
/// - You're OK with test code running in default ALC
/// - You just need the target assembly loaded for reference
/// 
/// For true isolation where test code runs in the ALC, you need:
/// - Test code in separate assembly
/// - Reflection-based invocation
/// - Serialization for parameters
/// </summary>
public static class AlcTestRunner
{
    /// <summary>
    /// Loads assembly into ALC and executes action.
    /// NOTE: The action itself runs in default ALC, only the loaded assembly is in the ALC.
    /// This is useful for accessing types from the loaded assembly.
    /// </summary>
    public static void WithAssembly(string assemblyPath, Action testAction)
    {
        var alcName = $"TestALC_{Guid.NewGuid()}";
        var alc = new AssemblyLoadContext(alcName, isCollectible: true);
        var alcWeakRef = new WeakReference(alc);

        try
        {
            UnloadDiagnostics.Log($"[AlcTestRunner] Creating ALC: {alcName}");

            // Load target assembly
            var fullPath = Path.GetFullPath(assemblyPath);
            var targetAssembly = alc.LoadFromAssemblyPath(fullPath);
            UnloadDiagnostics.Log($"[AlcTestRunner] Loaded assembly: {targetAssembly.FullName}");

            // Execute the test action
            // NOTE: This runs in the default ALC, but can reference types from the loaded assembly
            testAction();

            UnloadDiagnostics.Log($"[AlcTestRunner] Test action executed successfully");
        }
        catch (Exception ex)
        {
            UnloadDiagnostics.Log($"[AlcTestRunner] Test action failed: {ex.Message}");
            throw;
        }
        finally
        {
            // Unload the ALC
            UnloadDiagnostics.Log($"[AlcTestRunner] Unloading ALC: {alcName}");
            alc?.Unload();

            // Verify unload
            for (int i = 0; i < 10 && alcWeakRef.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            if (alcWeakRef.IsAlive)
            {
                UnloadDiagnostics.Log($"[AlcTestRunner] WARNING: ALC did not unload!");
                throw new InvalidOperationException("AssemblyLoadContext failed to unload");
            }
            else
            {
                UnloadDiagnostics.Log($"[AlcTestRunner] ALC unloaded successfully");
            }
        }
    }

    /// <summary>
    /// Executes a method via reflection in the isolated ALC.
    /// This is similar to UnloadTest.Invoke() but with simpler syntax for common cases.
    /// </summary>
    public static TResult InvokeInAlc<TResult>(
        string assemblyPath,
        string typeName,
        string methodName,
        params object?[] args)
    {
        var alcName = $"TestALC_{Guid.NewGuid()}";
        var alc = new AssemblyLoadContext(alcName, isCollectible: true);
        var alcWeakRef = new WeakReference(alc);

        try
        {
            UnloadDiagnostics.Log($"[AlcTestRunner] Creating ALC for invocation: {alcName}");

            // Load assembly
            var fullPath = Path.GetFullPath(assemblyPath);
            var assembly = alc.LoadFromAssemblyPath(fullPath);
            
            // Get type and method
            var type = assembly.GetType(typeName, throwOnError: true)!;
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method == null)
            {
                throw new MissingMethodException($"Method '{methodName}' not found on type '{typeName}'");
            }

            // Invoke
            var result = method.Invoke(null, args);
            
            UnloadDiagnostics.Log($"[AlcTestRunner] Method invoked successfully");

            return (TResult)result!;
        }
        finally
        {
            // Unload
            alc?.Unload();
            
            for (int i = 0; i < 10 && alcWeakRef.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (alcWeakRef.IsAlive)
            {
                throw new InvalidOperationException("AssemblyLoadContext failed to unload");
            }
        }
    }
}

/// <summary>
/// Provides documentation and examples for different ALC testing approaches.
/// </summary>
public static class AlcTestingGuide
{
    /*
     * APPROACH 1: Current UnloadTest.Invoke() - Reflection with Serialization
     * =========================================================================
     * 
     * [Test]
     * public void TestWithSerialization()
     * {
     *     var input = new MyDto { Id = 42 };
     *     
     *     UnloadTest
     *         .Invoke(assemblyPath, "MyTask", "Process", input)
     *         .Execute();
     * }
     * 
     * Pros:
     * - True isolation - everything runs in ALC
     * - Automatically serializes parameters
     * - Tests unloadability
     * 
     * Cons:
     * - Requires serialization for complex types
     * - Polymorphic types need special handling
     * - Cannot directly debug into task code
     * 
     * 
     * APPROACH 2: AlcTestRunner.WithAssembly() - Reference Types
     * ============================================================
     * 
     * [Test]
     * public void TestWithTypeReference()
     * {
     *     AlcTestRunner.WithAssembly(assemblyPath, () =>
     *     {
     *         // Can reference types from loaded assembly
     *         // But code runs in default ALC
     *         var result = SomeStaticMethod();
     *         Assert.That(result, Is.EqualTo(expected));
     *     });
     * }
     * 
     * Pros:
     * - Cleaner syntax for simple cases
     * - Can use complex types without serialization
     * - Easy to debug
     * 
     * Cons:
     * - Code doesn't actually run in ALC
     * - Doesn't test true isolation
     * - Type mixing between ALCs possible
     * 
     * 
     * APPROACH 3: Direct Type Usage (No Isolation)
     * =============================================
     * 
     * [Test]
     * public void TestDirect()
     * {
     *     // Just reference the task project normally
     *     var input = new MyDto { Id = 42 };
     *     var result = MyTask.Process(input);
     *     Assert.That(result.Success, Is.True);
     * }
     * 
     * Pros:
     * - Simplest to write
     * - Easy to debug
     * - No serialization needed
     * 
     * Cons:
     * - Doesn't test unloadability
     * - Doesn't test isolation
     * - Not suitable for plugin scenarios
     * 
     * 
     * RECOMMENDATION
     * ==============
     * 
     * Use APPROACH 1 (UnloadTest.Invoke with serialization) when:
     * - Testing plugin/task unloadability
     * - Need true isolation
     * - Simulating real plugin loading scenario
     * 
     * Use APPROACH 3 (direct reference) when:
     * - Testing business logic only
     * - Unloadability not a concern
     * - Faster test execution needed
     * 
     * Use APPROACH 2 only for specific cases where you need type references
     * but not full isolation.
     */
}


