using NUnit.Framework;

namespace UnloadTests.Tests;

internal static class TestAssets
{
    public static string Path =>
        System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                //"UnloadTests.Targets",
                "UnloadTests.Targets.dll"));
}