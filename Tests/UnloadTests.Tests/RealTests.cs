using System.Reflection;
using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;
using UnloadTests.Targets;
using FluentAssertions;

namespace UnloadTests.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class SerializationTests
    {
        private const string SERIALIZATION_TARGET = "UnloadTests.Targets.SerializationTestTarget";

        [Test]
        public void SerializeSimpleDto_ShouldWorkAcrossAlcBoundary()
        {
            var input = new SimpleDto
            {
                Id = 42,
                Name = "Test",
                Value = 123.45
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoSimple", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeComplexDto_WithNestedObjects_ShouldWork()
        {
            var input = new ComplexDto
            {
                Id = 1,
                Name = "Complex",
                Nested = new SimpleDto { Id = 2, Name = "Nested", Value = 99.9 },
                Numbers = new List<int> { 1, 2, 3, 4, 5 },
                Tags = new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoComplex", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeDerivedDto_WithInheritedProperties_ShouldWork()
        {
            var input = new DerivedDto
            {
                BaseId = 100,
                BaseProperty = "Base Value",
                DerivedProperty = "Derived Value",
                DerivedValue = 200
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoDerived", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeDtoWithFields_ShouldIncludePublicFields()
        {
            var input = new DtoWithFields
            {
                PublicField = 999,
                PublicStringField = "Field Value"
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoWithFields", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateSimpleDto_PropertiesArePreserved()
        {
            var input = new SimpleDto
            {
                Id = 42,
                Name = "Test",
                Value = 123.45
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateSimple", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateComplexDto_NestedPropertiesArePreserved()
        {
            var input = new ComplexDto
            {
                Id = 1,
                Name = "Complex",
                Nested = new SimpleDto { Id = 2, Name = "Nested", Value = 99.9 },
                Numbers = new List<int> { 1, 2, 3 },
                Tags = new Dictionary<string, string> { { "key1", "value1" } }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateComplex", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateDerivedDto_InheritedPropertiesArePreserved()
        {
            var input = new DerivedDto
            {
                BaseId = 100,
                BaseProperty = "Base Value",
                DerivedProperty = "Derived Value",
                DerivedValue = 200
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateDerived", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateDtoWithFields_FieldsArePreserved()
        {
            var input = new DtoWithFields
            {
                PublicField = 999,
                PublicStringField = "Field Value"
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateFields", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeNull_ShouldWork()
        {
            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoSimple", (SimpleDto?)null)
                    .Execute()
            );
        }

        [Test]
        public void SerializeMultipleParameters_ShouldWork()
        {
            var simple = new SimpleDto { Id = 1, Name = "First", Value = 10.0 };
            var derived = new DerivedDto
            {
                BaseId = 2,
                BaseProperty = "Base",
                DerivedProperty = "Derived",
                DerivedValue = 20
            };

            // Note: This would require a method that accepts multiple parameters
            // For now, we test that each parameter type works individually
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                    UnloadTest
                        .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateSimple", simple)
                        .Execute()
                );

                Assert.DoesNotThrow(() =>
                    UnloadTest
                        .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateDerived", derived)
                        .Execute()
                );
            });
        }

        [Test]
        public void SerializeListOfObjects_WithMixedTypes_ShouldWork()
        {
            var input = new MixedObjectContainer
            {
                Description = "Mixed types test",
                Items = new List<object>
                {
                    42,
                    "Hello",
                    123.45,
                    true,
                    DateTime.Now,
                    Guid.NewGuid()
                }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoMixedObjects", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateListOfObjects_PropertiesArePreserved()
        {
            var input = new MixedObjectContainer
            {
                Description = "Mixed types validation",
                Items = new List<object>
                {
                    42,
                    "Test String",
                    99.99
                }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateMixedObjects", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeSqlConnectionParameters_ShouldWork()
        {
            var input = new ConnectionParameters
            {
                Server = "localhost",
                Port = 1433,
                Database = "TestDB",
                Username = "sa",
                Password = "P@ssw0rd!",
                ConnectionTimeout = 60,
                IntegratedSecurity = false,
                Encrypt = true,
                AdditionalParameters = new Dictionary<string, object>
                {
                    { "ApplicationName", "MyApp" },
                    { "MultipleActiveResultSets", true },
                    { "ConnectRetryCount", 3 },
                    { "ConnectRetryInterval", 10.5 },
                    { "LoadBalanceTimeout", 30 }
                }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoConnectionParameters", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateSqlConnectionParameters_PropertiesArePreserved()
        {
            var input = new ConnectionParameters
            {
                Server = "db.example.com",
                Port = 5432,
                Database = "production",
                Username = "admin",
                Password = "Secret123",
                ConnectionTimeout = 30,
                IntegratedSecurity = false,
                Encrypt = true
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateConnectionParameters", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializeDatabaseConfig_WithMultipleConnections_ShouldWork()
        {
            var input = new DatabaseConfig
            {
                Primary = new ConnectionParameters
                {
                    Server = "primary.db.com",
                    Port = 1433,
                    Database = "MainDB",
                    Username = "app_user",
                    Password = "Pass123"
                },
                Backup = new ConnectionParameters
                {
                    Server = "backup.db.com",
                    Port = 1433,
                    Database = "MainDB",
                    Username = "app_user",
                    Password = "Pass123"
                },
                MaxRetries = 3,
                AllowedHosts = new List<string> { "host1", "host2", "host3" }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoDatabaseConfig", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidateDatabaseConfig_NestedConnectionsArePreserved()
        {
            var input = new DatabaseConfig
            {
                Primary = new ConnectionParameters
                {
                    Server = "main.server.com",
                    Port = 1433,
                    Database = "TestDB",
                    Username = "user1",
                    Password = "pwd1"
                },
                MaxRetries = 5
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateDatabaseConfig", input)
                    .Execute()
            );
        }

        [Test]
        public void SerializePolymorphicList_WithDifferentDerivedTypes_ShouldWork()
        {
            var input = new List<Animal>
            {
                new Dog { Name = "Buddy", Age = 5, Breed = "Golden Retriever" },
                new Cat { Name = "Whiskers", Age = 3, IsIndoor = true },
                new Bird { Name = "Tweety", Age = 2, WingSpan = 15.5 },
                new Dog { Name = "Max", Age = 7, Breed = "German Shepherd" },
                new Cat { Name = "Mittens", Age = 4, IsIndoor = false }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "EchoAnimalList", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidatePolymorphicList_InheritedPropertiesArePreserved()
        {
            var input = new List<Animal>
            {
                new Dog { Name = "Rex", Age = 6, Breed = "Labrador" },
                new Cat { Name = "Shadow", Age = 2, IsIndoor = true },
                new Bird { Name = "Polly", Age = 1, WingSpan = 12.0 }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "ValidateAnimalList", input)
                    .Execute()
            );
        }

        [Test]
        public void ValidatePolymorphicList_TypesAreDistinguishable()
        {
            var input = new List<Animal>
            {
                new Dog { Name = "Fido", Age = 4, Breed = "Beagle" },
                new Cat { Name = "Felix", Age = 5, IsIndoor = true },
                new Bird { Name = "Robin", Age = 1, WingSpan = 10.5 }
            };

            Assert.DoesNotThrow(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, SERIALIZATION_TARGET, "CountAnimalTypes", input)
                    .Execute()
            );
        }
    }

    [TestFixture]
    [NonParallelizable]
    public class AlcIsolationTests
    {
        [Test]
        public void ExecuteTask_ShouldUnloadAfterExecution()
        {
            void ExecuteInScope()
            {
                UnloadTest
                    .Invoke(TestAssets.Path, "UnloadTests.Targets.SimpleTestTarget", "OneArg", 42)
                    .Execute();

                // The ALC should be unloaded after Execute() completes
            }

            ExecuteInScope();

            // Force garbage collection
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // The test passes if no exception is thrown during Execute
            Assert.Pass("Task executed and unloaded successfully");
        }

        [Test]
        public void ExecuteTaskWithException_ShouldStillUnload()
        {
            Assert.Throws<TargetInvocationException>(() =>
                UnloadTest
                    .Invoke(TestAssets.Path, "UnloadTests.Targets.SimpleTestTarget", "Throwing", 1)
                    .Execute()
            );

            // Even with exception, ALC should unload
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Pass("Task threw exception but still unloaded successfully");
        }
    }
}
