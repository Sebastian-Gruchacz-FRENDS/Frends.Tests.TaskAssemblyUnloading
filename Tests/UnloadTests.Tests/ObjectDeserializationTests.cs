using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Frends.Test.TaskInjection;
using NUnit.Framework;
using FluentAssertions;
using UnloadTests.Targets;

namespace UnloadTests.Tests
{
    [TestFixture]
    public class ObjectDeserializationTests
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {

        };

        public ObjectDeserializationTests()
        {
            _options.Converters.Add(new AlcObjectConverter<object>());
        }

        class TestRecordSimple
        {
            public object Value { get; set; }
        }


        [TestCase(null)]
        [TestCase(5)]
        [TestCase(6L)]
        [TestCase(21.37)]
        [TestCase(true)]
        [TestCase("abc")]
        [TestCase('a')]
        public void DeserializingPrimitiveProperties_ShouldWork(object value)
        {
            var record = new TestRecordSimple { Value = value };

            string serialized = System.Text.Json.JsonSerializer.Serialize(record, _options);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestRecordSimple>(serialized, _options);

            deserialized.Should().BeEquivalentTo(record);
        }

        // kind of primitives
        [Test]
        public void DeserializingNonAttributablePrimitiveProperties_ShouldWork()
        {
            void Test(object value)
            {
                var record = new TestRecordSimple { Value = value };

                string serialized = System.Text.Json.JsonSerializer.Serialize(record, _options);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestRecordSimple>(serialized, _options);

                deserialized.Should().BeEquivalentTo(record);
            }

            Test(DateTime.Now);
            Test(DateTimeOffset.Now);
            Test(Guid.NewGuid());
            Test(new Point(21, 37));
            // TODO: more?
        }

        // kind of primitives
        [Test]
        public void DeserializingSimpleArrays_ShouldWork()
        {
            void Test(object[] array, string testName)
            {
                string serialized = System.Text.Json.JsonSerializer.Serialize(array, _options);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<object[]>(serialized, _options);

                try
                {
                    deserialized.Should().BeEquivalentTo(array);
                }
                catch (Exception e)
                {
                    throw new SerializationException(testName, e);
                }
            }

            // solid arrays
            Test([1, 2, 3, 4, 5], "Solid integers");
            Test([1L, 2L, 3L, 4L, 5L], "Solid longs");
            Test(["1a", "2b", "3c", "4d", "5e"], "Solid strings");

            // mixed arrays
            Test([1, 2.0, 3L, "4d", DateTime.Now], "Mixed stuff");
        }

        // kind of primitives
        [Test]
        public void DeserializingSimpleLists_ShouldWork()
        {
            void Test(List<object> collection, string testName)
            {
                string serialized = System.Text.Json.JsonSerializer.Serialize(collection, _options);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<object>>(serialized, _options);

                try
                {
                    deserialized.Should().BeEquivalentTo(collection);
                }
                catch (Exception e)
                {
                    throw new SerializationException(testName, e);
                }
            }

            // solid arrays
            Test([1, 2, 3, 4, 5], "Solid integers");
            Test([1L, 2L, 3L, 4L, 5L], "Solid longs");
            Test(["1a", "2b", "3c", "4d", "5e"], "Solid strings");

            // mixed arrays
            Test([1, 2.0, 3L, "4d", DateTime.Now], "Mixed stuff");
        }

    }
}
