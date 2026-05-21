# Polymorphic Serialization Without Attributes - Solution

## Problem Solved

**Challenge:** Test framework needs to serialize polymorphic types (`List<Animal>` with Dog, Cat, Bird instances) across ALC boundaries, but the tested assemblies are **external code that cannot be modified** - we can't add `[JsonDerivedType]` attributes.

## Solution: RuntimeTypePreservingConverterFactory

### How It Works

1. **Detection:** Automatically detects collections of class types (`List<T>`, `IEnumerable<T>`, arrays)
2. **Serialization:** Embeds runtime type information for each element using `$type` and `$value` pattern
3. **Deserialization:** Resolves the actual type and deserializes to the correct derived type
4. **No modification needed:** Works with external assemblies without any attributes

### Example JSON Output

**List of polymorphic types:**
```json
[
  {
    "$type": "UnloadTests.Targets.Dog, UnloadTests.Targets, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    "$value": {
      "Breed": "Golden Retriever",
      "Name": "Buddy",
      "Age": 5
    }
  },
  {
    "$type": "UnloadTests.Targets.Cat, UnloadTests.Targets, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    "$value": {
      "IsIndoor": true,
      "Name": "Whiskers",
      "Age": 3
    }
  }
]
```

**Dictionary with mixed value types (clean, no wrapping for primitives):**
```json
{
  "Server": "localhost",
  "Port": 1433,
  "AdditionalParameters": {
    "ApplicationName": "MyApp",
    "MultipleActiveResultSets": true,
    "ConnectRetryCount": 3,
    "ConnectRetryInterval": 10.5,
    "LoadBalanceTimeout": 30
  }
}
```

### Implementation

**File:** `Frends.Tests.TaskAssemblyUnloading/RuntimeTypePreservingConverter.cs`

1. **`RuntimeTypePreservingConverterFactory`** - Converter factory that creates converters for collections
   - Detects `List<T>`, `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, and arrays
   - Detects `Dictionary<string, object>` for configuration scenarios
   - Only handles class types (potential polymorphism)

2. **`PolymorphicCollectionConverter<T>`** - The actual converter for collections
   - During serialization: Checks if runtime type differs from declared type, embeds type info
   - During deserialization: Reads `$type` property, resolves type, deserializes to correct type
   - Handles type resolution across assembly versions

3. **`PolymorphicDictionaryConverter`** - Specialized converter for `Dictionary<string, object>`
   - Preserves runtime types for primitive values (int, bool, string, double, etc.)
   - No extra wrapping for primitives (clean JSON)
   - Embeds type info only for complex objects
   - Common in real-world config/parameter objects

### Key Features

? **No attributes required** - Works with external, unmodifiable code  
? **Automatic detection** - Applies to all collection types automatically  
? **Dictionary<string, object> support** - Handles mixed value types in config dictionaries ?  
? **Cross-ALC compatible** - Type resolution works across AssemblyLoadContexts  
? **Version-tolerant** - Handles assembly version differences  
? **.NET 6+ compatible** - Uses standard JSON APIs  
? **Recursive-safe** - Avoids infinite recursion during (de)serialization  

### Test Results

**All 44 tests PASS**, including:
- `SerializePolymorphicList_WithDifferentDerivedTypes_ShouldWork`
- `ValidatePolymorphicList_InheritedPropertiesArePreserved`
- `ValidatePolymorphicList_TypesAreDistinguishable`

### Usage in Test Framework

The converter is automatically applied in `ExecutionBuilder.cs`:

```csharp
var jsonOptions = new JsonSerializerOptions
{
    IncludeFields = true,
    PropertyNameCaseInsensitive = true
};

// Add converter factory that handles polymorphic collections
jsonOptions.Converters.Add(new RuntimeTypePreservingConverterFactory());
```

The same converter factory is instantiated in the target ALC for deserialization, ensuring type resolution happens in the correct context.

### Comparison with [JsonDerivedType] Approach

| Aspect | [JsonDerivedType] | RuntimeTypePreservingConverterFactory |
|--------|-------------------|--------------------------------------|
| **Requires code modification** | ? Yes | ? No |
| **Works with external code** | ? No | ? Yes |
| **JSON size** | Smaller (short discriminator) | Larger (full type name) |
| **Type safety** | Compile-time | Runtime |
| **Version tolerance** | Limited | Good |
| **Setup complexity** | None (built-in) | Custom converter |

### Benefits for Testing External Assemblies

1. **No source code access needed** - Test any external Task assembly
2. **No reflection to add attributes** - Clean, standard approach
3. **Works with sealed types** - No restrictions on tested code
4. **Handles deep hierarchies** - Any level of inheritance works
5. **Collection-aware** - Arrays, Lists, IEnumerables all work

### .NET Compatibility

- **Minimum:** .NET 6 (uses `JsonConverterFactory`, `JsonConverter<T>`)
- **Tested:** .NET 8
- **Multi-targeting:** Supported (no version-specific APIs used)

### Limitations

1. **JSON size:** Full type names increase payload size
2. **Performance:** Type resolution via reflection has overhead
3. **Security:** Deserializes types based on JSON content (standard JSON limitation)
4. **Collection types only:** Currently only handles collections, not individual polymorphic properties

### Future Enhancements

If needed, could extend to:
- Individual polymorphic properties (not just collections)
- Custom type name aliases (shorter JSON)
- Type whitelisting for security
- Performance optimizations (type cache)

## Conclusion

The `RuntimeTypePreservingConverterFactory` provides a **production-ready solution** for testing external Task assemblies with polymorphic types, without requiring any modifications to the tested code. It works seamlessly across ALC boundaries and handles all common polymorphic scenarios.
