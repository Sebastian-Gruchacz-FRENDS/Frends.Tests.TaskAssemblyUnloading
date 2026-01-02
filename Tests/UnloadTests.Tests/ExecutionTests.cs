using System.Reflection;
using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;

namespace UnloadTests.Tests;

[TestFixture]
[NonParallelizable]
public class ExecutionTests
{
    private const string SIMPLE_TARGET = @"UnloadTests.Targets.SimpleTestTarget";


    [Test]
    public void Executes_Method_With_NoArgs()
    {
        UnloadTest
            .Invoke(TestAssets.Path, SIMPLE_TARGET, "NoArgs")
            .Execute();
    }

    [Test]
    public void Executes_Method_With_Args()
    {
        UnloadTest
            .Invoke(TestAssets.Path, SIMPLE_TARGET, "OneArg", 5)
            .Execute();
    }

    [Test]
    public void Executes_With_Default_Arguments()
    {
        UnloadTest
            .Invoke(TestAssets.Path, SIMPLE_TARGET, "Defaults")
            .Execute();
    }

    [Test]
    public void Throws_On_Missing_Assembly()
    {
        Assert.Throws<FileNotFoundException>(() =>
            UnloadTest.Invoke("missing.dll", "X", "Y").Execute());
    }

    [Test]
    public void Throws_On_Missing_Type()
    {
        Assert.Throws<TypeLoadException>(() =>
            UnloadTest.Invoke(TestAssets.Path, "Nope.Type", "X").Execute());
    }

    [Test]
    public void Throws_On_Missing_Method()
    {
        Assert.Throws<MissingMethodException>(() =>
            UnloadTest.Invoke(TestAssets.Path, SIMPLE_TARGET, "Nope").Execute());
    }

    [Test]
    public void Throws_On_Ambiguous_Method()
    {
        Assert.Throws<AmbiguousMatchException>(() =>
            UnloadTest.Invoke(TestAssets.Path, SIMPLE_TARGET, "OneArg").Execute());
    }

    [Test]
    public void Throws_When_Method_Throws()
    {
        var ex = Assert.Throws<TargetInvocationException>(() =>
            UnloadTest.Invoke(TestAssets.Path, SIMPLE_TARGET, "Throwing").Execute());

        Assert.That(ex!.InnerException, Is.TypeOf<InvalidOperationException>());
    }
}