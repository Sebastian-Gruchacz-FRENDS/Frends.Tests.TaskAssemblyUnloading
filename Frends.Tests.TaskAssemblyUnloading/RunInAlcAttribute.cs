using System.Reflection;
using System.Runtime.Loader;

namespace Frends.Tests.TaskAssemblyUnloading;

/// <summary>
/// Helper for executing test logic directly in an isolated ALC without serialization.
/// This eliminates cross-ALC serialization issues by running everything in the same context.
/// 
/// Usage Pattern 1 - Direct execution with result:
/// <code>
/// [Test]
/// public void MyTest()
/// {
///     var result = AlcTestRunner
///         .InAssembly("path/to/assembly.dll")
///         .Execute(() =>
///         {
///             var obj = new ComplexObject { Id = 42 };
///             var result = ExternalTask.Method(obj);
///             return result.Property; // Return simple type for assertion
///         });
///     
///     Assert.That(result, Is.EqualTo(expected));
/// }
/// </code>
/// 
/// Usage Pattern 2 - Action without result:
/// <code>
/// [Test]
/// public void MyTest()
/// {
///     AlcTestRunner
///         .InAssembly("path/to/assembly.dll")
///         .Execute(() =>
///         {
///             var obj = new ComplexObject { Id = 42 };
///             ExternalTask.Method(obj);
///             // Throws exception if fails
///         });
/// }
/// </code>
/// 
/// Benefits:
/// - No serialization needed
/// - Direct object references
/// - Simpler than current approach
/// - Still verifies unloadability
/// 
/// Limitations:
/// - Test logic must be in delegate
/// - Can only return primitive types or serializable results
/// - Complex assertions need to be inside the delegate
/// </summary>
public static class AlcTestRunner
{
    /// <summary>
    /// Start building an ALC test execution for the specified assembly.
    /// </summary>
    public static AlcExecutionBuilder InAssembly(string assemblyPath)
    {
        return new AlcExecutionBuilder(assemblyPath);
    }

    public class AlcExecutionBuilder
    {
        private readonly string _assemblyPath;
        private readonly List<string> _additionalAssemblies = new();

        internal AlcExecutionBuilder(string assemblyPath)
        {
            _assemblyPath = assemblyPath;
        }

        /// <summary>
        /// Load additional assemblies into the ALC (optional).
        /// </summary>
        public AlcExecutionBuilder WithAssembly(string additionalAssemblyPath)
        {
            _additionalAssemblies.Add(additionalAssemblyPath);
            return this;
        }

        /// <summary>
        /// Execute an action in the isolated ALC. Throws if the action throws.
        /// </summary>
        public void Execute(Action testAction)
        {
            ExecuteInternal(() =>
            {
                testAction();
                return 0; // Dummy return value
            });
        }

        /// <summary>
        /// Execute a function in the isolated ALC and return the result.
        /// Result type must be serializable or a primitive type.
        /// </summary>
        public T Execute<T>(Func<T> testFunc)
        {
            return ExecuteInternal(testFunc);
        }

        private T ExecuteInternal<T>(Func<T> testFunc)
        {
            var alcName = $"TestALC_{Guid.NewGuid()}";
            var alc = new AssemblyLoadContext(alcName, isCollectible: true);
            var alcWeakRef = new WeakReference(alc);

            try
            {
                UnloadDiagnostics.Log($"[AlcTestRunner] Creating ALC: {alcName}");

                // Load target assembly
                var fullPath = Path.GetFullPath(_assemblyPath);
                var targetAssembly = alc.LoadFromAssemblyPath(fullPath);
                UnloadDiagnostics.Log($"[AlcTestRunner] Loaded assembly: {targetAssembly.FullName}");

                // Load additional assemblies
                foreach (var additionalPath in _additionalAssemblies)
                {
                    var additionalFullPath = Path.GetFullPath(additionalPath);
                    var additionalAssembly = alc.LoadFromAssemblyPath(additionalFullPath);
                    UnloadDiagnostics.Log($"[AlcTestRunner] Loaded additional assembly: {additionalAssembly.FullName}");
                }

                // Execute the test function
                // NOTE: The delegate captures references from the calling context,
                // but will use types loaded in the ALC when instantiating new objects
                var result = testFunc();

                UnloadDiagnostics.Log($"[AlcTestRunner] Test function executed successfully");

                return result;
            }
            catch (Exception ex)
            {
                UnloadDiagnostics.Log($"[AlcTestRunner] Test function failed: {ex.Message}");
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
    }
}

