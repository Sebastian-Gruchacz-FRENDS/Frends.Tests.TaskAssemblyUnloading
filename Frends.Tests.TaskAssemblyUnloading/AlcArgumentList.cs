using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;

namespace Frends.Tests.TaskAssemblyUnloading;

/// <summary>
/// Records object construction instructions that can be replayed inside a target ALC.
/// This avoids serialization entirely - objects are created directly in the correct context.
/// 
/// Usage (type-safe, no magic strings):
/// <code>
/// UnloadTest
///     .From(path)
///     .Type("MyTask")
///     .TaskMethod("Process")
///     .WithArgs(args => args
///         .New&lt;MyDto&gt;(b => b
///             .Set(x => x.Id, 42)
///             .Set(x => x.Name, "Test"))
///         .New&lt;Options&gt;(b => b
///             .Set(x => x.Format, Format.N))
///         .Value(CancellationToken.None))
///     .Execute();
/// </code>
/// </summary>
public class AlcArgumentList
{
    private readonly List<IArgumentFactory> _factories = new();

    /// <summary>
    /// Create a new instance of T with type-safe property configuration.
    /// </summary>
    public AlcArgumentList New<T>(Action<ObjectBuilder<T>> configure) where T : class
    {
        var builder = new ObjectBuilder<T>();
        configure(builder);
        _factories.Add(builder);
        return this;
    }

    /// <summary>
    /// Create a new instance of T with default values (no configuration).
    /// </summary>
    public AlcArgumentList New<T>() where T : class
    {
        _factories.Add(new ObjectBuilder<T>());
        return this;
    }

    /// <summary>
    /// Create a List&lt;T&gt; argument with items constructed in the target ALC.
    /// Supports polymorphic items via Add&lt;TDerived&gt;.
    /// </summary>
    public AlcArgumentList NewList<TItem>(Action<CollectionBuilder<TItem>> configure)
    {
        var builder = new CollectionBuilder<TItem>();
        configure(builder);
        _factories.Add(builder);
        return this;
    }

    /// <summary>
    /// Create a TItem[] array argument with items constructed in the target ALC.
    /// </summary>
    public AlcArgumentList NewArray<TItem>(Action<CollectionBuilder<TItem>> configure)
    {
        var builder = new CollectionBuilder<TItem>();
        configure(builder);
        builder.AsArray();
        _factories.Add(builder);
        return this;
    }

    /// <summary>
    /// Pass a primitive/framework value directly (no ALC boundary issue for these).
    /// </summary>
    public AlcArgumentList Value(object? value)
    {
        _factories.Add(new DirectValue(value));
        return this;
    }

    /// <summary>
    /// Create arguments inside the specified ALC by replaying the recorded instructions.
    /// </summary>
    internal object?[] Materialize(AssemblyLoadContext alc)
    {
        var args = new object?[_factories.Count];
        for (int i = 0; i < _factories.Count; i++)
        {
            args[i] = _factories[i].Create(alc);
        }
        return args;
    }

    internal int Count => _factories.Count;
}

/// <summary>
/// Type-safe builder that records property assignments via expressions.
/// Replayed via reflection in the target ALC.
/// </summary>
public class ObjectBuilder<T> : IArgumentFactory
{
    private readonly Type _sourceType = typeof(T);
    private readonly List<(string Name, object? Value)> _properties = new();
    private readonly List<(string Name, IArgumentFactory Nested)> _nestedProperties = new();

    /// <summary>
    /// Set a primitive/value-type property using an expression for type safety.
    /// </summary>
    public ObjectBuilder<T> Set<TProp>(Expression<Func<T, TProp>> property, TProp value)
    {
        var name = GetMemberName(property);
        _properties.Add((name, value));
        return this;
    }

    /// <summary>
    /// Set a nested object property, configured with its own type-safe builder.
    /// </summary>
    public ObjectBuilder<T> Set<TProp>(Expression<Func<T, TProp?>> property, Action<ObjectBuilder<TProp>> configure) where TProp : class
    {
        var name = GetMemberName(property);
        var nested = new ObjectBuilder<TProp>();
        configure(nested);
        _nestedProperties.Add((name, nested));
        return this;
    }

    /// <summary>
    /// Set a collection property with items built in the ALC.
    /// </summary>
    public ObjectBuilder<T> SetList<TItem>(Expression<Func<T, IEnumerable<TItem>?>> property, Action<CollectionBuilder<TItem>> configure)
    {
        var name = GetMemberName(property);
        var collectionBuilder = new CollectionBuilder<TItem>();
        configure(collectionBuilder);
        _nestedProperties.Add((name, collectionBuilder));
        return this;
    }

    /// <summary>
    /// Set a collection property with primitive/value-type items.
    /// </summary>
    public ObjectBuilder<T> SetValues<TItem>(Expression<Func<T, IEnumerable<TItem>?>> property, params TItem[] items)
    {
        var name = GetMemberName(property);
        var collectionBuilder = new CollectionBuilder<TItem>();
        foreach (var item in items)
        {
            collectionBuilder.AddValue(item);
        }
        _nestedProperties.Add((name, collectionBuilder));
        return this;
    }

    /// <summary>
    /// Set an array property (TItem[]) with items built in the ALC.
    /// Materializes each item inside the target ALC, then emits a TItem[] so it can be
    /// assigned to array-typed properties (e.g. SqlParameter[]).
    /// </summary>
    public ObjectBuilder<T> SetArray<TItem>(Expression<Func<T, TItem[]?>> property, Action<CollectionBuilder<TItem>> configure)
    {
        var name = GetMemberName(property);
        var collectionBuilder = new CollectionBuilder<TItem>();
        configure(collectionBuilder);
        collectionBuilder.AsArray();
        _nestedProperties.Add((name, collectionBuilder));
        return this;
    }

    public object? Create(AssemblyLoadContext alc)
    {
        var alcType = AlcTypeResolver.ResolveTypeInAlc(alc, _sourceType);
        var instance = Activator.CreateInstance(alcType)!;

        foreach (var (name, value) in _properties)
        {
            SetMember(alc, alcType, instance, name, value);
        }

        foreach (var (name, factory) in _nestedProperties)
        {
            var prop = alcType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(instance, factory.Create(alc));
            }
            else
            {
                var field = alcType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(instance, factory.Create(alc));
                }
            }
        }

        return instance;
    }

    private static void SetMember(AssemblyLoadContext alc, Type alcType, object instance, string name, object? value)
    {
        var prop = alcType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            var targetValue = AlcTypeResolver.ConvertValueForAlc(alc, value, prop.PropertyType);
            prop.SetValue(instance, targetValue);
        }
        else
        {
            var field = alcType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var targetValue = AlcTypeResolver.ConvertValueForAlc(alc, value, field.FieldType);
                field.SetValue(instance, targetValue);
            }
        }
    }

    private static string GetMemberName<TObj, TProp>(Expression<Func<TObj, TProp>> expression)
    {
        return expression.Body switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            _ => throw new ArgumentException($"Expression must be a member access, got: {expression.Body.NodeType}")
        };
    }
}

/// <summary>
/// Builds a collection (List&lt;T&gt;) of items inside the target ALC.
/// Supports both complex objects (via ObjectBuilder) and primitive values.
/// </summary>
public class CollectionBuilder<TItem> : IArgumentFactory
{
    private readonly Type _itemType = typeof(TItem);
    private readonly List<IArgumentFactory> _items = new();
    private bool _emitAsArray;

    /// <summary>
    /// Emit the materialized collection as a TItem[] array instead of a List&lt;TItem&gt;.
    /// Use this when the target property/parameter is an array type.
    /// </summary>
    internal CollectionBuilder<TItem> AsArray()
    {
        _emitAsArray = true;
        return this;
    }

    /// <summary>
    /// Add an item of the base type, configured with a type-safe builder.
    /// </summary>
    public CollectionBuilder<TItem> Add(Action<ObjectBuilder<TItem>> configure)
    {
        var builder = new ObjectBuilder<TItem>();
        configure(builder);
        _items.Add(builder);
        return this;
    }

    /// <summary>
    /// Add a derived-type item to the collection.
    /// </summary>
    public CollectionBuilder<TItem> Add<TDerived>(Action<ObjectBuilder<TDerived>> configure) where TDerived : class, TItem
    {
        var builder = new ObjectBuilder<TDerived>();
        configure(builder);
        _items.Add(builder);
        return this;
    }

    /// <summary>
    /// Add a primitive/value item directly.
    /// </summary>
    public CollectionBuilder<TItem> AddValue(TItem? value)
    {
        _items.Add(new DirectValue(value));
        return this;
    }

    public object? Create(AssemblyLoadContext alc)
    {
        var alcItemType = AlcTypeResolver.ResolveTypeInAlc(alc, _itemType);

        if (_emitAsArray)
        {
            var array = Array.CreateInstance(alcItemType, _items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                array.SetValue(_items[i].Create(alc), i);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(alcItemType);
        var list = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod("Add")!;

        foreach (var itemFactory in _items)
        {
            addMethod.Invoke(list, [itemFactory.Create(alc)]);
        }

        return list;
    }
}

internal static class AlcTypeResolver
{
    internal static Type ResolveTypeInAlc(AssemblyLoadContext alc, Type type)
    {
        var asm = type.Assembly;
        var asmName = asm.GetName().Name;

        if (asmName != null && (asmName.StartsWith("System.") || asmName.StartsWith("Microsoft.") ||
            asmName == "System.Private.CoreLib" || asmName == "mscorlib"))
        {
            return type;
        }

        var alcAsm = alc.Assemblies.FirstOrDefault(a => a.GetName().Name == asmName);
        if (alcAsm == null)
        {
            alcAsm = alc.LoadFromAssemblyPath(asm.Location);
        }

        return alcAsm.GetType(type.FullName!)!;
    }

    internal static object? ConvertValueForAlc(AssemblyLoadContext alc, object? value, Type targetType)
    {
        if (value == null) return null;

        var valueType = value.GetType();

        if (valueType.IsPrimitive || valueType == typeof(string) ||
            valueType == typeof(DateTime) || valueType == typeof(Guid) ||
            valueType == typeof(DateTimeOffset) || valueType == typeof(TimeSpan) ||
            valueType == typeof(decimal))
        {
            return value;
        }

        if (valueType.IsEnum)
        {
            var alcEnumType = ResolveTypeInAlc(alc, targetType);
            return Enum.Parse(alcEnumType, value.ToString()!);
        }

        return value;
    }
}

internal class DirectValue : IArgumentFactory
{
    private readonly object? _value;
    public DirectValue(object? value) => _value = value;
    public object? Create(AssemblyLoadContext alc) => _value;
}

internal interface IArgumentFactory
{
    object? Create(AssemblyLoadContext alc);
}
