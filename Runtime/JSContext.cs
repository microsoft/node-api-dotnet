using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NodeApi;

/// <summary>
/// Manages JavaScript interop context for the lifetime of a module.
/// </summary>
/// <remarks>
/// A <see cref="JSContext"/> instance is constructed when the module is loaded and disposed when
/// the module is unloaded. The context tracks several kinds of JS references used internally
/// by this assembly, so that the references can be re-used for the lifetime of the module and
/// disposed when the module is unloaded.
/// </remarks>
public sealed class JSContext : IDisposable
{
    public JSContext()
    {
        JSNativeApi.Interop.Initialize();
    }

    // Track JS constructors and instance JS wrappers for exported classes, enabling
    // .NET objects to be automatically wrapped when returned to JS, and re-wrapped as needed
    // if the (weakly-referenced) JS wrapper has been released.

    // TODO: Consider an optimization that avoids dictionary lookups:
    // If an exported class is declared as partial, then the generator can add a
    // static `JSConstructor` property and an instance `JSWrapper` property to the class.
    // (The dictionary mappings could still be used to export external/non-partial classes.)

    /// <summary>
    /// Maps from exported class types to (strong references to) JS constructors for each class.
    /// </summary>
    /// <remarks>
    /// Used to automatically construct a JS wrapper object with correct prototype whenever
    /// a class instance is marshalled from C# to JS.
    /// </remarks>
    private readonly ConcurrentDictionary<Type, JSReference> _classMap = new();

    /// <summary>
    /// Maps from C# objects to (weak references to) JS wrappers for each object.
    /// </summary>
    /// <remarks>
    /// Enables re-using the same JS wrapper objects for the same C# objects, so that
    /// a C# object maps to the same JS instance when marshalled multiple times. The
    /// references are weak to allow the JS wrappers to be released; new wrappers are
    /// re-constructed as needed.
    /// </remarks>
    private readonly ConcurrentDictionary<object, JSReference> _objectMap = new();

    /// <summary>
    /// Maps from exported struct types to (strong references to) JS constructors for classes
    /// that represent each struct.
    /// </summary>
    /// <remarks>
    /// Used to automatically construct a JS object with correct prototype whenever
    /// a struct is marshalled from C# to JS. Since structs are marshalled by value,
    /// the JS object is not a wrapper, rather the properties are copied by the marshaller.
    /// </remarks>
    private readonly ConcurrentDictionary<Type, JSReference> _structMap = new();

    /// <summary>
    /// Maps from JS class names to (strong references to) JS constructors for classes imported
    /// from JS to C#.
    /// </summary>
    /// <remarks>
    /// Enables C# code to construct instances of built-in JS classes, without having to resolve
    /// the constructors every time.
    /// </remarks>
    private readonly ConcurrentDictionary<string, JSReference> _importMap = new();

    /// <summary>
    /// Collection of references tracked for any purpose that have the lifetime of the context.
    /// </summary>
    private readonly ConcurrentBag<JSReference> _trackedReferences = new();

    /// <summary>
    /// Registers a class JS constructor, enabling automatic JS wrapping of instances of the class.
    /// </summary>
    /// <param name="constructorFunction">JS class constructor function returned from
    /// <see cref="JSNativeApi.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal JSValue RegisterClass<T>(JSValue constructorFunction) where T : class
    {
        _classMap.AddOrUpdate(
            typeof(T),
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Class already registered for JS export: " + typeof(T)));
        return constructorFunction;
    }

    /// <summary>
    /// Gets a class JS constructor that was previously registered.
    /// </summary>
    private JSValue GetClassConstructor<T>() where T : class
    {
        if (!_classMap.TryGetValue(typeof(T), out JSReference? constructorReference))
        {
            throw new InvalidOperationException(
                "Class not registered for JS export: " + typeof(T));
        }

        JSValue? constructorFunction = constructorReference!.GetValue();
        if (!constructorFunction.HasValue)
        {
            // This should never happen because the reference is "strong".
            throw new InvalidOperationException("Failed to resolve class constructor reference.");
        }

        return constructorFunction.Value;
    }

    /// <summary>
    /// Attaches an object to a JS wrapper, and saves a weak reference to the wrapper.
    /// </summary>
    /// <param name="wrapper">JS object passed as the 'this' argument to the constructor callback
    /// for <see cref="JSNativeApi.DefineClass"/>.</param>
    /// <param name="obj">New or existing instance of the class to be wrapped.</param>
    /// <returns>The JS wrapper.</returns>
    internal unsafe JSValue InitializeObjectWrapper<T>(JSValue wrapper, T obj) where T : class
    {
        // The reference returned by Wrap() is weak (refcount=0), which is good:
        // if the JS object is released then the reference will fail to resolve, and
        // GetOrCreateObjectWrapper() will create a new JS wrapper if requested.
        JSNativeApi.Wrap(wrapper, obj, out JSReference wrapperWeakRef);

        _objectMap.AddOrUpdate(
            obj,
            (_) => wrapperWeakRef,
            (_, oldReference) =>
            {
                oldReference.Dispose();
                return wrapperWeakRef;
            });

        return wrapper;
    }

    /// <summary>
    /// Gets or creates a JS wrapper for an instance of a class.
    /// </summary>
    /// <returns>The JS wrapper.</returns>
    /// <remarks>
    /// If the class was constructed via JS, then the wrapper created at that time will be
    /// found in the map and returned, if the weak reference to it is still valid. Otherwise
    /// a new JS object is constructed to wrap the existing instance, and a weak reference to
    /// the new wrapper is saved in the map.
    /// </remarks>
    public JSValue GetOrCreateObjectWrapper<T>(T obj) where T : class
    {
        JSValue? wrapper = null;
        JSReference CreateWrapper(T obj)
        {
            // Pass the existing instance as an external value to the JS constructor.
            // The constructor callback will then use that instead of creating a new
            // instance of the class.
            JSValue externalValue = JSValue.CreateExternal(obj);
            JSValue constructorFunction = GetClassConstructor<T>();
            wrapper = constructorFunction.CallAsConstructor(externalValue);
            return new(wrapper.Value, isWeak: true);
        }

        _objectMap.AddOrUpdate(
            obj,
            (_) =>
            {
                // No wrapper was found in the map for the object. Create a new one.
                return CreateWrapper(obj);
            },
            (_, wrapperReference) =>
            {
                wrapper = wrapperReference.GetValue();
                if (wrapper.HasValue)
                {
                    // A valid reference was found in the map. Return it to keep the same mapping.
                    return wrapperReference;
                }

                // A reference was found in the map, but the JS object was released.
                // Create a new wrapper JS object and update the reference in the map.l
                wrapperReference.Dispose();
                return CreateWrapper(obj);
            });

        return wrapper!.Value;
    }
    public JSValue GetOrCreateCollectionWrapper<T>(
        IEnumerable<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSIterable.Enumerable<T> adapter ? adapter.Array :
            GetOrCreateCollectionWrapper(
                collection, () => JSIterable.Proxy(collection, this, toJS));
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlyCollection<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSArray.ReadOnlyCollection<T> adapter ? adapter.Array :
            GetOrCreateCollectionWrapper(
                collection, () => JSArray.Proxy(collection, this, toJS));
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        ICollection<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSArray.Collection<T> adapter ? adapter.Array :
            GetOrCreateCollectionWrapper(
                collection, () => JSArray.Proxy(collection, this, toJS, fromJS));
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlyList<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSArray.ReadOnlyList<T> adapter ? adapter.Array :
            GetOrCreateCollectionWrapper(
                collection, () => JSArray.Proxy(collection, this, toJS));
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IList<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSArray.List<T> adapter ? adapter.Array :
            GetOrCreateCollectionWrapper(
                collection, () => JSArray.Proxy(collection, this, toJS, fromJS));
    }

    private JSValue GetOrCreateCollectionWrapper(
        object collection,
        Func<JSValue> createWrapper)
    {
        JSValue? wrapper = null;

        _objectMap.AddOrUpdate(
            collection,
            (_) =>
            {
                // No wrapper was found in the map for the object. Create a new one.
                wrapper = createWrapper();
                JSNativeApi.Wrap(wrapper.Value, collection);
                return new JSReference(wrapper.Value, isWeak: true);
            },
            (_, wrapperReference) =>
            {
                wrapper = wrapperReference.GetValue();
                if (wrapper.HasValue)
                {
                    // A valid reference was found in the map. Return it to keep the same mapping.
                    return wrapperReference;
                }

                // A reference was found in the map, but the JS object was released.
                // Create a new wrapper JS object and update the reference in the map.l
                wrapperReference.Dispose();

                wrapper = createWrapper();
                JSNativeApi.Wrap(wrapper.Value, collection);
                return new JSReference(wrapper.Value, isWeak: true);
            });

        return wrapper!.Value;
    }

    /// <summary>
    /// Registers a struct JS constructor, enabling instantiation of JS wrappers for the struct.
    /// </summary>
    /// <param name="constructorFunction">JS struct constructor function returned from
    /// <see cref="JSNativeApi.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal JSValue RegisterStruct<T>(JSValue constructorFunction) where T : struct
    {
        _structMap.AddOrUpdate(
            typeof(T),
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Struct already registered for JS export: " + typeof(T)));
        return constructorFunction;
    }

    /// <summary>
    /// Creates a new (empty) JS instance for a struct.
    /// </summary>
    /// <returns>The JS wrapper.</returns>
    public JSValue CreateStruct<T>() where T : struct
    {
        if (!_structMap.TryGetValue(typeof(T), out JSReference? constructorReference))
        {
            throw new InvalidOperationException(
                "Struct not registered for JS export: " + typeof(T));
        }

        JSValue? constructorFunction = constructorReference!.GetValue();
        if (!constructorFunction.HasValue)
        {
            // This should never happen because the reference is "strong".
            throw new InvalidOperationException("Failed to resolve struct constructor reference.");
        }

        return JSNativeApi.CallAsConstructor(constructorFunction.Value);
    }

    public JSValue Import(string name)
    {
        JSReference reference = _importMap.GetOrAdd(name, (_) =>
        {
            JSValue value = JSValue.Global[name];
            return new JSReference(value);
        });
        return reference.GetValue() ?? JSValue.Undefined;
    }

    public JSReference TrackReference(JSValue value)
    {
        var reference = new JSReference(value);
        _trackedReferences.Add(reference);
        return reference;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            DisposeReferences(_objectMap);
            DisposeReferences(_classMap);
            DisposeReferences(_structMap);
            DisposeReferences(_trackedReferences);
        }

        GC.SuppressFinalize(this);
    }

    public bool IsDisposed { get; private set; } = false;

    private static void DisposeReferences<TKey>(
        ConcurrentDictionary<TKey, JSReference> references) where TKey : notnull
    {
        foreach (JSReference reference in references.Values)
        {
            try
            {
                reference.Dispose();
            }
            catch (JSException)
            {
            }
        }

        references.Clear();
    }

    private static void DisposeReferences(
        ConcurrentBag<JSReference> references)
    {
        foreach (JSReference reference in references)
        {
            try
            {
                reference.Dispose();
            }
            catch (JSException)
            {
            }
        }

        references.Clear();
    }
}
