using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;
using UnloadTests.Targets;

namespace UnloadTests.Tests;

/// <summary>
/// Tests for the builder-based argument creation approach.
/// Objects are constructed directly in the target ALC via reflection - NO serialization needed.
/// Expression-based API provides compile-time safety with no magic strings.
/// </summary>
[TestFixture]
[NonParallelizable]
public class CallbackBuilderTests
{
    private const string COMPOSITE_TARGET = "UnloadTests.Targets.ComplexTargetGuidTask";
    private const string SERIALIZATION_TARGET = "UnloadTests.Targets.SerializationTestTarget";

    [Test]
    public void BuilderArgs_SimpleDto_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateSimple")
            .WithArgs(args => args
                .New<SimpleDto>(b => b
                    .Set(x => x.Id, 42)
                    .Set(x => x.Name, "Test")
                    .Set(x => x.Value, 123.45)))
            .Execute();
    }

    [Test]
    public void BuilderArgs_DerivedDto_InheritedPropertiesWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateDerived")
            .WithArgs(args => args
                .New<DerivedDto>(b => b
                    .Set(x => x.BaseId, 100)
                    .Set(x => x.BaseProperty, "Base Value")
                    .Set(x => x.DerivedProperty, "Derived Value")
                    .Set(x => x.DerivedValue, 200)))
            .Execute();
    }

    [Test]
    public void BuilderArgs_WithEnumProperty_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(COMPOSITE_TARGET)
            .TaskMethod("GenerateGuidV1")
            .WithArgs(args => args
                .New<TimeBasedGuidParameters>(b => b
                    .Set(x => x.UseMacAddress, true))
                .New<Options>(b => b
                    .Set(x => x.Format, Format.N))
                .Value(CancellationToken.None))
            .Execute();
    }

    [Test]
    public void BuilderArgs_MultipleParameters_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(COMPOSITE_TARGET)
            .TaskMethod("GenerateGuidV3")
            .WithArgs(args => args
                .New<NameBasedGuidParameters>(b => b
                    .Set(x => x.NamespaceGuid, Guid.NewGuid().ToString())
                    .Set(x => x.InputString, "hello"))
                .New<Options>(b => b
                    .Set(x => x.Format, Format.P))
                .Value(CancellationToken.None))
            .Execute();
    }

    [Test]
    public void BuilderArgs_WithFields_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateFields")
            .WithArgs(args => args
                .New<DtoWithFields>(b => b
                    .Set(x => x.PublicField, 999)
                    .Set(x => x.PublicStringField, "Field Value")))
            .Execute();
    }

    [Test]
    public void BuilderArgs_WithPrimitiveValue_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type("UnloadTests.Targets.SimpleTestTarget")
            .TaskMethod("TwoArgs")
            .WithArgs(args => args
                .Value(42)
                .Value("hello"))
            .Execute();
    }

    [Test]
    public void BuilderArgs_ComparedToSerializationApproach_BothWork()
    {
        // Serialization approach (existing)
        Assert.DoesNotThrow(() => UnloadTest
            .Invoke(TestAssets.Path, COMPOSITE_TARGET, "GenerateGuidV1",
                new TimeBasedGuidParameters { UseMacAddress = true },
                new Options { Format = Format.N },
                CancellationToken.None)
            .Execute());

        // Builder approach (new - no serialization!)
        Assert.DoesNotThrow(() => UnloadTest
            .From(TestAssets.Path)
            .Type(COMPOSITE_TARGET)
            .TaskMethod("GenerateGuidV1")
            .WithArgs(args => args
                .New<TimeBasedGuidParameters>(b => b.Set(x => x.UseMacAddress, true))
                .New<Options>(b => b.Set(x => x.Format, Format.N))
                .Value(CancellationToken.None))
            .Execute());
    }

    [Test]
    public void BuilderArgs_NestedObject_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateComplex")
            .WithArgs(args => args
                .New<ComplexDto>(b => b
                    .Set(x => x.Id, 1)
                    .Set(x => x.Name, "Parent")
                    .Set(x => x.Nested, n => n
                        .Set(x => x.Id, 10)
                        .Set(x => x.Name, "Child")
                        .Set(x => x.Value, 3.14))
                    .SetValues(x => x.Numbers, 1, 2, 3)))
            .Execute();
    }

    [Test]
    public void BuilderArgs_NestedDatabaseConfig_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateDatabaseConfig")
            .WithArgs(args => args
                .New<DatabaseConfig>(b => b
                    .Set(x => x.MaxRetries, 3)
                    .Set(x => x.Primary, p => p
                        .Set(x => x.Server, "localhost")
                        .Set(x => x.Port, 5432)
                        .Set(x => x.Database, "mydb")
                        .Set(x => x.Username, "admin")
                        .Set(x => x.Password, "secret")
                        .Set(x => x.ConnectionTimeout, 60))))
            .Execute();
    }

    [Test]
    public void BuilderArgs_PolymorphicList_ShouldWork()
    {
        UnloadTest
            .From(TestAssets.Path)
            .Type(SERIALIZATION_TARGET)
            .TaskMethod("ValidateAnimalList")
            .WithArgs(args => args
                .NewList<Animal>(list => list
                    .Add<Dog>(b => b
                        .Set(x => x.Name, "Rex")
                        .Set(x => x.Age, 5)
                        .Set(x => x.Breed, "Labrador"))
                    .Add<Cat>(b => b
                        .Set(x => x.Name, "Whiskers")
                        .Set(x => x.Age, 3)
                        .Set(x => x.IsIndoor, true))
                    .Add<Bird>(b => b
                        .Set(x => x.Name, "Tweety")
                        .Set(x => x.Age, 2)
                        .Set(x => x.WingSpan, 0.3))))
            .Execute();
    }
}
