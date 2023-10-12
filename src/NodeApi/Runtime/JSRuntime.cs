// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Runtime;

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
///   - Prefer strings, Span<T> and nint over pointers
///   - Prefer ref & out over pointers, when practical
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
public abstract class JSRuntime
{
    private static NotSupportedException NS([CallerMemberName] string name = "")
        => new($"The {name} method is not supported by the current JS runtime.");

    public virtual napi_status GetVersion(napi_env env, out uint result) => throw NS();

    public virtual napi_status RunScript(napi_env env, napi_value script, out napi_value result) => throw NS();

    #region Instance data

    public virtual napi_status GetInstanceData(
        napi_env env,
        out nint result) => throw NS();
    public virtual napi_status SetInstanceData(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint) => throw NS();

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
    public virtual napi_status GetLastErrorInfo(napi_env env, out napi_extended_error_info result) => throw NS();
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
    public virtual napi_status GetValueBool(napi_env env, napi_value value, out bool result) => throw NS();
    public virtual napi_status GetValueStringUtf8(napi_env env, napi_value value, Span<byte> buf, out int result) => throw NS();
    public virtual napi_status GetValueStringUtf16(napi_env env, napi_value value, Span<char> buf, out int result) => throw NS();
    public virtual napi_status GetValueDate(napi_env env, napi_value value, out double result) => throw NS();
    public virtual napi_status GetSymbolFor(napi_env env, string name, out napi_value result) => throw NS();
    public virtual napi_status GetArrayLength(napi_env env, napi_value value, out int result) => throw NS();
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

    // TODO

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
        uint initial_refcount,
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
    public virtual napi_status Unwrap(napi_env env, napi_value js_object, out nint result) => throw NS();
    public virtual napi_status RemoveWrap(napi_env env, napi_value js_object, out nint result) => throw NS();

    #endregion
}
