# Advanced Serialization Tests - Summary

## Tests Added

Added comprehensive tests for advanced serialization scenarios across ALC boundaries:

### 1. List<object> with Mixed Types

**Tests:**
- `SerializeListOfObjects_WithMixedTypes_ShouldWork`
- `ValidateListOfObjects_PropertiesArePreserved`

**Validates:**
- Heterogeneous collections with primitives, strings, numbers, dates, GUIDs
- Type information preservation
- Value integrity

**Example:**
```csharp
new List<object> { 42, "Hello", 123.45, DateTime.Now, Guid.NewGuid() }
```

### 2. SQL Connection Parameters

**Tests:**
- `SerializeSqlConnectionParameters_ShouldWork`
- `ValidateSqlConnectionParameters_PropertiesArePreserved`
- `SerializeDatabaseConfig_WithMultipleConnections_ShouldWork`
- `ValidateDatabaseConfig_NestedConnectionsArePreserved`

**Validates:**
- Complex configuration objects
- Nested connection objects
- Dictionary properties
- Real-world scenarios

**Example:**
```csharp
new ConnectionParameters
{
    Server = "localhost",
    Port = 1433,
    Database = "TestDB",
    Username = "sa",
    Password = "P@ssw0rd!",
    AdditionalParameters = new Dictionary<string, string>
    {
        { "ApplicationName", "MyApp" }
    }
}
```

### 3. Polymorphic Lists (List<BaseType> with Derived Instances)

**Tests:**
- `SerializePolymorphicList_WithDifferentDerivedTypes_ShouldWork`
- `ValidatePolymorphicList_InheritedPropertiesArePreserved`
- `ValidatePolymorphicList_TypesAreDistinguishable`

**Validates:**
- Polymorphic type hierarchies
- Type discriminators (`[JsonDerivedType]`)
- Derived type properties
- Type identity preservation

**Example:**
```csharp
[JsonDerivedType(typeof(Dog), typeDiscriminator: "dog")]
[JsonDerivedType(typeof(Cat), typeDiscriminator: "cat")]
[JsonDerivedType(typeof(Bird), typeDiscriminator: "bird")]
public abstract class Animal { }

var animals = new List<Animal>
{
    new Dog { Name = "Buddy", Breed = "Golden Retriever" },
    new Cat { Name = "Whiskers", IsIndoor = true },
    new Bird { Name = "Tweety", WingSpan = 15.5 }
};
```

## Implementation Changes

### ExecutionBuilder.cs

Added generic type resolution support:

1. **`ResolveTypeInAlc(AssemblyLoadContext, Type)`**
   - Recursively resolves generic type arguments
   - Handles `List<T>`, `Dictionary<K,V>`, etc.
   - Reconstructs generic types with ALC-resolved element types

2. **`ResolveNonGenericTypeInAlc(AssemblyLoadContext, Type)`**
   - Helper for resolving non-generic types

### SerializationTestTarget.cs

Added test classes:
- `Animal`, `Dog`, `Cat`, `Bird` (polymorphic hierarchy)
- `ConnectionParameters` (SQL-like configuration)
- `DatabaseConfig` (nested configuration)
- `MixedObjectContainer` (List<object> wrapper)

Added test methods:
- `EchoMixedObjects`, `ValidateMixedObjects`
- `EchoConnectionParameters`, `ValidateConnectionParameters`
- `EchoDatabaseConfig`, `ValidateDatabaseConfig`
- `EchoAnimalList`, `ValidateAnimalList`, `CountAnimalTypes`

## Test Results

**All 44 tests PASS:**
- 19 serialization tests (10 original + 9 new advanced scenarios)
- 2 ALC isolation tests
- 13 execution tests
- 10 object deserialization tests

## Key Findings

1. **Generic type resolution works:** `List<Animal>` correctly resolves to `List<ALC-Animal>`
2. **Polymorphic serialization requires `[JsonDerivedType]`:** Modern .NET approach for type discriminators
3. **Mixed type collections serialize correctly:** `List<object>` with various types works seamlessly
4. **Complex real-world objects work:** SQL connection parameters and configuration objects serialize properly
5. **No performance issues:** All tests complete quickly with proper ALC unloading

## Benefits

- ? Handles all common real-world scenarios
- ? Proper type isolation maintained
- ? Clean error messages when serialization fails
- ? Easy to debug with JSON logging
- ? No custom serialization logic needed
- ? Standard .NET serialization attributes work

## Limitations Confirmed

- Abstract classes need `[JsonDerivedType]` attributes for polymorphism
- Circular references still not supported (standard JSON limitation)
- Private fields not serialized (by design, use properties)
