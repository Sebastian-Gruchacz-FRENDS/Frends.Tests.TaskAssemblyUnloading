namespace UnloadTests.Targets
{
    // Test classes for serialization scenarios
    public class SimpleDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Value { get; set; }
    }

    public class ComplexDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public SimpleDto? Nested { get; set; }
        public List<int>? Numbers { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    public class BaseDto
    {
        public int BaseId { get; set; }
        public string? BaseProperty { get; set; }
    }

    public class DerivedDto : BaseDto
    {
        public string? DerivedProperty { get; set; }
        public int DerivedValue { get; set; }
    }

    public class DtoWithFields
    {
        public int PublicField = 0;
        public string? PublicStringField = null;
        private int _privateField = 42;

        public int GetPrivateField() => _privateField;
    }

    // Polymorphic type hierarchy WITHOUT JSON type discriminators
    // (simulating external code that we cannot modify)
    public abstract class Animal
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public abstract string GetSound();
    }

    public class Dog : Animal
    {
        public string? Breed { get; set; }
        public override string GetSound() => "Woof";
    }

    public class Cat : Animal
    {
        public bool IsIndoor { get; set; }
        public override string GetSound() => "Meow";
    }

    public class Bird : Animal
    {
        public double WingSpan { get; set; }
        public override string GetSound() => "Tweet";
    }

    // SQL-like connection parameters (mimicking real-world scenarios)
    public class ConnectionParameters
    {
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? Database { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int ConnectionTimeout { get; set; } = 30;
        public bool IntegratedSecurity { get; set; }
        public bool Encrypt { get; set; }
        public Dictionary<string, object>? AdditionalParameters { get; set; }
    }

    public class DatabaseConfig
    {
        public ConnectionParameters? Primary { get; set; }
        public ConnectionParameters? Backup { get; set; }
        public int MaxRetries { get; set; }
        public List<string>? AllowedHosts { get; set; }
    }

    // Container for mixed object lists
    public class MixedObjectContainer
    {
        public List<object>? Items { get; set; }
        public string? Description { get; set; }
    }

    // SQL-like parameter with object Value (mimicking Frends.MicrosoftSQL.ExecuteQueryToFile.Definitions.SqlParameter)
    public class SqlParameter
    {
        public string? Name { get; set; }
        public object? Value { get; set; }
    }

    // Container with an array of class-type objects
    public class QueryInput
    {
        public string? Query { get; set; }
        public SqlParameter[]? Parameters { get; set; }
    }

    // Task methods to test
    public static class SerializationTestTarget
    {
        public static SimpleDto EchoSimple(SimpleDto input)
        {
            return input;
        }

        public static ComplexDto EchoComplex(ComplexDto input)
        {
            return input;
        }

        public static DerivedDto EchoDerived(DerivedDto input)
        {
            return input;
        }

        public static DtoWithFields EchoWithFields(DtoWithFields input)
        {
            return input;
        }

        public static bool ValidateSimple(SimpleDto input)
        {
            return input != null && 
                   input.Id > 0 && 
                   !string.IsNullOrEmpty(input.Name);
        }

        public static bool ValidateComplex(ComplexDto input)
        {
            return input != null &&
                   input.Nested != null &&
                   input.Numbers != null && input.Numbers.Count > 0 &&
                   input.Tags != null && input.Tags.Count > 0;
        }

        public static bool ValidateDerived(DerivedDto input)
        {
            return input != null &&
                   input.BaseId > 0 &&
                   !string.IsNullOrEmpty(input.BaseProperty) &&
                   !string.IsNullOrEmpty(input.DerivedProperty);
        }

        public static bool ValidateFields(DtoWithFields input)
        {
            return input != null &&
                   input.PublicField > 0 &&
                   !string.IsNullOrEmpty(input.PublicStringField);
        }

        // New: List<object> tests
        public static MixedObjectContainer EchoMixedObjects(MixedObjectContainer input)
        {
            return input;
        }

        public static bool ValidateMixedObjects(MixedObjectContainer input)
        {
            if (input?.Items == null || input.Items.Count == 0)
                return false;

            // Validate that we have different types
            bool hasInt = input.Items.Any(i => i is int);
            bool hasString = input.Items.Any(i => i is string);
            bool hasDouble = input.Items.Any(i => i is double);

            return hasInt || hasString || hasDouble;
        }

        // New: SQL Connection Parameters tests
        public static ConnectionParameters EchoConnectionParameters(ConnectionParameters input)
        {
            return input;
        }

        public static bool ValidateConnectionParameters(ConnectionParameters input)
        {
            if (input == null ||
                string.IsNullOrEmpty(input.Server) ||
                string.IsNullOrEmpty(input.Database) ||
                input.Port <= 0)
            {
                return false;
            }

            // Validate AdditionalParameters if present
            if (input.AdditionalParameters != null && input.AdditionalParameters.Count > 0)
            {
                // Check that mixed types are preserved
                foreach (var kvp in input.AdditionalParameters)
                {
                    if (kvp.Value == null)
                        continue;

                    // Verify type preservation
                    var valueType = kvp.Value.GetType();
                    if (valueType != typeof(string) &&
                        valueType != typeof(int) &&
                        valueType != typeof(long) &&
                        valueType != typeof(double) &&
                        valueType != typeof(float) &&
                        valueType != typeof(bool) &&
                        valueType != typeof(decimal))
                    {
                        return false; // Unexpected type
                    }
                }
            }

            return true;
        }

        public static DatabaseConfig EchoDatabaseConfig(DatabaseConfig input)
        {
            return input;
        }

        public static bool ValidateDatabaseConfig(DatabaseConfig input)
        {
            return input != null &&
                   input.Primary != null &&
                   !string.IsNullOrEmpty(input.Primary.Server);
        }

        // New: Polymorphic list tests
        public static List<Animal> EchoAnimalList(List<Animal> input)
        {
            return input;
        }

        public static bool ValidateAnimalList(List<Animal> input)
        {
            if (input == null || input.Count == 0)
                return false;

            // Check that we have different animal types
            bool hasDog = input.Any(a => a is Dog);
            bool hasCat = input.Any(a => a is Cat);
            bool hasBird = input.Any(a => a is Bird);

            // Verify properties are preserved
            bool allHaveNames = input.All(a => !string.IsNullOrEmpty(a.Name));
            bool allHaveAge = input.All(a => a.Age > 0);

            return (hasDog || hasCat || hasBird) && allHaveNames && allHaveAge;
        }

        public static int CountAnimalTypes(List<Animal> input)
        {
            if (input == null)
                return 0;

            var typeCount = input.Select(a => a.GetType()).Distinct().Count();
            return typeCount;
        }

        // Array serialization tests
        public static bool ValidateArrayOfParameters(QueryInput input)
        {
            if (input == null || input.Parameters == null || input.Parameters.Length == 0)
                return false;

            if (string.IsNullOrEmpty(input.Query))
                return false;

            // Verify each parameter has a name and a correctly-typed value
            foreach (var p in input.Parameters)
            {
                if (string.IsNullOrEmpty(p.Name))
                    return false;
                if (p.Value == null)
                    return false;

                // Ensure values are actual .NET types, not JsonElement
                var valueType = p.Value.GetType();
                if (valueType.FullName == "System.Text.Json.JsonElement")
                    return false;
            }

            return true;
        }

        public static bool ValidateObjectPropertyTypes(QueryInput input)
        {
            if (input?.Parameters == null || input.Parameters.Length < 3)
                return false;

            // Expect: int, string, double in that order
            return input.Parameters[0].Value is int
                && input.Parameters[1].Value is string
                && input.Parameters[2].Value is double;
        }
    }
}

