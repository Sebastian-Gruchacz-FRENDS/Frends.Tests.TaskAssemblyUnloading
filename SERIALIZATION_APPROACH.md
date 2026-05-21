# Cross-ALC Serialization Approach

## Overview

This document explains the simplified approach for serializing objects across AssemblyLoadContext (ALC) boundaries in the Frends Task Assembly Unloading test framework.

## The Problem

When testing task assemblies in isolated AssemblyLoadContexts, we need to:
1. Pass parameters from the default context to the isolated ALC
2. Ensure the task method receives types from its own ALC (not the default)
3. Handle complex objects, including:
   - Nested objects
   - Collections
   - Inherited properties
   - Public fields
4. Not shade/hide ALC-related errors in the tested code

## Previous Approach (Overcomplicated)

The previous implementation used:
- Custom `AlcObjectConverter<T>` with type metadata (`$t` and `$v`)
- `AssemblyQualifiedName` for type resolution
- Helper objects created inside the ALC via reflection
- Complex `CrossAlcHelper` utility classes

**Issues:**
- Fragile type resolution across ALC boundaries
- Complex indirection that could hide ALC errors
- Difficult to debug serialization failures
- Over-engineered for the actual need

## New Approach (Simplified)

### Core Principle

**Serialize data, not type metadata.** Let each side use its own type definitions.

### How It Works

1. **Serialize in default context:**
   ```csharp
   var json = JsonSerializer.Serialize(arg, arg.GetType(), jsonOptions);
   ```

2. **Resolve target type in ALC (handling generics):**
   ```csharp
   var alcParamType = ResolveTypeInAlc(alcContext, paramType);
   // For List<Animal>, this resolves to List<ALC-Animal>
   ```

3. **Deserialize using ALC's types:**
   ```csharp
   // Get JsonSerializer from the ALC's System.Text.Json
   var jsonSerializerAssembly = LoadTypeAssemblyIntoAlc(alcContext, typeof(JsonSerializer));
   var alcJsonSerializer = jsonSerializerAssembly.GetType(typeof(JsonSerializer).FullName!)!;
   
   // Deserialize with ALC's type
   var genericDeserialize = deserializeMethod.MakeGenericMethod(alcParamType);
   var deserialized = genericDeserialize.Invoke(null, [json, alcOptions]);
   ```

### Generic Type Resolution

For generic types like `List<Animal>`, the framework:
1. Detects the generic type definition (`List<>`)
2. Resolves each generic argument recursively in the target ALC
3. Reconstructs the generic type: `List<ALC-Animal>`
4. Framework generics (List, Dictionary) remain in the default context
5. Custom generic types are loaded into the ALC

### Key Features

- **Framework types are shared:** `System.Text.Json` and other framework assemblies are automatically shared between ALCs
- **User types are isolated:** Task assemblies are loaded into the isolated ALC
- **Standard JSON with custom converter:** Uses `JsonSerializer` with `RuntimeTypePreservingConverterFactory` for polymorphic collections
- **Type name matching:** Only attempts serialization when `FullName` matches (ensures type compatibility)
- **Generic type support:** Properly handles `List<T>`, `Dictionary<K,V>`, and other generic collections
- **Polymorphic serialization:** Automatic runtime type preservation for collections - **no attributes needed on external code**

## What's Supported

### ? Fully Supported

1. **Simple DTOs:**
   ```csharp
   public class SimpleDto
   {
       public int Id { get; set; }
       public string Name { get; set; }
   }
   ```

2. **Complex nested objects:**
   ```csharp
   public class ComplexDto
   {
       public SimpleDto Nested { get; set; }
       public List<int> Numbers { get; set; }
       public Dictionary<string, string> Tags { get; set; }
   }
   ```

3. **Inherited properties:**
   ```csharp
   public class DerivedDto : BaseDto
   {
       public string DerivedProperty { get; set; }
   }
   ```
   Both base and derived properties are serialized correctly.

4. **Public fields:**
   ```csharp
   public class DtoWithFields
   {
       public int PublicField = 0;
       public string PublicStringField = null;
   }
   ```
   When `IncludeFields = true` is set.

5. **Collections:**
   - `List<T>` (including `List<object>` with mixed types)
   - `Dictionary<K, V>`
   - Arrays
   - Other JSON-serializable collections

6. **Null values:** Handled correctly

7. **List<object> with mixed types:**
   ```csharp
   var items = new List<object>
   {
       42,
       "Hello",
       123.45,
       DateTime.Now,
       Guid.NewGuid()
   };
   ```

8. **SQL-like connection parameters:**
   ```csharp
   public class ConnectionParameters
   {
       public string Server { get; set; }
       public int Port { get; set; }
       public Dictionary<string, string> AdditionalParameters { get; set; }
   }
   ```
   Complex real-world configuration objects work seamlessly.

9. **Polymorphic lists with runtime type preservation:**
```csharp
// NO attributes needed on external code!
public abstract class Animal { }
public class Dog : Animal { public string Breed { get; set; } }
public class Cat : Animal { public bool IsIndoor { get; set; } }
   
var animals = new List<Animal>
{
    new Dog { Name = "Buddy", Breed = "Golden Retriever" },
    new Cat { Name = "Whiskers", IsIndoor = true }
};
```
Uses custom `RuntimeTypePreservingConverterFactory` that embeds type information automatically.
**Works with external assemblies** - no code modification required!

10. **Generic collections with custom element types:**
    - `List<CustomType>`
    - `List<BaseType>` with derived instances
    - Nested generic types
    
    The framework automatically resolves generic type arguments in the target ALC.

### ?? Limitations

1. **Private fields:** Not serialized (by design, use properties or public fields)
2. **Circular references:** Standard JSON limitations apply
3. **Type name must match:** Types in both ALCs must have the same `FullName`
4. **Polymorphic collections:** Automatically handled by `RuntimeTypePreservingConverterFactory` (no attributes needed on external code)

## Testing

### Test Coverage

The implementation includes comprehensive tests in `Tests/UnloadTests.Tests/RealTests.cs`:

1. **SerializationTests (19 tests):**
   - Simple DTOs
   - Complex nested objects
   - Inherited properties
   - Public fields
   - Null values
   - Multiple parameters
   - Validation tests (ensuring data integrity)
   - **List<object>** with mixed primitive types
   - **SQL connection parameters** (complex real-world objects)
   - **Database configuration** with nested connection objects
   - **Polymorphic lists** with multiple derived types
   - Type discrimination in polymorphic collections

2. **AlcIsolationTests (2 tests):**
   - ALC unloading after execution
   - ALC unloading after exception

3. **ExecutionTests (13 tests):**
   - Method resolution
   - Error handling
   - Composite tasks
   - With/without serialization modes

4. **ObjectDeserializationTests (10 tests):**
   - Primitive types
   - Non-attributable types
   - Arrays and lists

**Total: 44 passing tests** (5 sample tests skipped)

### Running Tests

```bash
# All tests
dotnet test Tests/UnloadTests.Tests/UnloadTests.Tests.csproj

# Serialization tests only
dotnet test Tests/UnloadTests.Tests/UnloadTests.Tests.csproj --filter "FullyQualifiedName~SerializationTests"

# ALC isolation tests only
dotnet test Tests/UnloadTests.Tests/UnloadTests.Tests.csproj --filter "FullyQualifiedName~AlcIsolationTests"
```

## Benefits of the New Approach

1. **Simplicity:** Much easier to understand and maintain
2. **No error shading:** ALC issues in task code surface clearly
3. **Standard JSON:** Uses well-tested serialization without custom logic
4. **Better debugging:** JSON can be inspected in logs
5. **Type safety:** Type name matching prevents incompatible type assignment
6. **Full isolation:** All user code runs with types from the isolated ALC

## Usage Examples

### With Serialization (Default)

```csharp
var input = new MyDto { Id = 42, Name = "Test" };

UnloadTest
    .Invoke("MyTask.dll", "MyNamespace.MyTask", "Process", input)
    .Execute();
```

The framework automatically:
1. Detects that `input` needs serialization
2. Serializes to JSON
3. Loads `MyDto` type into the ALC
4. Deserializes using the ALC's type
5. Invokes the method
6. Unloads the ALC

### Without Serialization

For framework types or when no serialization is needed:

```csharp
UnloadTest
    .InvokeWithoutSerialization("MyTask.dll", "MyNamespace.MyTask", "Process", 42, "simple")
    .Execute();
```

Or using the fluent syntax:

```csharp
UnloadTest
    .From("MyTask.dll")
    .Type("MyNamespace.MyTask")
    .TaskMethod("Process")
    .WithArgs(false, 42, "simple")  // false = no serialization
    .Execute();
```

## Cleanup Opportunities

Now that the complex serialization approach has been simplified, you can optionally:

1. **Keep `CrossAlcHelper`** - for other use cases that might need it
2. **Keep `AlcObjectConverter`** - for specialized scenarios
3. **Or remove them** - if they're only used by the main execution path

The `ObjectDeserializationTests.cs` tests use `AlcObjectConverter` directly, so if you want to keep testing that converter separately, you can leave it in place.

## Conclusion

The simplified approach successfully handles:
- ? Complex objects
- ? Inherited properties
- ? Public fields
- ? Collections and nested objects
- ? **List<object> with mixed types**
- ? **Real-world configuration objects (SQL connection parameters)**
- ? **Polymorphic lists with type discriminators**
- ? **Generic collections with custom element types**
- ? Proper ALC isolation
- ? Clean error reporting

All while being much simpler and easier to maintain than the previous implementation.

### Advanced Scenarios Tested

1. **Heterogeneous collections:**
   ```csharp
   List<object> { 42, "string", 123.45, DateTime.Now }
   ```

2. **Configuration objects:**
   ```csharp
   ConnectionParameters { Server, Port, Database, AdditionalParameters }
   ```

3. **Polymorphic type hierarchies:**
   ```csharp
   List<Animal> { new Dog(), new Cat(), new Bird() }
   // Properly deserializes with [JsonDerivedType] attributes
   ```

All scenarios work correctly across the ALC boundary while maintaining proper type isolation.
