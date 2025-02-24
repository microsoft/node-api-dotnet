// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.IO;
#if UNMANAGED_DELEGATES
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;

using static JSRuntime;
using static NodejsRuntime;

/// <summary>
/// Shared code for the Node.js embedding classes.
/// </summary>
public sealed class NodeEmbedding
{
    public static readonly int EmbeddingApiVersion = 1;
    public static readonly int NodeApiVersion = 8;

    private static JSRuntime? s_jsRuntime;

    public static JSRuntime JSRuntime
    {
        get
        {
            if (s_jsRuntime == null)
            {
                throw new InvalidOperationException("The JSRuntime is not initialized.");
            }
            return s_jsRuntime;
        }
    }

#if NETFRAMEWORK || NETSTANDARD

    /// <summary>
    /// Discovers the fallback RID of the current platform.
    /// </summary>
    /// <returns></returns>
    static string? GetFallbackRuntimeIdentifier()
    {
        string? arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return arch is not null ? $"win-{arch}" : "win";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return arch is not null ? $"linux-{arch}" : "linux";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return arch is not null ? $"osx-{arch}" : "osx";

        return null;
    }

    /// <summary>
    /// Returns a version of the library name with the OS specific prefix and suffix.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    static string? MapLibraryName(string name)
    {
        if (name is null)
            return null;

        if (Path.HasExtension(name))
            return name;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return name + ".dll";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return name + ".dylib";

        return name + ".so";
    }

    /// <summary>
    /// Scans the runtimes/{rid}/native directory relative to the application base directory for the native library.
    /// </summary>
    /// <returns></returns>
    static string? FindLocalLibNode()
    {
        if (GetFallbackRuntimeIdentifier() is string rid)
            if (MapLibraryName("libnode") is string fileName)
                if (Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName) is string libPath)
                    if (File.Exists(libPath))
                        return libPath;

        return null;
    }

#endif

    /// <summary>
    /// Attempts to load the libnode library using the discovery logic as appropriate for the platform.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="DllNotFoundException"></exception>
    static nint LoadDefaultLibNode()
    {
#if NETFRAMEWORK || NETSTANDARD
        // search local paths that would be provided by LibNode packages
        string? path = FindLocalLibNode();
        if (path is not null)
            if (NativeLibrary.TryLoad(path, out nint handle))
                return handle;
#else
        // search using default dependency context
        if (NativeLibrary.TryLoad("libnode", typeof(NodeEmbedding).Assembly, null, out nint handle))
            return handle;
#endif

        // attempt to load from default OS search paths
        if (NativeLibrary.TryLoad("libnode", out nint defaultHandle))
            return defaultHandle;

        throw new DllNotFoundException("The JSRuntime cannot locate the libnode shared library.");
    }

    public static void Initialize(string? libNodePath)
    {
        if (s_jsRuntime != null)
        {
            throw new InvalidOperationException(
                "The JSRuntime can be initialized only once per process.");
        }
        nint libnodeHandle = libNodePath is null ? LoadDefaultLibNode() : NativeLibrary.Load(libNodePath);
        s_jsRuntime = new NodejsRuntime(libnodeHandle);
    }

    public delegate void ConfigurePlatformCallback(node_embedding_platform_config platformConfig);
    public delegate void ConfigureRuntimeCallback(
        node_embedding_platform platform, node_embedding_runtime_config runtimeConfig);
    public delegate void PreloadCallback(
        NodeEmbeddingRuntime runtime, JSValue process, JSValue require);
    public delegate JSValue LoadingCallback(
        NodeEmbeddingRuntime runtime, JSValue process, JSValue require, JSValue runCommonJS);
    public delegate void LoadedCallback(
        NodeEmbeddingRuntime runtime, JSValue loadResul);
    public delegate JSValue InitializeModuleCallback(
        NodeEmbeddingRuntime runtime, string moduleName, JSValue exports);
    public delegate void RunTaskCallback();
    public delegate bool PostTaskCallback(
        node_embedding_task_run_callback runTask,
        nint taskData,
        node_embedding_data_release_callback releaseTaskData);
    public delegate void RunNodeApiCallback();

    public struct Functor<T>
    {
        public nint Data;
        public T Callback;
        public readonly unsafe node_embedding_data_release_callback DataRelease =>
            new(s_releaseDataCallback);
    }

    public struct FunctorRef<T> : IDisposable
    {
        public nint Data;
        public T Callback;

        public readonly void Dispose()
        {
            if (Data != default)
                GCHandle.FromIntPtr(Data).Free();
        }
    }

    public static unsafe FunctorRef<node_embedding_platform_configure_callback>
    CreatePlatformConfigureFunctorRef(ConfigurePlatformCallback? callback) => new()
    {
        Data = callback != null ? (nint)GCHandle.Alloc(callback) : default,
        Callback = callback != null
            ? new node_embedding_platform_configure_callback(s_platformConfigureCallback)
            : default
    };

    public static unsafe FunctorRef<node_embedding_runtime_configure_callback>
    CreateRuntimeConfigureFunctorRef(ConfigureRuntimeCallback? callback) => new()
    {
        Data = callback != null ? (nint)GCHandle.Alloc(callback) : default,
        Callback = callback != null
            ? new node_embedding_runtime_configure_callback(s_runtimeConfigureCallback)
            : default
    };

    public static unsafe Functor<node_embedding_runtime_preload_callback>
    CreateRuntimePreloadFunctor(PreloadCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_runtime_preload_callback(s_runtimePreloadCallback)
    };

    public static unsafe Functor<node_embedding_runtime_loading_callback>
    CreateRuntimeLoadingFunctor(LoadingCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_runtime_loading_callback(s_runtimeLoadingCallback)
    };

    public static unsafe Functor<node_embedding_runtime_loaded_callback>
    CreateRuntimeLoadedFunctor(LoadedCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_runtime_loaded_callback(s_runtimeLoadedCallback)
    };

    public static unsafe Functor<node_embedding_module_initialize_callback>
    CreateModuleInitializeFunctor(InitializeModuleCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_module_initialize_callback(s_moduleInitializeCallback)
    };

    public static unsafe Functor<node_embedding_task_post_callback>
    CreateTaskPostFunctor(PostTaskCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_task_post_callback(s_taskPostCallback)
    };

    public static unsafe FunctorRef<node_embedding_node_api_run_callback>
    CreateNodeApiRunFunctorRef(RunNodeApiCallback callback) => new()
    {
        Data = (nint)GCHandle.Alloc(callback),
        Callback = new node_embedding_node_api_run_callback(s_nodeApiRunCallback)
    };

#if UNMANAGED_DELEGATES
    internal static readonly unsafe delegate* unmanaged[Cdecl]<nint, NodeEmbeddingStatus>
        s_releaseDataCallback = &ReleaseDataCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, node_embedding_platform_config, NodeEmbeddingStatus>
        s_platformConfigureCallback = &PlatformConfigureCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint,
        node_embedding_platform,
        node_embedding_runtime_config,
        NodeEmbeddingStatus>
        s_runtimeConfigureCallback = &RuntimeConfigureCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, node_embedding_runtime, napi_env, napi_value, napi_value, void>
        s_runtimePreloadCallback = &RuntimePreloadCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, node_embedding_runtime, napi_env, napi_value, napi_value, napi_value, napi_value>
        s_runtimeLoadingCallback = &RuntimeLoadingCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, node_embedding_runtime, napi_env, napi_value, void>
        s_runtimeLoadedCallback = &RuntimeLoadedCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, node_embedding_runtime, napi_env, nint, napi_value, napi_value>
        s_moduleInitializeCallback = &ModuleInitializeCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<nint, NodeEmbeddingStatus>
        s_taskRunTaskCallback = &TaskRunCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint,
        node_embedding_task_run_callback,
        nint,
        node_embedding_data_release_callback,
        nint,
        NodeEmbeddingStatus>
        s_taskPostCallback = &TaskPostCallbackAdapter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]<
        nint, napi_env, void>
        s_nodeApiRunCallback = &NodeApiRunCallbackAdapter;
#else
    internal static readonly node_embedding_data_release_callback.Delegate
        s_releaseDataCallback = ReleaseDataCallbackAdapter;
    internal static readonly node_embedding_platform_configure_callback.Delegate
        s_platformConfigureCallback = PlatformConfigureCallbackAdapter;
    internal static readonly node_embedding_runtime_configure_callback.Delegate
        s_runtimeConfigureCallback = RuntimeConfigureCallbackAdapter;
    internal static readonly node_embedding_runtime_preload_callback.Delegate
        s_runtimePreloadCallback = RuntimePreloadCallbackAdapter;
    internal static readonly node_embedding_runtime_loading_callback.Delegate
        s_runtimeLoadingCallback = RuntimeLoadingCallbackAdapter;
    internal static readonly node_embedding_runtime_loaded_callback.Delegate
        s_runtimeLoadedCallback = RuntimeLoadedCallbackAdapter;
    internal static readonly node_embedding_module_initialize_callback.Delegate
        s_moduleInitializeCallback = ModuleInitializeCallbackAdapter;
    internal static readonly node_embedding_task_run_callback.Delegate
        s_taskRunCallback = TaskRunCallbackAdapter;
    internal static readonly node_embedding_task_post_callback.Delegate
        s_taskPostCallback = TaskPostCallbackAdapter;
    internal static readonly node_embedding_node_api_run_callback.Delegate
        s_nodeApiRunCallback = NodeApiRunCallbackAdapter;
#endif

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe NodeEmbeddingStatus ReleaseDataCallbackAdapter(nint data)
    {
        if (data != default)
        {
            GCHandle.FromIntPtr(data).Free();
        }
        return NodeEmbeddingStatus.OK;
    }


#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe NodeEmbeddingStatus PlatformConfigureCallbackAdapter(
        nint cb_data,
        node_embedding_platform_config platform_config)
    {
        try
        {
            var callback = (ConfigurePlatformCallback)GCHandle.FromIntPtr(cb_data).Target!;
            callback(platform_config);
            return NodeEmbeddingStatus.OK;
        }
        catch (Exception ex)
        {
            JSRuntime.EmbeddingSetLastErrorMessage(ex.Message.AsSpan());
            return NodeEmbeddingStatus.GenericError;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe NodeEmbeddingStatus RuntimeConfigureCallbackAdapter(
        nint cb_data,
        node_embedding_platform platform,
        node_embedding_runtime_config runtime_config)
    {
        try
        {
            var callback = (ConfigureRuntimeCallback)GCHandle.FromIntPtr(cb_data).Target!;
            callback(platform, runtime_config);
            return NodeEmbeddingStatus.OK;
        }
        catch (Exception ex)
        {
            JSRuntime.EmbeddingSetLastErrorMessage(ex.Message.AsSpan());
            return NodeEmbeddingStatus.GenericError;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe void RuntimePreloadCallbackAdapter(
        nint cb_data,
        node_embedding_runtime runtime,
        napi_env env,
        napi_value process,
        napi_value require)
    {
        using var jsValueScope = new JSValueScope(JSValueScopeType.Root, env, JSRuntime);
        try
        {
            var callback = (PreloadCallback)GCHandle.FromIntPtr(cb_data).Target!;
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.FromHandle(runtime);
            callback(embeddingRuntime, new JSValue(process), new JSValue(require));
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe napi_value RuntimeLoadingCallbackAdapter(
        nint cb_data,
        node_embedding_runtime runtime,
        napi_env env,
        napi_value process,
        napi_value require,
        napi_value run_cjs)
    {
        using var jsValueScope = new JSValueScope(JSValueScopeType.Root, env, JSRuntime);
        try
        {
            var callback = (LoadingCallback)GCHandle.FromIntPtr(cb_data).Target!;
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.FromHandle(runtime);
            return (napi_value)callback(
                embeddingRuntime, new JSValue(process), new JSValue(require), new JSValue(run_cjs));
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
            return napi_value.Null;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe void RuntimeLoadedCallbackAdapter(
        nint cb_data,
        node_embedding_runtime runtime,
        napi_env env,
        napi_value loading_result)
    {
        using var jsValueScope = new JSValueScope(JSValueScopeType.Root, env, JSRuntime);
        try
        {
            var callback = (LoadedCallback)GCHandle.FromIntPtr(cb_data).Target!;
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.FromHandle(runtime);
            callback(embeddingRuntime, new JSValue(loading_result));
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe napi_value ModuleInitializeCallbackAdapter(
        nint cb_data,
        node_embedding_runtime runtime,
        napi_env env,
        nint module_name,
        napi_value exports)
    {
        using var jsValueScope = new JSValueScope(JSValueScopeType.Root, env, JSRuntime);
        try
        {
            var callback = (InitializeModuleCallback)GCHandle.FromIntPtr(cb_data).Target!;
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.FromHandle(runtime);
            return (napi_value)callback(
                embeddingRuntime,
                Utf8StringArray.PtrToStringUTF8((byte*)module_name),
                new JSValue(exports));
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
            return napi_value.Null;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe NodeEmbeddingStatus TaskRunCallbackAdapter(nint cb_data)
    {
        try
        {
            var callback = (RunTaskCallback)GCHandle.FromIntPtr(cb_data).Target!;
            callback();
            return NodeEmbeddingStatus.OK;
        }
        catch (Exception ex)
        {
            JSRuntime.EmbeddingSetLastErrorMessage(ex.Message.AsSpan());
            return NodeEmbeddingStatus.GenericError;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe NodeEmbeddingStatus TaskPostCallbackAdapter(
        nint cb_data,
        node_embedding_task_run_callback run_task,
        nint task_data,
        node_embedding_data_release_callback release_task_data,
        nint is_posted)
    {
        try
        {
            var callback = (PostTaskCallback)GCHandle.FromIntPtr(cb_data).Target!;
            bool isPosted = callback(run_task, task_data, release_task_data);
            if (is_posted != default)
            {
                *(c_bool*)is_posted = isPosted;
            }

            return NodeEmbeddingStatus.OK;
        }
        catch (Exception ex)
        {
            JSRuntime.EmbeddingSetLastErrorMessage(ex.Message.AsSpan());
            return NodeEmbeddingStatus.GenericError;
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    internal static unsafe void NodeApiRunCallbackAdapter(nint cb_data, napi_env env)
    {
        using var jsValueScope = new JSValueScope(JSValueScopeType.Root, env, JSRuntime);
        try
        {
            var callback = (RunNodeApiCallback)GCHandle.FromIntPtr(cb_data).Target!;
            callback();
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
        }
    }
}
