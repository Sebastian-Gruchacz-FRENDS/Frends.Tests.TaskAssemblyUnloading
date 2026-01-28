using System.Reflection;
using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;
using UnloadTests.Targets;

namespace UnloadTests.Tests;

[TestFixture]
[NonParallelizable]
public class ExecutionTests
{
    private const string SIMPLE_TARGET = @"UnloadTests.Targets.SimpleTestTarget";
    private const string COMPOSITE_TARGET = @"UnloadTests.Targets.ComplexTargetGuidTask";

    [Test]
    public void Executes_Method_With_NoArgs()
    {
        // Task Methods without args are not allowed
        Assert.Throws<MissingMethodException>(() =>
            UnloadTest
            .Invoke(TestAssets.Path, SIMPLE_TARGET, "NoArgs")
            .Execute());
    }

    [Test]
    public void Executes_Method_With_Args()
    {
        UnloadTest
            .Invoke(TestAssets.Path, SIMPLE_TARGET, "OneArg", 5)
            .Execute();
    }

    [Test]
    public void ExecutesMethodWithArgs_Using_LongerSyntax()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SIMPLE_TARGET)
            .TaskMethod("OneArg")
            .WithArgs(false, 5) // no serialization need
            .Execute();
    }

    [Test]
    public void ExecutesMethodWithArgs_UsingTypedSyntax_DoesNotConflictWithUnload()
    {
        UnloadTest
            .FromType(typeof(ComplexTargetGuidTask))
            .TaskMethod(nameof(ComplexTargetGuidTask.GenerateGuidV3))
            .ExecuteWithoutSerialization();
    }

    [Test]
    public void Executes_With_Default_Arguments()
    {
        // When not providing arguments - but there is no ambiguous methods - execution with default values will be attempted
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
        // When not providing arguments - ambiguity in method calls cannot be resolved (same method name, different parameters)
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

    [Test]
    public void CompositeTasks_WithDefaults_ShouldRunAndUnloadProperly()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => UnloadTest
                .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV1")
                .Execute()
            );

            Assert.DoesNotThrow(() => UnloadTest
                .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV3")
                .Execute()
            );

            Assert.DoesNotThrow(() => UnloadTest
                .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV4")
                .Execute()
            );
        });
    }

    [Test]
    public void CompositeTasks_WithEmbeddedParameters_ShouldThrowBecauseOfWrongAssembly()
    {
        // Nested types will not be serialized-deserialized across the ALC boundary - ALC is part of the typedef. Have to use one of:
        //  - primitive types
        //  - shared library types
        //  - test calls without params
        //  - regular Invoke(), that uses boundary serialization (default)

        Assert.Throws<MissingMethodException>(() => UnloadTest
            .InvokeWithoutSerialization(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV1", new TimeBasedGuidParameters(), new Options(), CancellationToken.None)
            .Execute()
        );

        Assert.Throws<MissingMethodException>(() => UnloadTest
            .InvokeWithoutSerialization(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV3", new NameBasedGuidParameters(), new Options(), CancellationToken.None)
            .Execute()
        );
    }

    [Test]
    public void CompositeTasks_WithEmbeddedParameters_WithSerializationDisabled_ShouldThrowBecauseOfWrongAssembly()
    {
        Assert.DoesNotThrow(() => UnloadTest
            .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV1", new TimeBasedGuidParameters(), new Options(), CancellationToken.None)
            .Execute()
        );

        Assert.DoesNotThrow(() => UnloadTest
            .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV3", new NameBasedGuidParameters(), new Options(), CancellationToken.None)
            .Execute()
        );
    }
}