using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static NodeApi.JSNativeApi;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

/// <summary>
/// Tracks JS constructors and instance JS wrappers for exported classes, enabling
/// .NET objects to be automatically wrapped when returned to JS, and re-wrapped as needed
/// if the (weakly-referenced) JS wrapper has been released.
/// </summary>
internal static class ObjectMap
{
    // TODO: Consider an optimization that avoids dictionary lookups:
    // If an exported class is declared as partial, then the generator can add a
    // static `JSConstructor` property and an instance `JSWrapper` property to the class.
    // (The dictionary mappings could still be used to export external/non-partial classes.)

    private static readonly ConcurrentDictionary<Type, JSReference> s_classMap = new();
    private static readonly ConcurrentDictionary<object, JSReference> s_objectMap = new();
    private static readonly ConcurrentDictionary<Type, JSReference> s_structMap = new();

    /// <summary>
    /// Registers a class JS constructor, enabling automatic JS wrapping of instances of the class.
    /// </summary>
    /// <param name="constructorFunction">JS class constructor function returned from
    /// <see cref="JSNativeApi.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal static JSValue RegisterClass<T>(JSValue constructorFunction) where T : class
    {
        s_classMap.AddOrUpdate(
            typeof(T),
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Class already registered for JS export: " + typeof(T)));
        return constructorFunction;
    }

    /// <summary>
    /// Gets a class JS constructor that was previously registered.
    /// </summary>
    private static JSValue GetClassConstructor<T>() where T : class
    {
        if (!s_classMap.TryGetValue(typeof(T), out JSReference? constructorReference))
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
    internal static unsafe JSValue InitializeObjectWrapper<T>(JSValue wrapper, T obj) where T : class
    {
        GCHandle valueHandle = GCHandle.Alloc(obj);
        napi_ref result;
        napi_wrap(
            (napi_env)JSValueScope.Current,
            (napi_value)wrapper,
            (nint)valueHandle,
            new napi_finalize(&FinalizeGCHandle),
            nint.Zero,
            &result).ThrowIfFailed();

        // The reference returned by napi_wrap() is weak (refcount=0), which is good:
        // if the JS object is released then the reference will fail to resolve, and
        // GetOrCreateObjectWrapper() will create a new JS wrapper if requested.
        JSReference wrapperReference = new(result, isWeak: true);

        s_objectMap.AddOrUpdate(
            obj,
            (_) => wrapperReference,
            (_, oldReference) =>
            {
                oldReference.Dispose();
                return wrapperReference;
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
    internal static JSValue GetOrCreateObjectWrapper<T>(T obj) where T : class
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

        s_objectMap.AddOrUpdate(
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

    /// <summary>
    /// Registers a struct JS constructor, enabling instantiation of JS wrappers for the struct.
    /// </summary>
    /// <param name="constructorFunction">JS struct constructor function returned from
    /// <see cref="JSNativeApi.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal static JSValue RegisterStruct<T>(JSValue constructorFunction) where T : struct
    {
        s_structMap.AddOrUpdate(
            typeof(T),
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Struct already registered for JS export: " + typeof(T)));
        return constructorFunction;
    }

    /// <summary>
    /// Creates a new (empty) JS wrapper instance for a struct.
    /// </summary>
    /// <returns>The JS wrapper.</returns>
    internal static JSValue CreateStructWrapper<T>() where T : struct
    {
        if (!s_structMap.TryGetValue(typeof(T), out JSReference? constructorReference))
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
}
