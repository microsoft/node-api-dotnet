// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static NodejsRuntime;

/// <summary>
/// Abstract base class for a JavaScript runtime.
/// </summary>
/// <remarks>
/// This is a mid-level API; it is lower level than JSValue and related types and methods, while
/// it is higher level than the napi_* native functions.
/// 
/// This middle layer serves two purposes:
///  1. It is used to implement all of the higher level APIs in the library, while encapsulating
///     tedious concerns of dynamic function binding, memory pinning, string encoding, etc.
///  2. It allows swapping out the runtime implementation, either for testing with mocks
///     or for using a JS runtime other than Node.js. (Other runtimes must implement the Node API.)
///
/// Guidelines for this API:
///   - Use .NET style method names, not napi_* function naming
///   - Prefer strings, Span, and nint over pointers
///   - Prefer ref and out over pointers, when practical
///   - Use napi_value instead of JSValue, JSObject, JSArray, etc.
///   - Do not throw exceptions; return status codes instead
///   - Avoid overloads that are purely for convenience
///   - GC handles should be managed at a higher layer; implementations of these APIs should
///     not allocate or dereference GC handles
///
/// The base methods all have default implementations that throw
/// <see cref="NotSupportedException"/>. This makes it easier to create runtime classes
/// (or test mocks) that implement only part of the API surface.
/// </remarks>
public abstract partial class JSRuntime
{
    private static NotSupportedException NS([CallerMemberName] string name = "")
        => new($"The {name} method is not supported by the current JS runtime.");

    public virtual bool IsAvailable(string functionName) => true;

    public virtual napi_status GetVersion(napi_env env, out uint result) => throw NS();

    public virtual napi_status RunScript(napi_env env, napi_value script, out napi_value result) => throw NS();

    public virtual napi_status AddFinalizer(
        napi_env env,
        napi_value value,
        nint finalizeData,
        napi_finalize finalizeCallback,
        nint finalizeHint,
        out napi_ref result) => throw NS();

    public virtual napi_status AddFinalizer(
        napi_env env,
        napi_value value,
        nint finalizeData,
        napi_finalize finalizeCallback,
        nint finalizeHint) => throw NS();

    public virtual napi_status AdjustExternalMemory(
        napi_env env, long changeInBytes, out long result) => throw NS();

    #region Instance data

    public virtual napi_status GetInstanceData(
        napi_env env,
        out nint result) => throw NS();
    public virtual napi_status SetInstanceData(
        napi_env env,
        nint data,
        napi_finalize finalizeCallback,
        nint finalizeHint) => throw NS();

    #endregion

    #region Error handling

    public virtual napi_status CreateError(napi_env env, napi_value code, napi_value msg, out napi_value result) => throw NS();
    public virtual napi_status CreateTypeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result) => throw NS();
    public virtual napi_status CreateRangeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result) => throw NS();
    public virtual napi_status CreateSyntaxError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result) => throw NS();

    public virtual napi_status Throw(napi_env env, napi_value error) => throw NS();
    public virtual napi_status ThrowError(napi_env env, string? code, string msg) => throw NS();
    public virtual napi_status ThrowTypeError(napi_env env, string? code, string msg) => throw NS();
    public virtual napi_status ThrowRangeError(napi_env env, string? code, string msg) => throw NS();
    public virtual napi_status ThrowSyntaxError(napi_env env, string? code, string msg) => throw NS();

    public virtual napi_status IsExceptionPending(napi_env env, out bool result) => throw NS();
    public virtual napi_status GetLastErrorInfo(napi_env env, out napi_extended_error_info? result) => throw NS();
    public virtual napi_status GetAndClearLastException(napi_env env, out napi_value result) => throw NS();

    #endregion

    #region Value type checking

    public virtual napi_status GetValueType(napi_env env, napi_value value, out napi_valuetype result) => throw NS();
    public virtual napi_status IsDate(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsPromise(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsError(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsArray(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsArrayBuffer(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsDetachedArrayBuffer(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsTypedArray(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status IsDataView(napi_env env, napi_value value, out bool result) => throw NS();

    #endregion

    #region Value retrieval

    public virtual napi_status GetValueDouble(napi_env env, napi_value value, out double result) => throw NS();
    public virtual napi_status GetValueInt32(napi_env env, napi_value value, out int result) => throw NS();
    public virtual napi_status GetValueUInt32(napi_env env, napi_value value, out uint result) => throw NS();
    public virtual napi_status GetValueInt64(napi_env env, napi_value value, out long result) => throw NS();
    public virtual napi_status GetValueBigInt64(napi_env env, napi_value value, out long result, out bool lossless) => throw NS();
    public virtual napi_status GetValueBigInt64(napi_env env, napi_value value, out ulong result, out bool lossless) => throw NS();
    public virtual napi_status GetBigIntWordCount(napi_env env, napi_value value, out nuint result) => throw NS();
    public virtual napi_status GetBigIntWords(napi_env env, napi_value value, out int sign, Span<ulong> words, out nuint result) => throw NS();
    public virtual napi_status GetValueBool(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status GetValueStringUtf8(napi_env env, napi_value value, Span<byte> buf, out int result) => throw NS();
    public virtual napi_status GetValueStringUtf16(napi_env env, napi_value value, Span<char> buf, out int result) => throw NS();
    public virtual napi_status GetValueDate(napi_env env, napi_value value, out double result) => throw NS();
    public virtual napi_status GetSymbolFor(napi_env env, string name, out napi_value result) => throw NS();
    public virtual napi_status GetArrayLength(napi_env env, napi_value value, out int result) => throw NS();
    public virtual napi_status GetArrayBufferInfo(
        napi_env env, napi_value value, out nint data, out nuint length) => throw NS();
    public virtual napi_status GetTypedArrayInfo(
        napi_env env,
        napi_value value,
        out napi_typedarray_type type,
        out nuint byteLength,
        out nint data,
        out napi_value arraybuffer,
        out nuint offset) => throw NS();
    public virtual napi_status GetDataViewInfo(
        napi_env env,
        napi_value value,
        out nuint byteLength,
        out nint data,
        out napi_value arraybuffer,
        out nuint offset) => throw NS();
    public virtual napi_status GetValueArrayBuffer(napi_env env, napi_value arraybuffer, out nint data) => throw NS();
    public virtual napi_status GetValueTypedArray(
        napi_env env,
        napi_value typedarray,
        out napi_typedarray_type type,
        out nint data,
        out napi_value arraybuffer,
        out int byte_offset) => throw NS();
    public virtual napi_status GetValueDataView(
        napi_env env,
        napi_value dataview,
        out nint data,
        out napi_value arraybuffer,
        out int byte_offset) => throw NS();
    public virtual napi_status GetValueExternal(napi_env env, napi_value value, out nint result) => throw NS();

    public virtual napi_status StrictEquals(napi_env env, napi_value lhs, napi_value rhs, out bool result) => throw NS();

    #endregion

    #region Value creation

    public virtual napi_status GetGlobal(napi_env env, out napi_value result) => throw NS();
    public virtual napi_status GetUndefined(napi_env env, out napi_value result) => throw NS();
    public virtual napi_status GetNull(napi_env env, out napi_value result) => throw NS();
    public virtual napi_status GetBoolean(napi_env env, bool value, out napi_value result) => throw NS();
    public virtual napi_status CreateNumber(napi_env env, double value, out napi_value result) => throw NS();
    public virtual napi_status CreateNumber(napi_env env, int value, out napi_value result) => throw NS();
    public virtual napi_status CreateNumber(napi_env env, uint value, out napi_value result) => throw NS();
    public virtual napi_status CreateNumber(napi_env env, long value, out napi_value result) => throw NS();
    public virtual napi_status CreateBigInt(napi_env env, long value, out napi_value result) => throw NS();
    public virtual napi_status CreateBigInt(napi_env env, ulong value, out napi_value result) => throw NS();
    public virtual napi_status CreateBigInt(napi_env env, int sign, ReadOnlySpan<ulong> words, out napi_value result) => throw NS();
    public virtual napi_status CreateString(napi_env env, ReadOnlySpan<byte> utf8Str, out napi_value result) => throw NS();
    public virtual napi_status CreateString(napi_env env, ReadOnlySpan<char> utf16Str, out napi_value result) => throw NS();
    public virtual napi_status CreateDate(napi_env env, double time, out napi_value result) => throw NS();
    public virtual napi_status CreateSymbol(napi_env env, napi_value description, out napi_value result) => throw NS();
    public virtual napi_status CreateObject(napi_env env, out napi_value result) => throw NS();
    public virtual napi_status CreateArray(napi_env env, out napi_value result) => throw NS();
    public virtual napi_status CreateArray(napi_env env, int length, out napi_value result) => throw NS();
    public virtual napi_status CreateArrayBuffer(
        napi_env env,
        int byte_length,
        out nint data,
        out napi_value result) => throw NS();
    public virtual napi_status CreateArrayBuffer(
        napi_env env,
        nint external_data,
        int byte_length,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result) => throw NS();
    public virtual napi_status DetachArrayBuffer(napi_env env, napi_value arraybuffer) => throw NS();
    public virtual napi_status CreateTypedArray(
        napi_env env,
        napi_typedarray_type type,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result) => throw NS();
    public virtual napi_status CreateDataView(
        napi_env env,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result) => throw NS();
    public virtual napi_status CreateExternal(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result) => throw NS();
    public virtual napi_status CreateFunction(
        napi_env env,
        string? name,
        napi_callback cb,
        nint data,
        out napi_value result) => throw NS();
    public virtual napi_status CreatePromise(napi_env env, out napi_deferred deferred, out napi_value promise) => throw NS();
    public virtual napi_status ResolveDeferred(napi_env env, napi_deferred deferred, napi_value resolution) => throw NS();
    public virtual napi_status RejectDeferred(napi_env env, napi_deferred deferred, napi_value rejection) => throw NS();

    #endregion

    #region Value coercion

    public virtual napi_status CoerceToBool(napi_env env, napi_value value, out napi_value result) => throw NS();
    public virtual napi_status CoerceToNumber(napi_env env, napi_value value, out napi_value result) => throw NS();
    public virtual napi_status CoerceToObject(napi_env env, napi_value value, out napi_value result) => throw NS();
    public virtual napi_status CoerceToString(napi_env env, napi_value value, out napi_value result) => throw NS();

    #endregion

    #region Handle scopes

    public virtual napi_status OpenHandleScope(napi_env env, out napi_handle_scope result) => throw NS();
    public virtual napi_status CloseHandleScope(napi_env env, napi_handle_scope scope) => throw NS();
    public virtual napi_status OpenEscapableHandleScope(
        napi_env env,
        out napi_escapable_handle_scope result) => throw NS();
    public virtual napi_status CloseEscapableHandleScope(napi_env env, napi_escapable_handle_scope scope) => throw NS();
    public virtual napi_status EscapeHandle(
        napi_env env,
        napi_escapable_handle_scope scope,
        napi_value escapee,
        out napi_value result) => throw NS();

    #endregion

    #region References

    public virtual napi_status CreateReference(
        napi_env env,
        napi_value value,
        uint initialRefcount,
        out napi_ref result) => throw NS();
    public virtual napi_status DeleteReference(napi_env env, napi_ref @ref) => throw NS();
    public virtual napi_status RefReference(napi_env env, napi_ref @ref, out uint result) => throw NS();
    public virtual napi_status UnrefReference(napi_env env, napi_ref @ref, out uint result) => throw NS();
    public virtual napi_status GetReferenceValue(napi_env env, napi_ref @ref, out napi_value result) => throw NS();

    #endregion

    #region Function calls

    public virtual napi_status CallFunction(
        napi_env env,
        napi_value recv,
        napi_value func,
        ReadOnlySpan<napi_value> args,
        out napi_value result) => throw NS();
    public virtual napi_status GetCallbackInfo(
        napi_env env,
        napi_callback_info cbinfo,
        out int argc,
        out nint data) => throw NS();
    public virtual napi_status GetCallbackArgs(
        napi_env env,
        napi_callback_info cbinfo,
        Span<napi_value> args,
        out napi_value this_arg) => throw NS();
    public virtual napi_status GetNewTarget(
        napi_env env,
        napi_callback_info cbinfo,
        out napi_value result) => throw NS();

    #endregion

    #region Object properties

    public virtual napi_status HasProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result) => throw NS();
    public virtual napi_status HasOwnProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result) => throw NS();
    public virtual napi_status GetProperty(
        napi_env env, napi_value js_object, napi_value key, out napi_value result) => throw NS();
    public virtual napi_status SetProperty(
        napi_env env, napi_value js_object, napi_value key, napi_value value) => throw NS();
    public virtual napi_status DeleteProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result) => throw NS();

    public virtual napi_status HasNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out bool result) => throw NS();
    public virtual napi_status GetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out napi_value result) => throw NS();
    public virtual napi_status SetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, napi_value value) => throw NS();

    public virtual napi_status HasElement(
        napi_env env, napi_value js_object, uint index, out bool result) => throw NS();
    public virtual napi_status GetElement(
        napi_env env, napi_value js_object, uint index, out napi_value result) => throw NS();
    public virtual napi_status SetElement(
        napi_env env, napi_value js_object, uint index, napi_value value) => throw NS();
    public virtual napi_status DeleteElement(
        napi_env env, napi_value js_object, uint index, out bool result) => throw NS();

    #endregion

    #region Property and class definition

    public virtual napi_status GetPropertyNames(
        napi_env env,
        napi_value js_object,
        out napi_value result) => throw NS();
    public virtual napi_status GetAllPropertyNames(
        napi_env env,
        napi_value js_object,
        napi_key_collection_mode key_mode,
        napi_key_filter key_filter,
        napi_key_conversion key_conversion,
        out napi_value result) => throw NS();
    public virtual napi_status Freeze(napi_env env, napi_value value) => throw NS();
    public virtual napi_status Seal(napi_env env, napi_value value) => throw NS();
    public virtual napi_status DefineProperties(
        napi_env env,
        napi_value js_object,
        ReadOnlySpan<napi_property_descriptor> properties) => throw NS();
    public virtual napi_status DefineClass(
        napi_env env,
        string name,
        napi_callback constructor,
        nint data,
        ReadOnlySpan<napi_property_descriptor> properties,
        out napi_value result) => throw NS();

    public virtual napi_status GetPrototype(
        napi_env env,
        napi_value js_object,
        out napi_value result) => throw NS();
    public virtual napi_status NewInstance(
        napi_env env,
        napi_value constructor,
        ReadOnlySpan<napi_value> args,
        out napi_value result) => throw NS();
    public virtual napi_status InstanceOf(
        napi_env env,
        napi_value js_object,
        napi_value constructor,
        out bool result) => throw NS();

    public virtual napi_status Wrap(
        napi_env env,
        napi_value js_object,
        nint native_object,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_ref result) => throw NS();

    public virtual napi_status Wrap(
        napi_env env,
        napi_value js_object,
        nint native_object,
        napi_finalize finalize_cb,
        nint finalize_hint) => throw NS();

    public virtual napi_status Unwrap(napi_env env, napi_value js_object, out nint result) => throw NS();
    public virtual napi_status RemoveWrap(napi_env env, napi_value js_object, out nint result) => throw NS();
    public virtual napi_status SetObjectTypeTag(
        napi_env env, napi_value value, Guid typeTag) => throw NS();
    public virtual napi_status CheckObjectTypeTag(
        napi_env env, napi_value value, Guid typeTag, out bool result) => throw NS();

    #endregion

    #region Thread-safe functions

    public virtual napi_status CreateThreadSafeFunction(
        napi_env env,
        napi_value func,
        napi_value asyncResource,
        napi_value asyncResourceName,
        int maxQueueSize,
        int initialThreadCount,
        nint threadFinalizeData,
        napi_finalize threadFinalizeCallback,
        nint context,
        napi_threadsafe_function_call_js callJSCallback,
        out napi_threadsafe_function result) => throw NS();
    public virtual napi_status CallThreadSafeFunction(
        napi_threadsafe_function func,
        nint data,
        napi_threadsafe_function_call_mode isBlocking) => throw NS();
    public virtual napi_status GetThreadSafeFunctionContext(
        napi_threadsafe_function func,
        out nint result) => throw NS();
    public virtual napi_status AcquireThreadSafeFunction(napi_threadsafe_function func) => throw NS();
    public virtual napi_status ReleaseThreadSafeFunction(
        napi_threadsafe_function func,
        napi_threadsafe_function_release_mode mode) => throw NS();
    public virtual napi_status RefThreadSafeFunction(napi_env env, napi_threadsafe_function func) => throw NS();
    public virtual napi_status UnrefThreadSafeFunction(napi_env env, napi_threadsafe_function func) => throw NS();

    #endregion

    #region Async work

    public virtual napi_status AsyncInit(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        out napi_async_context result) => throw NS();
    public virtual napi_status AsyncDestroy(napi_env env, napi_async_context asyncContext) => throw NS();
    public virtual napi_status CreateAsyncWork(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        napi_async_execute_callback execute,
        napi_async_complete_callback complete,
        nint data,
        out napi_async_work result) => throw NS();
    public virtual napi_status QueueAsyncWork(napi_env env, napi_async_work work) => throw NS();
    public virtual napi_status DeleteAsyncWork(napi_env env, napi_async_work work) => throw NS();
    public virtual napi_status CancelAsyncWork(napi_env env, napi_async_work work) => throw NS();

    public virtual napi_status MakeCallback(
        napi_env env,
        napi_async_context asyncContext,
        napi_value recv,
        napi_value func,
        Span<napi_value> args,
        out napi_value result) => throw NS();
    public virtual napi_status OpenCallbackScope(
        napi_env env,
        napi_value resourceObject,
        napi_async_context asyncContext,
        out napi_callback_scope result) => throw NS();
    public virtual napi_status CloseCallbackScope(napi_env env, napi_callback_scope scope) => throw NS();

    #endregion

    #region Cleanup hooks

    public virtual napi_status AddAsyncCleanupHook(
        napi_env env,
        napi_async_cleanup_hook hook,
        nint arg,
        out napi_async_cleanup_hook_handle result) => throw NS();
    public virtual napi_status RemoveAsyncCleanupHook(napi_async_cleanup_hook_handle removeHandle) => throw NS();
    public virtual napi_status AddEnvCleanupHook(napi_env env, napi_cleanup_hook func, nint arg) => throw NS();
    public virtual napi_status RemoveEnvCleanupHook(napi_env env, napi_cleanup_hook func, nint arg) => throw NS();

    #endregion

    #region Buffers

    public virtual napi_status IsBuffer(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status CreateBuffer(napi_env env, Span<byte> data, out napi_value result) => throw NS();
    public virtual napi_status CreateBufferCopy(
        napi_env env,
        ReadOnlySpan<byte> data,
        out nint resultData,
        out napi_value result) => throw NS();
    public virtual napi_status CreateExternalBuffer(
        napi_env env,
        Span<byte> data,
        napi_finalize finalizeCallback,
        nint finalizeHint,
        out napi_value result) => throw NS();
    public virtual napi_status GetBufferInfo(
        napi_env env,
        napi_value value,
        out nint data,
        out nuint length) => throw NS();

    #endregion

    #region Misc Node.js functions

    [DoesNotReturn]
    public virtual void FatalError(string location, string message) => throw NS();
    public virtual napi_status FatalException(napi_env env, napi_value err) => throw NS();

    public virtual napi_status GetUVEventLoop(napi_env env, out uv_loop_t result) => throw NS();

    public virtual void RegisterModule(ref napi_module module) => throw NS();
    public virtual napi_status GetModuleFileName(napi_env env, out string result) => throw NS();
    public virtual napi_status GetNodeVersion(napi_env env, out napi_node_version result) => throw NS();

    #endregion

    #region Embedding

    public virtual node_embedding_status
        EmbeddingOnError(node_embedding_handle_error_functor error_handler) => throw NS();

    public virtual node_embedding_status EmbeddingSetApiVersion(
        int embedding_api_version,
        int node_api_version) => throw NS();

    public virtual node_embedding_status EmbeddingRunMain(
        ReadOnlySpan<string> args,
        node_embedding_configure_platform_functor_ref configure_platform,
        node_embedding_configure_runtime_functor_ref configure_runtime) => throw NS();

    public virtual node_embedding_status EmbeddingCreatePlatform(
        ReadOnlySpan<string> args,
        node_embedding_configure_platform_functor_ref configure_platform,
        out node_embedding_platform result) => throw NS();

    public virtual node_embedding_status
        EmbeddingDeletePlatform(node_embedding_platform platform) => throw NS();

    public virtual node_embedding_status EmbeddingPlatformSetFlags(
        node_embedding_platform_config platform_config,
        node_embedding_platform_flags flags) => throw NS();

    public virtual node_embedding_status EmbeddingPlatformGetParsedArgs(
        node_embedding_platform platform,
        node_embedding_get_args_functor_ref get_args,
        node_embedding_get_args_functor_ref get_runtime_args) => throw NS();

    public virtual node_embedding_status EmbeddingRunRuntime(
        node_embedding_platform platform,
        node_embedding_configure_runtime_functor_ref configure_runtime) => throw NS();

    public virtual node_embedding_status EmbeddingCreateRuntime(
        node_embedding_platform platform,
        node_embedding_configure_runtime_functor_ref configure_runtime,
        out node_embedding_runtime result) => throw NS();

    public virtual node_embedding_status
        EmbeddingDeleteRuntime(node_embedding_runtime runtime) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeSetFlags(
        node_embedding_runtime_config runtime_config,
        node_embedding_runtime_flags flags) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeSetArgs(
        node_embedding_runtime_config runtime_config,
        ReadOnlySpan<string> args,
        ReadOnlySpan<string> runtime_args) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeOnPreload(
        node_embedding_runtime_config runtime_config,
        node_embedding_preload_functor run_preload) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeOnStartExecution(
        node_embedding_runtime_config runtime_config,
        node_embedding_start_execution_functor start_execution,
        node_embedding_handle_result_functor handle_result) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeAddModule(
        node_embedding_runtime_config runtime_config,
        string moduleName,
        node_embedding_initialize_module_functor init_module,
        int module_node_api_version) => throw NS();

    public virtual node_embedding_status EmbeddingRuntimeSetTaskRunner(
        node_embedding_runtime_config runtime_config,
        node_embedding_post_task_functor post_task) => throw NS();

    public virtual node_embedding_status EmbeddingRunEventLoop(
        node_embedding_runtime runtime,
        node_embedding_event_loop_run_mode run_mode,
        out bool has_more_work) => throw NS();

    public virtual node_embedding_status
        EmbeddingCompleteEventLoop(node_embedding_runtime runtime) => throw NS();

    public virtual node_embedding_status
        EmbeddingTerminateEventLoop(node_embedding_runtime runtime) => throw NS();

    public virtual node_embedding_status EmbeddingRunNodeApi(
        node_embedding_runtime runtime,
        node_embedding_run_node_api_functor_ref run_node_api) => throw NS();

    public virtual node_embedding_status EmbeddingOpenNodeApiScope(
        node_embedding_runtime runtime,
        out node_embedding_node_api_scope node_api_scope,
        out napi_env env) => throw NS();

    public virtual node_embedding_status EmbeddingCloseNodeApiScope(
        node_embedding_runtime runtime,
        node_embedding_node_api_scope node_api_scope) => throw NS();

    #endregion
}
