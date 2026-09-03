// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Interop.JSCollectionProxies;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Manages JavaScript interop context for the lifetime of the .NET Node API host.
/// </summary>
/// <remarks>
/// A <see cref="JSRuntimeContext"/> instance is constructed when the .NET Node API managed host is
/// loaded, and the hosting infrastructure tears it down when the host is unloaded. (For AOT there
/// is no "host" component, so each AOT module has a context that matches the module lifetime.) The
/// context tracks several kinds of JS references used internally by this assembly, so that the
/// references can be re-used for the lifetime of the host and released during context teardown.
/// </remarks>
public sealed class JSRuntimeContext
{
    /// <summary>
    /// Name of a global object that may hold context specific to Node API .NET.
    /// </summary>
    /// <remarks>
    /// Currently it is only used to pass the require() function to .NET AOT modules.
    /// </remarks>
    public const string GlobalObjectName = "node_api_dotnet";

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
    /// Maps from (weak references to) C# class objects to (weak references to) JS wrappers for
    /// each object.
    /// </summary>
    /// <remarks>
    /// Enables re-using the same JS wrapper objects for the same C# objects, so that
    /// a C# object maps to the same JS instance when marshalled multiple times. The
    /// references are weak to allow the JS wrappers to be released; new wrappers are
    /// re-constructed as needed. This is not used with C# structs which are always
    /// passed to/from JS by value.
    /// </remarks>
    private readonly ConditionalWeakTable<object, JSReference> _objectMap = new();

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
    /// Holds a reference to the synchronous CommonJS require() function.
    /// </summary>
    private JSReference? _requireFunction;

    /// <summary>
    /// Holds a reference to the asynchronous ES import() function.
    /// </summary>
    private JSReference? _importFunction;

    private readonly ConcurrentDictionary<Type, JSProxy.Handler> _collectionProxyHandlerMap = new();

    // Two buckets so ownership is explicit: DisposableAnnotations are disposed at context teardown;
    // Annotations are not. Both are lazy and touched only on the JS thread.
    private Dictionary<Type, object>? _annotations;
    private Dictionary<Type, IDisposable>? _disposableAnnotations;

    // Module instances disposed at context teardown. Unlike the type-keyed annotations, several
    // modules share one context, so these are appended rather than keyed by type.
    private List<IDisposable>? _moduleDisposables;

    // Env instance-data layout: one GCHandle slot per runtime sharing the napi_env. Slot 0 is the
    // module context (managed host / AOT module / embedding); slot 1 is the native host context.
    // A runtime reads and writes only its own slot, so it never dereferences the other runtime's
    // GCHandle (which belongs to a separate GC heap).
    private const int ModuleContextSlot = 0;
    private const int HostContextSlot = 1;
    private const int InstanceDataSlotCount = 2;

    // Written into a slot when its context is disposed, so an env is associated with a runtime
    // context exactly once: RegisterInstanceData rejects a non-empty slot (a live handle or this
    // tombstone), and FromEnv/FinalizeInstanceData treat the tombstone as "no live context". A real
    // GCHandle is never -1.
    private static readonly nint s_disposedSlot = -1;

    // This runtime's slot in the instance-data block: the module slot by default, or the host slot
    // once the native host calls UseHostContextSlot() at startup.
    private static int s_instanceDataSlot = ModuleContextSlot;

    // Runtime v-table used by FromEnv to read env-keyed instance data. NodejsRuntime is stateless,
    // and wrappers such as TracingJSRuntime delegate instance-data access to it, so any registered
    // production runtime can read every env. The env check in FromEnv makes an instance-backed
    // custom runtime fail closed if this field is stale.
    private static JSRuntime? s_instanceDataRuntime;

    internal napi_env EnvironmentHandle
    {
        get
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(JSRuntimeContext));
            }

            return UncheckedEnvironmentHandle;
        }
    }

    /// <summary>
    /// Gets the environment handle without checking whether the context is disposed. For use
    /// only where a checked access is unnecessary, such as capturing the env to release a
    /// reference on the JS thread (where a disposed context makes the release a safe no-op).
    /// </summary>
    internal napi_env UncheckedEnvironmentHandle { get; }

    /// <summary>
    /// Gets the GCHandle that roots this context and is stored in its env instance-data slot. It is
    /// freed when the context is disposed at env teardown; finalizers resolve the context via
    /// <see cref="FromEnv"/> rather than this handle, so freeing it leaves nothing dangling.
    /// </summary>
    internal nint ContextHandle { get; }

    /// <summary>
    /// The managed thread that constructed this context — its environment's JS thread. A runtime
    /// scope may be entered only on this thread.
    /// </summary>
    internal int OwningThreadId { get; }

    public static explicit operator napi_env(JSRuntimeContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        return context.EnvironmentHandle;
    }

    /// <summary>
    /// Resolves the <see cref="JSRuntimeContext"/> for the calling runtime from a napi_env, via the
    /// env instance-data block, or null if none is registered. Unlike <see cref="Current"/> this
    /// does not require a current scope, so callback dispatch can recover the context when no scope
    /// is on the thread-static stack yet.
    /// </summary>
    public static unsafe JSRuntimeContext? FromEnv(napi_env env)
    {
        JSRuntime? runtime = s_instanceDataRuntime;
        if (runtime is null)
        {
            return null;
        }

        runtime.GetInstanceData(env, out nint instanceData).ThrowIfFailed();
        if (instanceData == default)
        {
            return null;
        }

        nint slotHandle = ((nint*)instanceData)[s_instanceDataSlot];
        if (slotHandle == default || slotHandle == s_disposedSlot)
        {
            return null;
        }

        // The production runtime lookup is env-keyed, but an instance-backed custom runtime could
        // return another env's block when the process-wide runtime is stale. Fail closed unless the
        // resolved context owns the requested env.
        JSRuntimeContext? context = GCHandle.FromIntPtr(slotHandle).Target as JSRuntimeContext;
        return context is not null && context.UncheckedEnvironmentHandle == env ? context : null;
    }

    /// <summary>
    /// Configures the calling runtime to use the native host's instance-data slot. Called once by
    /// the native host at startup; every other runtime keeps the default module slot.
    /// </summary>
    internal static void UseHostContextSlot() => s_instanceDataSlot = HostContextSlot;

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    /// <exception cref="InvalidOperationException">No runtime context was set for the current
    /// thread.</exception>
    public static JSRuntimeContext Current => JSValueScope.Current.RuntimeContext;

    public JSRuntime Runtime { get; }

    private JSSynchronizationContext? _synchronizationContext;

    /// <summary>
    /// Gets the synchronization context that marshals callbacks and continuations to the JS thread.
    /// A default one is created on first access, which must happen while a scope for this context is
    /// current, because creating it captures the current scope's runtime and environment.
    /// </summary>
    public JSSynchronizationContext SynchronizationContext
    {
        get
        {
            if (_synchronizationContext is not null)
            {
                return _synchronizationContext;
            }

            // Lazy creation captures the CURRENT scope's env/thread, so it must run only while this
            // context is current -- otherwise it would bind this context to a different environment.
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(JSRuntimeContext));
            }
            if (JSValueScope.Current.RuntimeContext != this)
            {
                throw new InvalidOperationException(
                    "The synchronization context must be created while its runtime context is current.");
            }

            return _synchronizationContext = JSSynchronizationContext.Create();
        }
    }

    /// <summary>
    /// Creates a runtime context for a JS environment. Used by AOT module entry points and other
    /// embedders that own the environment and therefore create the context rather than resolving
    /// it from a host.
    /// </summary>
    /// <param name="env">The JS environment handle.</param>
    /// <param name="runtime">The JS runtime interface; defaults to a <see cref="NodejsRuntime"/>.
    /// </param>
    /// <param name="synchronizationContext">The synchronization context owned by this context; a
    /// default one is created when omitted.</param>
    public static JSRuntimeContext Create(
        napi_env env,
        JSRuntime? runtime = null,
        JSSynchronizationContext? synchronizationContext = null)
        => new(env, runtime ?? new NodejsRuntime(), synchronizationContext);

    internal JSRuntimeContext(
        napi_env env,
        JSRuntime runtime,
        JSSynchronizationContext? synchronizationContext = null)
    {
        if (env.IsNull) throw new ArgumentNullException(nameof(env));

        UncheckedEnvironmentHandle = env;
        Runtime = runtime;
        OwningThreadId = Environment.CurrentManagedThreadId;
        ContextHandle = (nint)GCHandle.Alloc(this);
        try
        {
            RegisterInstanceData(env, runtime);
        }
        catch
        {
            // Registration failed before any caller holds this context to dispose it; free the
            // rooting handle so a failed construction leaks nothing (the block, if allocated, is
            // freed by RegisterInstanceData).
            GCHandle.FromIntPtr(ContextHandle).Free();
            throw;
        }

        _synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Registers this context in the env instance-data block at this runtime's slot, allocating the
    /// block and attaching the teardown finalizer if this runtime is the first to claim the slot.
    /// </summary>
    private unsafe void RegisterInstanceData(napi_env env, JSRuntime runtime)
    {
        runtime.GetInstanceData(env, out nint instanceData).ThrowIfFailed();
        if (instanceData == default)
        {
            // One block per env, freed by FinalizeInstanceData when the last context on the env is
            // disposed at teardown.
            instanceData = Marshal.AllocHGlobal(IntPtr.Size * InstanceDataSlotCount);
            for (int i = 0; i < InstanceDataSlotCount; i++)
            {
                ((nint*)instanceData)[i] = default;
            }

            napi_status status = runtime.SetInstanceData(
                env,
                instanceData,
                new napi_finalize(s_finalizeInstanceData),
                finalizeHint: default);
            if (status != napi_status.napi_ok)
            {
                // Registration failed, so Node never took ownership of the block; free it here.
                Marshal.FreeHGlobal(instanceData);
                status.ThrowIfFailed();
            }
        }
        else if (((nint*)instanceData)[s_instanceDataSlot] != default)
        {
            // An env is associated with a runtime context exactly once. The slot holds a live
            // context's handle, or a tombstone after one was disposed; either way a second
            // association is rejected rather than silently replacing the first.
            throw new InvalidOperationException(
                "The environment is already associated with a runtime context.");
        }

        ((nint*)instanceData)[s_instanceDataSlot] = ContextHandle;

        // Publish the process-wide runtime that FromEnv uses only after registration succeeds, so a
        // failed GetInstanceData/SetInstanceData or a rejected duplicate slot can't repoint FromEnv at
        // a runtime that never registered a context on this env.
        s_instanceDataRuntime = runtime;
    }

#if !UNMANAGED_DELEGATES
    private static readonly napi_finalize.Delegate s_finalizeInstanceData = FinalizeInstanceData;
#else
    private static readonly unsafe delegate* unmanaged[Cdecl]<napi_env, nint, nint, void>
        s_finalizeInstanceData = &FinalizeInstanceData;
#endif

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void FinalizeInstanceData(napi_env env, nint data, nint hint)
    {
        // Runs during env teardown, where calling into JS is forbidden. Dispose the owning
        // runtime's context (which clears its slot and frees its rooting GCHandle). Only this
        // runtime's slot is read, never the other runtime's (whose GCHandle belongs to a separate
        // GC heap).
        nint slotHandle = ((nint*)data)[s_instanceDataSlot];
        if (slotHandle != default && slotHandle != s_disposedSlot)
        {
            try
            {
                (GCHandle.FromIntPtr(slotHandle).Target as JSRuntimeContext)?.Dispose();
            }
            catch
            {
                // A finalizer must never throw; teardown continues regardless.
            }
        }

        // Free the shared block once no slot holds a live context (each is empty or tombstoned);
        // disposing a host context cascades synchronously to the other slot. Do not null it out
        // via napi_set_instance_data: that deletes this very TrackedFinalizer, which Node then
        // deletes again (double free).
        for (int i = 0; i < InstanceDataSlotCount; i++)
        {
            nint slot = ((nint*)data)[i];
            if (slot != default && slot != s_disposedSlot)
            {
                return;
            }
        }

        Marshal.FreeHGlobal(data);
    }

    /// <summary>
    /// Gets or sets the require() function, that supports synchronously importing CommonJS modules.
    /// </summary>
    /// <remarks>
    /// Managed-host initialization will typically pass in the require function.
    /// </remarks>
    public JSFunction RequireFunction
    {
        get
        {
            JSValue? value = _requireFunction?.GetValue();
            if (value?.IsFunction() == true)
            {
                return (JSFunction)value.Value;
            }

            JSValue globalObject = JSValue.Global[GlobalObjectName];
            if (globalObject.IsObject())
            {
                JSValue globalRequire = globalObject["require"];
                if (globalRequire.IsFunction())
                {
                    _requireFunction = new JSReference(globalRequire);
                    return (JSFunction)globalRequire;
                }
            }

            throw new InvalidOperationException(
                $"The require function was not found on the global {GlobalObjectName} object. " +
                $"Set `global.{GlobalObjectName}.require` before loading the module.");
        }
        set
        {
            _requireFunction?.Dispose();
            _requireFunction = new JSReference(value);
        }
    }

    /// <summary>
    /// Gets or sets the import() function, that supports asynchronously importing ES modules.
    /// </summary>
    /// <remarks>
    /// Managed-host initialization will typically pass in the import function.
    /// </remarks>
    public JSFunction ImportFunction
    {
        get
        {
            JSValue? value = _importFunction?.GetValue();
            if (value?.IsFunction() == true)
            {
                return (JSFunction)value.Value;
            }

            JSValue globalObject = JSValue.Global[GlobalObjectName];
            if (globalObject.IsObject())
            {
                JSValue globalImport = globalObject["import"];
                if (globalImport.IsFunction())
                {
                    _importFunction = new JSReference(globalImport);
                    return (JSFunction)globalImport;
                }
            }

            throw new InvalidOperationException(
                $"The import function was not found on the global {GlobalObjectName} object. " +
                $"Set `global.{GlobalObjectName}.import` before loading the module.");
        }
        set
        {
            _importFunction?.Dispose();
            _importFunction = new JSReference(value);
        }
    }

    /// <summary>
    /// Registers a class JS constructor, enabling automatic JS wrapping of instances of the class.
    /// </summary>
    /// <param name="classType">Type of the class to be registered.</param>
    /// <param name="constructorFunction">JS class constructor function returned from
    /// <see cref="JSRuntime.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal JSValue RegisterClass(Type classType, JSValue constructorFunction)
    {
        _classMap.AddOrUpdate(
            classType,
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Class already registered for JS export: " + classType.Name));
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
                "Class not registered for JS export: " + typeof(T).Name);
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
    /// for <see cref="JSRuntime.DefineClass"/>.</param>
    /// <param name="externalInstance">New or existing instance of the class to be wrapped,
    /// passed as a JS "external" value.</param>
    /// <returns>The JS wrapper.</returns>
    internal JSValue InitializeObjectWrapper(JSValue wrapper, JSValue externalInstance)
    {
        object obj = externalInstance.GetValueExternal();

        // The reference returned by Wrap() is weak (refcount=0), which is good:
        // if the JS object is released then the reference will fail to resolve, and
        // GetOrCreateObjectWrapper() will create a new JS wrapper if requested.
        wrapper.Wrap(obj, out JSReference wrapperWeakRef);

        if (_objectMap.TryGetValue(obj, out JSReference? existingWrapperWeakRef))
        {
            if (!existingWrapperWeakRef.IsDisposed && existingWrapperWeakRef.TryGetValue(out _))
            {
                // If the .NET object is already mapped to a non-released JS object, then
                // another one should not be created.
                throw new InvalidOperationException("Duplicate .NET to JS object mapping.");
            }
            else
            {
                // An existing wrapper was in the map, but the JS object was released.
                existingWrapperWeakRef.Dispose();
                _objectMap.Remove(obj);
            }
        }

        _objectMap.Add(obj, wrapperWeakRef);
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
            // Marshal null object reference to JS undefined.
            return JSValue.Undefined;
        }

        return GetOrCreateObjectWrapper(obj, () =>
        {
            if (obj is Stream stream)
            {
                return NodeStream.CreateProxy(stream);
            }
            else
            {
                // Pass the existing instance as an external value to the JS constructor.
                // The constructor callback will then use that instead of creating a new
                // instance of the class.
                JSValue externalValue = JSValue.CreateExternal(obj);
                JSValue constructorFunction = GetClassConstructor<T>();
                return constructorFunction.CallAsConstructor(externalValue);
            }
        });
    }

    private JSValue GetOrCreateObjectWrapper<T>(T obj, Func<JSValue> createWrapper)
        where T : class
    {
        if (_objectMap.TryGetValue(obj, out JSReference? wrapperWeakRef) &&
            !wrapperWeakRef.IsDisposed)
        {
            if (wrapperWeakRef.TryGetValue(out JSValue existingWrapper))
            {
                // Return the JS wrapper that was found in the map.
                return existingWrapper;
            }
            else
            {
                // The JS wrapper was released.
                // The disposed weak reference will be removed from the map below.
                wrapperWeakRef.Dispose();
                _objectMap.Remove(obj);
            }
        }

        // No wrapper was found in the map for the object. Create a new one.
        JSValue wrapper = createWrapper();

        // Add the wrapper reference if it is not added by createWrapper().
        if (!_objectMap.TryGetValue(obj, out wrapperWeakRef) || wrapperWeakRef.IsDisposed)
        {
            wrapperWeakRef = new JSReference(wrapper, isWeak: true);

            // Use AddOrUpdate() in case the constructor just added the object
            // or a previously-disposed wrapper was in the map.
#if NETFRAMEWORK || NETSTANDARD
            _objectMap.Remove(obj);
            _objectMap.Add(obj, wrapperWeakRef);
#else
            _objectMap.AddOrUpdate(obj, wrapperWeakRef);
#endif
        }

        return wrapper;
    }

    public JSValue GetOrCreateCollectionWrapper<T>(
        IEnumerable<T> collection,
        JSValue.From<T> toJS)
    {
        return collection is JSIterableEnumerable<T> adapter ? adapter.Value :
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IList<T>),
                    (_) => CreateArrayProxyHandlerForList(toJS, fromJS));
                return new JSProxy(new JSArray(), proxyHandler, collection);
            });
    }

#if READONLY_SET
    public JSValue GetOrCreateCollectionWrapper<T>(
        IReadOnlySet<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSSetReadOnlySet<T> adapter ? adapter.Value :
            GetOrCreateObjectWrapper(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                    typeof(IReadOnlySet<T>),
                    (_) => CreateSetProxyHandlerForReadOnlySet(toJS, fromJS));
                return new JSProxy(new JSSet(), proxyHandler, collection);
            });
    }
#endif

    public JSValue GetOrCreateCollectionWrapper<T>(
        ISet<T> collection,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return collection is JSSetSet<T> adapter ? adapter.Value :
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
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
            GetOrCreateObjectWrapper(collection, () =>
            {
                JSProxy.Handler proxyHandler = _collectionProxyHandlerMap.GetOrAdd(
                typeof(IDictionary<TKey, TValue>),
                    (_) => CreateMapProxyHandlerForDictionary(
                        keyToJS, valueToJS, keyFromJS, valueFromJS));
                return new JSProxy(new JSMap(), proxyHandler, collection);
            });
    }

    /// <summary>
    /// Registers a struct JS constructor, enabling instantiation of JS objects for the struct.
    /// </summary>
    /// <param name="structType">Type of the struct to be registered.</param>
    /// <param name="constructorFunction">JS struct constructor function returned from
    /// <see cref="JSRuntime.DefineClass"/></param>
    /// <returns>The JS constructor.</returns>
    internal JSValue RegisterStruct(Type structType, JSValue constructorFunction)
    {
        _structMap.AddOrUpdate(
            structType,
            (_) => new JSReference(constructorFunction, isWeak: false),
            (_, _) => throw new InvalidOperationException(
                "Struct already registered for JS export: " + structType.Name));
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
                "Struct not registered for JS export: " + typeof(T).Name);
        }

        JSValue? constructorFunction = constructorReference!.GetValue();
        if (!constructorFunction.HasValue)
        {
            // This should never happen because the reference is "strong".
            throw new InvalidOperationException("Failed to resolve struct constructor reference.");
        }

        return constructorFunction.Value.CallAsConstructor();
    }

    /// <summary>
    /// Imports a module or module property from JavaScript.
    /// </summary>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <param name="esModule">True to import an ES module; false to import a CommonJS module
    /// (default).</param>
    /// <returns>The imported value. When importing from an ES module, this is a JS promise
    /// that resolves to the imported value.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref name="module" /> and
    /// <paramref name="property" /> are null.</exception>
    /// <exception cref="InvalidOperationException">The <see cref="RequireFunction" /> or
    /// <see cref="ImportFunction" /> property was not initialized.</exception>
    public JSValue Import(
        string? module,
        string? property = null,
        bool esModule = false)
    {
        if ((module == null || module == "global") && property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        JSReference reference = _importMap.GetOrAdd((module, property), (_) =>
        {
            // TODO: Consider what to do if the imported property is undefined
            // or not a referenceable type.

            if (module == null || module == "global")
            {
                // Importing a built-in object from `global`.
                JSValue value = JSValue.Global[property!];
                return new JSReference(value);
            }
            else if (property == null)
            {
                // Importing from a module via require() or import().
                JSFunction requireOrImport = esModule ? ImportFunction : RequireFunction;
                JSValue moduleValue = requireOrImport.CallAsStatic(module);
                return new JSReference(moduleValue);
            }
            else
            {
                // Getting a property on a module - import the module first.
                JSValue moduleValue = Import(module, property: null, esModule);
                if (esModule)
                {
                    return new JSReference(((JSPromise)moduleValue).Then(
                        (value) => value.IsUndefined() ?
                            JSValue.Undefined : value.GetProperty(property)));
                }
                else
                {
                    JSValue propertyValue = moduleValue.IsUndefined() ?
                        JSValue.Undefined : moduleValue.GetProperty(property);
                    return new JSReference(propertyValue);
                }
            }
        });
        return reference.GetValue();
    }

    /// <summary>
    /// Imports a module or module property from JavaScript.
    /// </summary>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <param name="esModule">True to import an ES module; false to import a CommonJS module
    /// (default).</param>
    /// <returns>A task that results in the imported value. When importing from an ES module,
    /// the task directly results in the imported value (not a JS promise).</returns>
    /// <exception cref="ArgumentNullException">Both <paramref name="module" /> and
    /// <paramref name="property" /> are null.</exception>
    /// <exception cref="InvalidOperationException">The <see cref="RequireFunction" /> or
    /// <see cref="ImportFunction" /> property was not initialized.</exception>
    public async Task<JSValue> ImportAsync(
        string? module,
        string? property = null,
        bool esModule = false)
    {
        JSValue value = Import(module, property, esModule);

        if (value.IsPromise())
        {
            value = await ((JSPromise)value).AsTask();
        }

        return value;
    }

    /// <summary>
    /// Gets a non-owning annotation associated with this context by its type, or null if none.
    /// </summary>
    public T? GetAnnotation<T>() where T : class
        => _annotations != null && _annotations.TryGetValue(typeof(T), out object? value)
            ? (T)value : null;

    /// <summary>
    /// Associates a non-owning annotation with this context, keyed by its type. The context never
    /// disposes it.
    /// </summary>
    public void SetAnnotation<T>(T value) where T : class
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        (_annotations ??= new())[typeof(T)] = value;
    }

    /// <summary>
    /// Gets an owning annotation associated with this context by its type, or null if none.
    /// </summary>
    public T? GetDisposableAnnotation<T>() where T : class, IDisposable
        => _disposableAnnotations != null &&
           _disposableAnnotations.TryGetValue(typeof(T), out IDisposable? value)
            ? (T)value : null;

    /// <summary>
    /// Associates an owning annotation with this context, keyed by its type. The context disposes
    /// it when the context itself is disposed (at environment teardown). Replacing an existing
    /// annotation of the same type disposes the one being displaced.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The context is already disposed, so the value
    /// would never be disposed.</exception>
    public void SetDisposableAnnotation<T>(T value) where T : class, IDisposable
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (IsDisposed) throw new ObjectDisposedException(nameof(JSRuntimeContext));

        _disposableAnnotations ??= new();
        if (_disposableAnnotations.TryGetValue(typeof(T), out IDisposable? existing) &&
            !ReferenceEquals(existing, value))
        {
            existing.Dispose();
        }

        _disposableAnnotations[typeof(T)] = value;
    }

    /// <summary>
    /// Registers a module instance to be disposed at environment teardown. Unlike
    /// <see cref="SetDisposableAnnotation{T}"/>, several modules can share one context, so instances
    /// are appended rather than keyed by type, and each is disposed once.
    /// </summary>
    internal void AddModuleDisposable(IDisposable disposable)
    {
        if (disposable is null) throw new ArgumentNullException(nameof(disposable));
        _moduleDisposables ??= new();

        // Dedupe by identity, not Equals: a module class may override equality, but each distinct
        // instance must be disposed once.
        foreach (IDisposable existing in _moduleDisposables)
        {
            if (ReferenceEquals(existing, disposable))
            {
                return;
            }
        }

        _moduleDisposables.Add(disposable);
    }

    internal void Dispose()
    {
        if (IsDisposed) return;

        // Disposal calls thread-affine napi (the sync context's RemoveEnvCleanupHook), so it must
        // run on the thread that created the context -- where the instance-data finalizer and the
        // JS dispose functions, the only expected callers, both run.
        if (OwningThreadId != Environment.CurrentManagedThreadId)
        {
            throw new JSInvalidThreadAccessException(
                currentScope: null,
                "A runtime context may be disposed only on the thread that created it.");
        }

        IsDisposed = true;

        // Run every teardown phase even if an earlier one throws, then release the context root and
        // rethrow the first failure. A throwing phase must not skip a later phase or the root
        // release: IsDisposed is already set, so a retry returns immediately and could otherwise
        // strand module instances, annotations, or the context root and its shared block.
        ExceptionDispatchInfo? firstFailure = null;

        // Dispose an already-created sync context only; never construct one here. Disposal can run
        // during env finalization when no scope is current, and creating one then would throw.
        try { _synchronizationContext?.Dispose(); }
        catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }

#if !(NETFRAMEWORK || NETSTANDARD)
        // ConditionalWeakTable<> is not enumerable in .NET Framework; those references are released
        // by their finalizers instead.
        try { DisposeReferences(_objectMap.Select((entry) => entry.Value)); }
        catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }
#endif
        try { DisposeReferences(_classMap.Values); }
        catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }
        try { DisposeReferences(_staticClassMap.Values); }
        catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }
        try { DisposeReferences(_structMap.Values); }
        catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }

        // Disposed after IsDisposed is set, so a late cross-thread post is already a no-op. Each item
        // is guarded so one failure does not skip the rest.
        if (_moduleDisposables != null)
        {
            foreach (IDisposable moduleDisposable in _moduleDisposables)
            {
                try { moduleDisposable.Dispose(); }
                catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }
            }
        }

        if (_disposableAnnotations != null)
        {
            foreach (IDisposable annotation in _disposableAnnotations.Values)
            {
                try { annotation.Dispose(); }
                catch (Exception ex) { firstFailure ??= ExceptionDispatchInfo.Capture(ex); }
            }
        }

        // Release this context's root so it can be collected, even if a phase above threw. The
        // shared block is freed by FinalizeInstanceData once every context on the env is gone.
        if (ContextHandle != default)
        {
            Runtime.GetInstanceData(UncheckedEnvironmentHandle, out nint instanceData);
            if (instanceData != default)
            {
                unsafe
                {
                    // Tombstone the slot (not empty) so the env can never re-associate a new context
                    // (see RegisterInstanceData). The one-context invariant means the slot still
                    // holds this context, but stay defensive.
                    if (((nint*)instanceData)[s_instanceDataSlot] == ContextHandle)
                    {
                        ((nint*)instanceData)[s_instanceDataSlot] = s_disposedSlot;
                    }
                }

                // Free this context's now-unregistered rooting handle. The slot was tombstoned above,
                // so no later FromEnv or finalizer can dereference the freed handle.
                GCHandle.FromIntPtr(ContextHandle).Free();
            }
        }

        // Surface the first teardown failure now that every phase has run and the root is released.
        firstFailure?.Throw();
    }

    private static void DisposeReferences(
        IEnumerable<JSReference> references)
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
    }


    private long _gcHandleCount;

    /// <summary>
    /// Gets the count of GC handles allocated in the current runtime context. Useful for
    /// detecting memory leaks related to .NET + JS interop.
    /// </summary>
    /// <remarks>
    /// JS objects use GC handles to hold onto .NET objects. When a JS object is garbage-
    /// collected, the GC handle it holds (if any) is freed, and then the .NET object becomes
    /// available for .NET garbage-collection (assuming there are no other GC handles or .NET
    /// references to the same object).
    /// </remarks>
    public long GCHandleCount => _gcHandleCount;

#if DEBUG

    /// <summary>
    /// Records the target object and allocation stack trace of a GC handle.
    /// Useful for debugging GC handle leaks. (Only enabled in debug mode.)
    /// </summary>
    public class GCHandleInfo
    {
        public object? Value { get; set; }
        public StackTrace AllocationStackTrace { get; set; } = null!;
    }

    /// <summary>
    /// Maps from GC handles to information about each handle. Useful for debugging
    /// GC handle leaks. (Only enabled in debug mode.)
    /// </summary>
    public Dictionary<nint, GCHandleInfo> GCHandleMap { get; }
        = new Dictionary<nint, GCHandleInfo>();

#endif

    /// <summary>
    /// Allocates a GC handle and tracks the allocation on this runtime context. Call this
    /// method instead of <see cref="GCHandle.Alloc(object?)" /> to track handle allocations.
    /// </summary>
    internal GCHandle AllocGCHandle(object value)
    {
        GCHandle handle = GCHandle.Alloc(value);

        Interlocked.Increment(ref _gcHandleCount);

#if DEBUG
        string targetType = value?.GetType().Name ?? "null";
        Debug.WriteLine($"Allocating GC handle {(nint)handle:X16}: {targetType}");
        GCHandleMap[(nint)handle] = new GCHandleInfo
        {
            Value = value,
            AllocationStackTrace = new StackTrace(skipFrames: 1),
        };
#endif
        return handle;
    }

    /// <summary>
    /// Frees a GC handle previously allocated via <see cref="AllocGCHandle(object)" />
    /// and tracked on this runtime context.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle was not previously allocated
    /// by <see cref="AllocGCHandle(object)" />, or was already freed.</exception>
    internal void FreeGCHandle(GCHandle handle)
    {
        Interlocked.Decrement(ref _gcHandleCount);

#if DEBUG
        string targetType = handle.Target?.GetType().Name ?? "null";
        Debug.WriteLine($"Freeing GC handle {(nint)handle:X16}: {targetType}");
        if (!GCHandleMap.Remove((nint)handle))
        {
            throw new InvalidOperationException(
                $"Freed GC handle to {targetType} was not in the handle map.");
        }
#endif

        handle.Free();
    }

#if NETFRAMEWORK || NETSTANDARD
    private class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
#endif
}
