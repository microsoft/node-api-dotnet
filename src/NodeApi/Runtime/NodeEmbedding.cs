// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
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

    public static void Initialize(string libNodePath)
    {
        if (string.IsNullOrEmpty(libNodePath)) throw new ArgumentNullException(nameof(libNodePath));
        if (s_jsRuntime != null)
        {
            throw new InvalidOperationException(
                "The JSRuntime can be initialized only once per process.");
        }
        nint libnodeHandle = NativeLibrary.Load(libNodePath);
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
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.GetOrCreate(runtime)
                ?? throw new InvalidOperationException("Embedding runtime is not found");
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
            //TODO: Unwrap from runtime
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.GetOrCreate(runtime)
                ?? throw new InvalidOperationException("Embedding runtime is not found");
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
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.GetOrCreate(runtime)
                ?? throw new InvalidOperationException("Embedding runtime is not found");
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
            NodeEmbeddingRuntime embeddingRuntime = NodeEmbeddingRuntime.GetOrCreate(runtime)
                ?? throw new InvalidOperationException("Embedding runtime is not found");
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
    internal static unsafe void NodeApiRunCallbackAdapter(
        nint cb_data,
        napi_env env)
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
