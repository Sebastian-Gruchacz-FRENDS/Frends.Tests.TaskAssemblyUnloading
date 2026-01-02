using NUnit.Framework;

namespace UnloadTests.Tests;

internal static class TestAssets
{
    /// <summary>
    /// Path to the test Assembly
    /// </summary>
    /// <remarks>See project XML on how Targets are referenced - ensuring compilation and then file(s) copy</remarks>
    public static string Path =>
        System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "UnloadTests.Targets.dll"));
}