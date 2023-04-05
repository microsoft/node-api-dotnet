// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using static Microsoft.JavaScript.NodeApi.Interop.JSCollectionProxies;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using napi_env = Microsoft.JavaScript.NodeApi.JSNativeApi.Interop.napi_env;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Manages JavaScript interop context for the lifetime of the .NET Node API host.
/// </summary>
/// <remarks>
/// A <see cref="JSRuntimeContext"/> instance is constructed when the .NET Node API managed host is
/// loaded, and disposed when the host is unloaded. (For AOT there is no "host" compnoent, so each
/// AOT module has a context that matches the module lifetime.) The context tracks several kinds
/// of JS references used internally by this assembly, so that the references can be re-used for
/// the lifetime of the module and disposed when the context is disposed.
/// </remarks>
public sealed class JSRuntimeContext : IDisposable
{
    /// <summary>
    /// Name of a global object that may hold context specific to Node API .NET.
    /// </summary>
    /// <remarks>
    /// Currently it is only used to pass the require() function to .NET AOT modules.
    /// </remarks>
    public const string GlobalObjectName = "node_api_dotnet";

    private readonly napi_env _env;

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
    /// Maps from exported static class names to (strong references to) JS objects for each class.
    /// </summary>
    /// <remarks>
    /// Used primarily to prevent the JS GC from collecting the class object, which can cause
    /// class property descriptors to be finalized while a class method is still referenced and
    /// called from JS.
    /// </remarks>
    private readonly ConcurrentDictionary<string, JSReference> _staticClassMap = new();

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
    private readonly ConcurrentDictionary<(string?, string?), JSReference> _importMap = new();

    /// <summary>
    /// Holds a reference to the require() function.
    /// </summary>
    private JSReference? _require;

    private readonly ConcurrentDictionary<Type, JSProxy.Handler> _collectionProxyHandlerMap = new();

    public bool IsDisposed { get; private set; }

    public static explicit operator napi_env(JSRuntimeContext context) => context._env;
    public static explicit operator JSRuntimeContext(napi_env env)
        => GetInstanceData(env) as JSRuntimeContext
           ?? throw new InvalidCastException("Context is not found in napi_env instance data.");

    /// <summary>
    /// Gets the current host context.
    /// </summary>
    public static JSRuntimeContext Current => JSValueScope.Current.RuntimeContext;

    public JSSynchronizationContext SynchronizationContext { get; }

    public JSRuntimeContext(napi_env env)
    {
        // TODO: Move this Initialize call to the creators of JSRuntimeContext
        JSNativeApi.Interop.Initialize();

        _env = env;
        SetInstanceData(env, this);
        SynchronizationContext = new JSSynchronizationContext();
    }

    /// <summary>
    /// Gets or sets the require() function.
    /// </summary>
    /// <remarks>
    /// Managed-host initialization will typically pass in the require function.
    /// </remarks>
    public JSValue Require
    {
        get
        {
            JSValue? value = _require?.GetValue();
            if (value?.IsFunction() == true)
            {
                return value.Value;
            }

            JSValue globalObject = JSValue.Global[GlobalObjectName];
            if (globalObject.IsObject())
            {
                JSValue globalRequire = globalObject["require"];
                if (globalRequire.IsFunction())
                {
                    _require = new JSReference(globalRequire);
                    return globalRequire;
                }
            }

            throw new InvalidOperationException(
                $"The require function was not found on the global {GlobalObjectName} object. " +
                $"Set `global.{GlobalObjectName} = {{ require }}` before loading the module.");
        }
        set
        {
            _require?.Dispose();
            _require = new JSReference(value);
        }
    }

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
    /// Registers a static class JS object, preventing it from being GC'd before the module is
    /// unloaded.
    /// </summary>
    /// <param name="name">Name of the static class.</param>
    /// <param name="classObject">Object that has the class properties and methods.</param>
    /// <returns>The JS object.</returns>
    internal JSValue RegisterStaticClass(string name, JSValue classObject)
    {
        _staticClassMap.AddOrUpdate(
            name,
            (_) => new JSReference(classObject, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Class already registered for JS export: " + name));
        return classObject;
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
    /// <param name="externalInstance">New or existing instance of the class to be wrapped,
    /// passed as a JS "external" value.</param>
    /// <returns>The JS wrapper.</returns>
    internal JSValue InitializeObjectWrapper(JSValue wrapper, JSValue externalInstance)
    {
        object obj = externalInstance.GetValueExternal();

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
        if (obj == null)
        {
            return JSValue.Null;
        }

        JSValue? wrapper = null;
        JSReference CreateWrapper(T obj)
        {
            if (obj is Stream stream)
            {
                wrapper = NodeStream.CreateProxy(stream);
            }
            else
            {
                // Pass the existing instance as an external value to the JS constructor.
                // The constructor callback will then use that instead of creating a new
                // instance of the class.
                JSValue externalValue = JSValue.CreateExternal(obj);
                JSValue constructorFunction = GetClassConstructor<T>();
                wrapper = constructorFunction.CallAsConstructor(externalValue);
            }

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
        return collection is JSIterableEnumerable<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IEnumerable<T>),
                    (_) => CreateIterableProxyHandlerForEnumerable(toJS));
                return new JSProxy(new JSObject(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IAsyncEnumerable<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSIterableEnumerable<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IAsyncEnumerable<T>),
                    (_) => CreateAsyncIterableProxyHandlerForAsyncEnumerable(toJS));
                return new JSProxy(new JSObject(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlyCollection<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSArrayReadOnlyCollection<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IReadOnlyCollection<T>),
                    (_) => CreateIterableProxyHandlerForReadOnlyCollection(toJS));
                return new JSProxy(new JSObject(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        ICollection<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSArrayCollection<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(ICollection<T>),
                    (_) => CreateIterableProxyHandlerForCollection(toJS, fromJS));
                return new JSProxy(new JSObject(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlyList<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSArrayReadOnlyList<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IReadOnlyList<T>),
                    (_) => CreateArrayProxyHandlerForReadOnlyList(toJS));
                return new JSProxy(new JSArray(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IList<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSArrayList<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IList<T>),
                    (_) => CreateArrayProxyHandlerForList(toJS, fromJS));
                return new JSProxy(new JSArray(), proxyHandler, collection);
            });
    }

#if !NETFRAMEWORK
    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlySet<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSSetReadOnlySet<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IReadOnlySet<T>),
                    (_) => CreateSetProxyHandlerForReadOnlySet(toJS, fromJS));
                return new JSProxy(new JSSet(), proxyHandler, collection);
            });
    }
#endif // !NETFRAMEWORK

    public JSValue GetOrCreateCollectionWrapper<T>(
        ISet<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSSetSet<T> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(ISet<T>),
                    (_) => CreateSetProxyHandlerForSet(toJS, fromJS));
                return new JSProxy(new JSSet(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> collection,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS)
    {
        return collection is JSMapReadOnlyDictionary<TKey, TValue> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                typeof(IReadOnlyDictionary<TKey, TValue>),
                    (_) => CreateMapProxyHandlerForReadOnlyDictionary(
                        keyToJS, valueToJS, keyFromJS));
                return new JSProxy(new JSMap(), proxyHandler, collection);
            });
    }

    public JSValue GetOrCreateCollectionWrapper<TKey, TValue>(
        IDictionary<TKey, TValue> collection,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS)
    {
        return collection is JSMapDictionary<TKey, TValue> adapter ? adapter.Value :
            GetOrCreateCollectionProxy(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                typeof(IDictionary<TKey, TValue>),
                    (_) => CreateMapProxyHandlerForDictionary(
                        keyToJS, valueToJS, keyFromJS, valueFromJS));
                return new JSProxy(new JSMap(), proxyHandler, collection);
            });
    }

    private JSValue GetOrCreateCollectionProxy(
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
                // Create a new wrapper JS object and update the reference in the map.
                wrapperReference.Dispose();
                wrapper = createWrapper();
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

    /// <summary>
    /// Imports a module or module property from JavaScript.
    /// </summary>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <returns>The imported value.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref cref="module" /> and
    /// <paramref cref="property" /> are null.</exception>
    /// <exception cref="InvalidOperationException">The <see cref="Require"/> function was
    /// not initialized.</exception>
    public JSValue Import(string? module, string? property = null)
    {
        if (module == null && property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        JSReference reference = _importMap.GetOrAdd((module, property), (_) =>
        {
            if (module == null)
            {
                // Importing a built-in object from `global`.
                JSValue value = JSValue.Global[property!];
                return new JSReference(value);
            }
            else if (property == null)
            {
                // Importing from a module via require().
                JSValue moduleValue = Require.Call(thisArg: JSValue.Undefined, module);
                return new JSReference(moduleValue);
            }
            else
            {
                // Getting a property on an imported module.
                JSValue moduleValue = Import(module, null);
                JSValue propertyValue = moduleValue.IsUndefined() ?
                    JSValue.Undefined : moduleValue.GetProperty(property);
                return new JSReference(propertyValue);
            }

        });
        return reference.GetValue() ?? JSValue.Undefined;
    }

    /// <summary>
    /// Runs callback in the main Node.JS loop for this module environment.
    /// </summary>
    /// <param name="callback">The callback to run.</param>
    /// <param name="allowSyncRun">Pass true to allow synchronous execution if are already
    /// in the main loop. Default is false.
    /// </param>
    /// <remarks>
    /// By default it runs the callback always asynchronously.
    /// Set the <c>allowSyncRun</c> parameter to true to allow sync execution if we are
    /// already in the main loop thread.
    ///
    /// This method can be called from any thread.
    /// </remarks>
    public void RunInMainLoop(Action callback, bool allowSyncRun = false)
    {
        if (IsDisposed) return;

        if (allowSyncRun && JSSynchronizationContext.Current == SynchronizationContext)
        {
            callback();
            return;
        }

        SynchronizationContext.Post(_ =>
        {
            if (IsDisposed) return;

            callback();
        }, null);
    }

    /// <summary>
    /// A helper method to run callbacks that need napi_env parameter
    /// in the main Node.JS loop for this module environment.
    /// </summary>
    /// <param name="callback">The callback to run.</param>
    /// <param name="allowSyncRun">Pass true to allow synchronous execution if are already
    /// in the main loop. Default is false.
    /// </param>
    /// <remarks>
    /// By default it runs the callback always asynchronously.
    /// Set the <c>allowSyncRun</c> parameter to true to allow sync execution if we are
    /// already in the main loop thread.
    ///
    /// This method can be called from any thread.
    /// </remarks>
    internal void RunInMainLoop(Action<napi_env> callback, bool allowSyncRun = false)
    {
        if (IsDisposed) return;

        RunInMainLoop(() => callback(_env), allowSyncRun);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        DisposeReferences(_objectMap);
        DisposeReferences(_classMap);
        DisposeReferences(_staticClassMap);
        DisposeReferences(_structMap);
        SynchronizationContext.Dispose();
    }

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
}
