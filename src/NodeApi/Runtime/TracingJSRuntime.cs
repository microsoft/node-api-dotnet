// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static NodejsRuntime;

/// <summary>
/// Wraps a <see cref="JSRuntime" /> and traces all the calls from .NET to JS, including
/// basic inspection of arguments, out values, and return status.
/// </summary>
/// <example>
/// Produces output similar to:
/// <code>
/// < CreateObject()
/// > CreateObject(000001E984E19BB0 object)
/// < DefineProperties(000001E984E19BB0 object, [hello(), toString()])
/// > DefineProperties(ok)
/// < Wrap(000001E984E19BB0 object, 0x000001E984CE4580 RuntimeType)
/// > Wrap(@000001E9808E7840 000001E984E19BF0 object)
/// < DefineProperties(000001E984E19B80 object, [Example])
/// > DefineProperties(ok)
/// < GetValueType(000001E984E19B80 object)
/// > GetValueType(object)
/// < GetInstanceData()
/// > GetInstanceData(0x000001E984CE1300 JSRuntimeContext)
/// < GetCallbackInfo(0000000CB8DFE560)
/// > GetCallbackInfo(1, 0x000001E984CE4590 JSPropertyDescriptor)
/// < GetCallbackArgs(0000000CB8DFE560, [1])
/// > GetCallbackArgs(0000000CB8DFE8C8 object, 0000000CB8DFE8D0 string ".NET")
/// </code>
/// </example>
public class TracingJSRuntime : JSRuntime
{
    public const int CallTrace = 1;
    public const int ReturnTrace = 2;
    public const int ExceptionTrace = 2;

    private readonly JSRuntime _runtime;
    private bool _formatting;

    public TracingJSRuntime(JSRuntime runtime, TraceSource trace)
    {
        _runtime = runtime;
        Trace = trace;
    }

    public TraceSource Trace { get; }

    public override bool IsAvailable(string functionName) => _runtime.IsAvailable(functionName);

    #region Formatting

    private static string Format(napi_platform platform) => platform.Handle.ToString("X16");
    private static string Format(napi_env env) => env.Handle.ToString("X16");
    private static string Format(napi_handle_scope scope) => scope.Handle.ToString("X16");
    private static string Format(napi_escapable_handle_scope scope) => scope.Handle.ToString("X16");
    private static string Format(napi_callback_info scope) => scope.Handle.ToString("X16");
    private static string Format(napi_threadsafe_function function)
        => function.Handle.ToString("X16");
    private static string Format(napi_callback_scope scope) => scope.Handle.ToString("X16");
    private static string Format(napi_async_context context) => context.Handle.ToString("X16");
    private static string Format(napi_async_work work) => work.Handle.ToString("X16");
    private static string Format(napi_async_cleanup_hook_handle hook)
         => hook.Handle.ToString("X16");
    private static string Format(uv_loop_t loop) => loop.Handle.ToString("X16");

    private string GetValueString(napi_env env, napi_value value)
    {
        if (_runtime.GetValueStringUtf16(env, value, [], out int length) ==
            napi_status.napi_ok)
        {
            string elipses = string.Empty;
            if (length > 32)
            {
                length = 32;
                elipses = "...";
            }

            Span<char> chars = stackalloc char[length + 1];
            if (_runtime.GetValueStringUtf16(env, value, chars, out _) ==
                napi_status.napi_ok)
            {
                return new string(chars.Slice(0, length).ToArray()) + elipses;
            }
        }

        return string.Empty;
    }

    private napi_value GetValueProperty(napi_env env, napi_value value, string property)
    {
        return _runtime.CreateString(env, property.AsSpan(), out napi_value propertyName) ==
            napi_status.napi_ok &&
            _runtime.GetProperty(env, value, propertyName, out napi_value propertyValue) ==
            napi_status.napi_ok ? propertyValue : default;
    }

    private string Format(napi_env env, napi_value value)
    {
        if (_formatting) return string.Empty; // Prevent tracing while formatting.
        _formatting = true;
        try
        {
            _runtime.GetValueType(env, value, out napi_valuetype valueType);

            string valueString = string.Empty;
            switch (valueType)
            {
                case napi_valuetype.napi_string:
                    valueString = $" \"{GetValueString(env, value)}\"";
                    break;

                case napi_valuetype.napi_number:
                    if (_runtime.GetValueDouble(env, value, out double number) ==
                        napi_status.napi_ok)
                    {
                        valueString = $" {number}";
                    }
                    break;

                case napi_valuetype.napi_boolean:
                    if (_runtime.GetValueBool(env, value, out bool boolean) ==
                        napi_status.napi_ok)
                    {
                        valueString = $" {(boolean ? "true" : "false")}";
                    }
                    break;

                case napi_valuetype.napi_object:
                    if (_runtime.IsArray(env, value, out bool isArray) ==
                        napi_status.napi_ok && isArray)
                    {
                        napi_value size = GetValueProperty(env, value, "length");
                        if (size != default)
                        {
                            valueString = $" [{size}]";
                        }
                    }
                    else if (_runtime.IsPromise(env, value, out bool isPromise) ==
                        napi_status.napi_ok && isPromise)
                    {
                        valueString = " {promise}";
                    }
                    else
                    {
                        napi_value constructor = GetValueProperty(env, value, "constructor");
                        if (_runtime.GetValueType(
                            env, constructor, out napi_valuetype constructorType) ==
                            napi_status.napi_ok &&
                            constructorType == napi_valuetype.napi_function)
                        {
                            napi_value constructorName = GetValueProperty(env, constructor, "name");
                            if (constructorName != default)
                            {
                                valueString = $" {GetValueString(env, constructorName)}";
                            }
                        }
                    }
                    break;

                case napi_valuetype.napi_function:
                    napi_value functionName = GetValueProperty(env, value, "name");
                    if (functionName != default)
                    {
                        valueString = $" {GetValueString(env, functionName)}()";
                    }
                    break;
            };

            return $"{value.Handle:X16} {valueType.ToString().Substring(5)}{valueString}";
        }
        finally
        {
            _formatting = false;
        }
    }

    private string Format(napi_env env, napi_ref @ref)
    {
        napi_status status = _runtime.GetReferenceValue(env, @ref, out napi_value value);
        if (status == napi_status.napi_ok)
        {
            return $"@{@ref.Handle:X16} " + Format(env, value);
        }
        else
        {
            return $"@{@ref.Handle:X16} {status.ToString().Substring(5)}";
        }
    }

    private string Format(napi_env env, napi_property_descriptor property)
    {
        string name = GetValueString(env, property.name);
        string suffix = property.method != default ? "()" : string.Empty;
        return name + suffix;
    }

    private static string Format(nint value)
    {
        if (value == 0)
        {
            return "null";
        }

        object? target;
        try
        {
            target = GCHandle.FromIntPtr(value).Target;
        }
        catch (Exception)
        {
            target = null;
        }

        return $"0x{value:X16} {target?.GetType().Name ?? "null"}";
    }

    private static string Format(string? value)
    {
        if (value?.Length > 32)
        {

#if NETFRAMEWORK
            value = value.Substring(0, 32) + "...";
#else
            value = string.Concat(value.AsSpan(0, 32), "...");
#endif
        }

        return value == null ? "null" : $"\"{value}\"";
    }

    private static string Format(bool? value)
    {
        return value == null ? "null" : value.Value ? "true" : "false";
    }

    #endregion

    #region Tracing

    private void TraceCall(
        IEnumerable<string> args,
        [CallerMemberName] string name = "",
        string prefix = "<")
    {
        if (_formatting) return; // Prevent tracing while formatting.

        // The env arg is not traced; would it be helpful?
        Trace.TraceEvent(
            TraceEventType.Information,
            CallTrace,
            "{0} {1}({2})",
            prefix,
            name,
            string.Join(", ", args));
    }

    private void TraceReturn(
        napi_status status,
        IEnumerable<string>? results = null,
        [CallerMemberName] string name = "",
        string prefix = ">")
    {
        if (_formatting) return; // Prevent tracing while formatting.

        Trace.TraceEvent(
            status == napi_status.napi_ok ? TraceEventType.Information : TraceEventType.Warning,
            ReturnTrace,
            "{0} {1}({2})",
            prefix,
            name,
            status != napi_status.napi_ok || results == null ?
                status.ToString().Substring(5) : string.Join(", ", results));
    }

    private void TraceException(
        Exception ex,
        [CallerMemberName] string name = "",
        string prefix = ">")
    {
        if (_formatting) return; // Prevent tracing while formatting.

        Trace.TraceEvent(
            TraceEventType.Error,
            ExceptionTrace,
            "{0} {1}({2}: {3})",
            prefix,
            name,
            ex.GetType().Name,
            ex.Message);
    }

    private napi_status TraceCall(
        IEnumerable<string> args,
        Func<napi_status> call,
        [CallerMemberName] string name = "")
    {
        TraceCall(args, name);

        napi_status status;
        try
        {
            status = call();
        }
        catch (Exception ex)
        {
            TraceException(ex, name);
            throw;
        }

        TraceReturn(status, null, name);
        return status;
    }

    private napi_status TraceCall(
        IEnumerable<string> args,
        Func<(napi_status, string)> call,
        [CallerMemberName] string name = "")
    {
        return TraceCall(args, () =>
        {
            (napi_status status, string result) = call();
            return (status, [result]);
        }, name);
    }

    private napi_status TraceCall(
        IEnumerable<string> args,
        Func<(napi_status, string[])> call,
        [CallerMemberName] string name = "")
    {
        TraceCall(args, name);

        napi_status status;
        string[] results;
        try
        {
            (status, results) = call();
        }
        catch (Exception ex)
        {
            TraceException(ex, name);
            throw;
        }

        TraceReturn(status, results, name);
        return status;
    }

#if NETFRAMEWORK
    private static readonly napi_callback.Delegate s_traceFunctionCallback = TraceFunctionCallback;
    private static readonly napi_callback.Delegate s_traceMethodCallback = TraceMethodCallback;
    private static readonly napi_callback.Delegate s_traceGetterCallback = TraceGetterCallback;
    private static readonly napi_callback.Delegate s_traceSetterCallback = TraceSetterCallback;
#else
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_traceFunctionCallback = &TraceFunctionCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_traceMethodCallback = &TraceMethodCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_traceGetterCallback = &TraceGetterCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_traceSetterCallback = &TraceSetterCallback;
#endif

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value TraceFunctionCallback(napi_env env, napi_callback_info cbinfo)
    {
        using var scope = new JSValueScope(JSValueScopeType.Callback);
        return ((TracingJSRuntime)scope.Runtime).TraceCallback<JSCallbackDescriptor>(
            scope, cbinfo, (descriptor) => descriptor);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value TraceMethodCallback(napi_env env, napi_callback_info cbinfo)
    {
        using var scope = new JSValueScope(JSValueScopeType.Callback);
        return ((TracingJSRuntime)scope.Runtime).TraceCallback<JSPropertyDescriptor>(
            scope, cbinfo, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Method!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value TraceGetterCallback(napi_env env, napi_callback_info cbinfo)
    {
        using var scope = new JSValueScope(JSValueScopeType.Callback);
        return ((TracingJSRuntime)scope.Runtime).TraceCallback<JSPropertyDescriptor>(
            scope, cbinfo, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Getter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value TraceSetterCallback(napi_env env, napi_callback_info cbinfo)
    {
        using var scope = new JSValueScope(JSValueScopeType.Callback);
        return ((TracingJSRuntime)scope.Runtime).TraceCallback<JSPropertyDescriptor>(
            scope, cbinfo, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Setter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    /// <summary>
    /// Traces a callback function, method, getter, or setter, including args and return value.
    /// (When tracing is enabled, this method replaces the normal InvokeCallback method.)
    /// </summary>
    public napi_value TraceCallback<TDescriptor>(
        JSValueScope scope,
        napi_callback_info cbinfo,
        Func<TDescriptor, JSCallbackDescriptor> getCallbackDescriptor)
    {
        JSCallbackArgs.GetDataAndLength(scope, cbinfo, out object? dataObj, out int length);
        TDescriptor data = (TDescriptor)(dataObj ??
            throw new InvalidOperationException("Callback data is null."));
        JSCallbackDescriptor descriptor = getCallbackDescriptor(data);

        Span<napi_value> argsSpan = stackalloc napi_value[length];
        JSCallbackArgs args = new(scope, cbinfo, argsSpan, descriptor.Data);

        string[] argsStrings = new string[length];
        for (int i = 0; i < length; i++)
        {
            argsStrings[i] = Format((napi_env)scope, (napi_value)args[i]);
        }

        TraceCall(argsStrings, descriptor.Name ?? "callback", ">>");

        napi_value result;
        try
        {
            result = (napi_value)descriptor.Callback(args);
        }
        catch (Exception ex)
        {
            TraceException(ex, descriptor.Name ?? "callback", "<<");
            JSError.ThrowError(ex);
            return napi_value.Null;
        }

        TraceReturn(
            napi_status.napi_ok,
            [Format((napi_env)scope, result)],
            descriptor.Name ?? "callback",
            "<<");
        return result;
    }

    #endregion

    public override napi_status GetVersion(napi_env env, out uint result)
    {
        uint value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetVersion(env, out value), value.ToString()));
        result = value;
        return status;
    }

    public override napi_status RunScript(napi_env env, napi_value script, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(env, script)],
            () => (_runtime.RunScript(env, script, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status AddFinalizer(
        napi_env env,
        napi_value value,
        nint finalizeData,
        napi_finalize finalizeCallback,
        nint finalizeHint,
        out napi_ref result)
    {
        napi_ref valueRef = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.AddFinalizer(
                env, value, finalizeData, finalizeCallback, finalizeHint, out valueRef),
                Format(env, valueRef)));
        result = valueRef;
        return status;
    }

    public override napi_status AddFinalizer(
        napi_env env,
        napi_value value,
        nint finalizeData,
        napi_finalize finalizeCallback,
        nint finalizeHint)
    {
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.AddFinalizer(
                env, value, finalizeData, finalizeCallback, finalizeHint)));
        return status;
    }

    public override napi_status AdjustExternalMemory(
        napi_env env,
        long changeInBytes,
        out long result)
    {
        long value = default;
        napi_status status = TraceCall(
            [changeInBytes.ToString()],
            () => (_runtime.AdjustExternalMemory(env, changeInBytes, out value), value.ToString()));
        result = value;
        return status;
    }

    #region Instance data

    public override napi_status GetInstanceData(napi_env env, out nint result)
    {
        nint value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetInstanceData(env, out value), Format(value)));
        result = value;
        return status;
    }

    public override napi_status SetInstanceData(
        napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint)
    {
        napi_status status = TraceCall(
            [Format(data)],
            () => _runtime.SetInstanceData(env, data, finalize_cb, finalize_hint));
        return status;
    }

    #endregion

    #region Error handling

    public override napi_status CreateError(
        napi_env env, napi_value code, napi_value msg, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(env, code), Format(env, msg)],
            () => (_runtime.CreateError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateTypeError(
        napi_env env, napi_value code, napi_value msg, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(env, code), Format(env, msg)],
            () => (_runtime.CreateTypeError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateRangeError(
        napi_env env, napi_value code, napi_value msg, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(env, code), Format(env, msg)],
            () => (_runtime.CreateRangeError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateSyntaxError(
        napi_env env, napi_value code, napi_value msg, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(env, code), Format(env, msg)],
            () => (_runtime.CreateSyntaxError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status Throw(napi_env env, napi_value error)
    {
        return TraceCall(
            [Format(env, error)],
            () => _runtime.Throw(env, error));
    }
    public override napi_status ThrowError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            [Format(code), Format(msg)],
            () => _runtime.ThrowError(env, code, msg));
    }

    public override napi_status ThrowTypeError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            [Format(code), Format(msg)],
            () => _runtime.ThrowTypeError(env, code, msg));
    }

    public override napi_status ThrowRangeError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            [Format(code), Format(msg)],
            () => _runtime.ThrowRangeError(env, code, msg));
    }

    public override napi_status ThrowSyntaxError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            [Format(code), Format(msg)],
            () => _runtime.ThrowSyntaxError(env, code, msg));
    }

    public override napi_status IsExceptionPending(napi_env env, out bool result)
    {
        bool value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.IsExceptionPending(env, out value), Format(value)));
        result = value;
        return status;
    }

    public override unsafe napi_status GetLastErrorInfo(
        napi_env env, out napi_extended_error_info? result)
    {
        napi_extended_error_info? value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetLastErrorInfo(env, out value), Format(
                value == null || value.Value.error_message == null ?
                null : Marshal.PtrToStringAnsi((nint)value.Value.error_message))));
        result = value;
        return status;
    }

    public override napi_status GetAndClearLastException(napi_env env, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetAndClearLastException(env, out value), Format(env, value)));
        result = value;
        return status;
    }

    #endregion

    #region Value type checking

    public override napi_status GetValueType(
        napi_env env, napi_value value, out napi_valuetype result)
    {
        napi_valuetype valueType = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueType(env, value, out valueType),
                valueType.ToString().Substring(5)));
        result = valueType;
        return status;
    }

    public override napi_status IsDate(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsDate(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsPromise(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsPromise(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsError(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsError(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsArray(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsArray(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsArrayBuffer(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsArrayBuffer(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsDetachedArrayBuffer(
        napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsDetachedArrayBuffer(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsTypedArray(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsTypedArray(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsDataView(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsDataView(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    #endregion

    #region Value retrieval

    public override napi_status GetValueDouble(napi_env env, napi_value value, out double result)
    {
        double resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueDouble(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueInt32(napi_env env, napi_value value, out int result)
    {
        int resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueInt32(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueUInt32(napi_env env, napi_value value, out uint result)
    {
        uint resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueUInt32(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueInt64(napi_env env, napi_value value, out long result)
    {
        long resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueInt64(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueBigInt64(
        napi_env env, napi_value value, out long result, out bool lossless)
    {
        long resultValue = default;
        bool losslessValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueBigInt64(env, value, out resultValue, out losslessValue),
                [resultValue.ToString(), losslessValue.ToString()]));
        result = resultValue;
        lossless = losslessValue;
        return status;
    }

    public override napi_status GetValueBigInt64(
        napi_env env, napi_value value, out ulong result, out bool lossless)
    {
        ulong resultValue = default;
        bool losslessValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueBigInt64(env, value, out resultValue, out losslessValue),
                [resultValue.ToString(), losslessValue.ToString()]));
        result = resultValue;
        lossless = losslessValue;
        return status;
    }

    public override napi_status GetBigIntWordCount(
        napi_env env, napi_value value, out nuint result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(env, value)]);

        napi_status status;
        try
        {
            status = _runtime.GetBigIntWordCount(env, value, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [result.ToString()]);
        return status;
    }

    public override napi_status GetBigIntWords(
        napi_env env, napi_value value, out int sign, Span<ulong> words, out nuint result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(env, value)]);

        napi_status status;
        try
        {
            status = _runtime.GetBigIntWords(env, value, out sign, words, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [sign.ToString(), result.ToString()]);
        return status;
    }

    public override napi_status GetValueBool(napi_env env, napi_value value, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueBool(env, value, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueStringUtf8(
        napi_env env, napi_value value, Span<byte> buf, out int result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(env, value)]);

        napi_status status;
        try
        {
            status = _runtime.GetValueStringUtf8(env, value, buf, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(
            status,
            buf.Length == 0 ? [result.ToString()] : new[]
            {
                Format(Encoding.UTF8.GetString(buf.Slice(0, buf.Length - 1).ToArray())),
                result.ToString(),
            });
        return status;
    }

    public override napi_status GetValueStringUtf16(
        napi_env env, napi_value value, Span<char> buf, out int result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(env, value)]);

        napi_status status;
        try
        {
            status = _runtime.GetValueStringUtf16(env, value, buf, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(
            status,
            buf.Length == 0 ? [result.ToString()] : new[]
            {
                Format(new string(buf.Slice(0, buf.Length - 1).ToArray())),
                result.ToString(),
            });
        return status;
    }

    public override napi_status GetValueDate(napi_env env, napi_value value, out double result)
    {
        double resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueDate(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetSymbolFor(napi_env env, string name, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            [Format(name)],
            () => (_runtime.GetSymbolFor(env, name, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status GetArrayLength(napi_env env, napi_value value, out int result)
    {
        int resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetArrayLength(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetArrayBufferInfo(
        napi_env env, napi_value value, out nint data,
        out nuint length)
    {
        nint resultData = default;
        nuint resultLength = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetArrayBufferInfo(env, value, out resultData, out resultLength),
                [resultData.ToString(), resultLength.ToString()]));
        data = resultData;
        length = resultLength;
        return status;
    }

    public override napi_status GetTypedArrayInfo(
        napi_env env,
        napi_value value,
        out napi_typedarray_type type,
        out nuint byteLength,
        out nint data,
        out napi_value arraybuffer,
        out nuint offset)
    {
        napi_typedarray_type resultType = default;
        nuint resultByteLength = default;
        nint resultData = default;
        napi_value resultArrayBuffer = default;
        nuint resultOffset = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetTypedArrayInfo(
                env,
                value,
                out resultType,
                out resultByteLength,
                out resultData,
                out resultArrayBuffer,
                out resultOffset),
                [
                    resultType.ToString(),
                    resultByteLength.ToString(),
                    resultData.ToString(),
                    Format(env, resultArrayBuffer),
                    resultOffset.ToString(),
                ]));
        type = resultType;
        byteLength = resultByteLength;
        data = resultData;
        arraybuffer = resultArrayBuffer;
        offset = resultOffset;
        return status;
    }

    public override napi_status GetDataViewInfo(
        napi_env env,
        napi_value value,
        out nuint byteLength,
        out nint data,
        out napi_value arraybuffer,
        out nuint offset)
    {
        nuint resultByteLength = default;
        nint resultData = default;
        napi_value resultArrayBuffer = default;
        nuint resultOffset = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetDataViewInfo(
                env,
                value,
                out resultByteLength,
                out resultData,
                out resultArrayBuffer,
                out resultOffset),
                [
                    resultByteLength.ToString(),
                    resultData.ToString(),
                    Format(env, resultArrayBuffer),
                    resultOffset.ToString(),
                ]));
        byteLength = resultByteLength;
        data = resultData;
        arraybuffer = resultArrayBuffer;
        offset = resultOffset;
        return status;
    }

    public override napi_status GetValueArrayBuffer(
        napi_env env, napi_value arraybuffer, out nint data)
    {
        nint resultData = default;
        napi_status status = TraceCall(
            [Format(env, arraybuffer)],
            () => _runtime.GetValueArrayBuffer(env, arraybuffer, out resultData));
        data = resultData;
        return status;
    }

    public override napi_status GetValueTypedArray(
        napi_env env,
        napi_value typedarray,
        out napi_typedarray_type type,
        out nint data,
        out napi_value arraybuffer,
        out int byte_offset)
    {
        napi_typedarray_type resultType = default;
        nint resultData = default;
        napi_value resultValue = default;
        int resultOffset = default;
        napi_status status = TraceCall(
            [Format(env, typedarray)],
            () => (_runtime.GetValueTypedArray(
                env,
                typedarray,
                out resultType,
                out resultData,
                out resultValue,
                out resultOffset),
            new[] {
                resultType.ToString().Substring(5),
                Format(env, resultValue),
                resultOffset.ToString(),
            }));
        type = resultType;
        data = resultData;
        arraybuffer = resultValue;
        byte_offset = resultOffset;
        return status;
    }

    public override napi_status GetValueDataView(
        napi_env env,
        napi_value dataview,
        out nint data,
        out napi_value arraybuffer,
        out int byte_offset)
    {
        nint resultData = default;
        napi_value resultValue = default;
        int resultOffset = default;
        napi_status status = TraceCall(
            [Format(env, dataview)],
            () => (_runtime.GetValueDataView(
                env,
                dataview,
                out resultData,
                out resultValue,
                out resultOffset),
            new[] {
                Format(env, resultValue),
                resultOffset.ToString(),
            }));
        data = resultData;
        arraybuffer = resultValue;
        byte_offset = resultOffset;
        return status;
    }

    public override napi_status GetValueExternal(napi_env env, napi_value value, out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetValueExternal(env, value, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status StrictEquals(
        napi_env env, napi_value lhs, napi_value rhs, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, lhs), Format(env, rhs)],
            () => (_runtime.StrictEquals(env, lhs, rhs, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Value creation

    public override napi_status GetGlobal(napi_env env, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetGlobal(env, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetUndefined(napi_env env, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetUndefined(env, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetNull(napi_env env, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetNull(env, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetBoolean(napi_env env, bool value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(value)],
            () => (_runtime.GetBoolean(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, double value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, int value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, uint value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, long value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateBigInt(napi_env env, long value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateBigInt(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateBigInt(napi_env env, ulong value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [value.ToString()],
            () => (_runtime.CreateBigInt(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateBigInt(
        napi_env env,
        int sign,
        ReadOnlySpan<ulong> words,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([sign.ToString(), $"[{words.Length}]"]);

        napi_status status;
        try
        {
            status = _runtime.CreateBigInt(env, sign, words, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<byte> utf8Str, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(Encoding.UTF8.GetString(utf8Str.ToArray()))]);

        napi_status status;
        try
        {
            status = _runtime.CreateString(env, utf8Str, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<char> utf16Str, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(new string(utf16Str.ToArray()))]);

        napi_status status;
        try
        {
            status = _runtime.CreateString(env, utf16Str, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status CreateDate(napi_env env, double time, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [time.ToString()],
            () => (_runtime.CreateNumber(env, time, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateSymbol(
        napi_env env, napi_value description, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, description)],
            () => (_runtime.CreateSymbol(env, description, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateObject(napi_env env, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.CreateObject(env, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateArray(napi_env env, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.CreateArray(env, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateArray(napi_env env, int length, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [length.ToString()],
            () => (_runtime.CreateArray(env, length, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateArrayBuffer(
        napi_env env,
        int byte_length,
        out nint data,
        out napi_value result)
    {
        nint resultData = default;
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [byte_length.ToString()],
            () => (_runtime.CreateArrayBuffer(env, byte_length, out resultData, out resultValue),
                Format(env, resultValue)));
        data = resultData;
        result = resultValue;
        return status;
    }

    public override napi_status CreateArrayBuffer(
        napi_env env,
        nint external_data,
        int byte_length,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            ["external:" + byte_length],
            () => (_runtime.CreateArrayBuffer(
                env, external_data, byte_length, finalize_cb, finalize_hint, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status DetachArrayBuffer(napi_env env, napi_value arraybuffer)
    {
        return TraceCall(
            [Format(env, arraybuffer)],
            () => _runtime.DetachArrayBuffer(env, arraybuffer));
    }

    public override napi_status CreateTypedArray(
        napi_env env,
        napi_typedarray_type type,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[]
            {
                type.ToString().Substring(5),
                length.ToString(),
                Format(env, arraybuffer),
                byte_offset.ToString(),
            },
            () => (_runtime.CreateTypedArray(
                env, type, length, arraybuffer, byte_offset, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateDataView(
        napi_env env,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[]
            {
                length.ToString(),
                Format(env, arraybuffer),
                byte_offset.ToString(),
            },
            () => (_runtime.CreateDataView(
                env, length, arraybuffer, byte_offset, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateExternal(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(data)],
            () => (_runtime.CreateExternal(
                env, data, finalize_cb, finalize_hint, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override unsafe napi_status CreateFunction(
       napi_env env,
       string? name,
       napi_callback cb,
       nint data,
       out napi_value result)
    {
        if (cb == new napi_callback(JSValue.s_invokeJSCallback))
        {
            cb = new napi_callback(s_traceFunctionCallback);
        }
        else if (cb == new napi_callback(JSValue.s_invokeJSMethod))
        {
            cb = new napi_callback(s_traceMethodCallback);
        }
        else if (cb == new napi_callback(JSValue.s_invokeJSGetter))
        {
            cb = new napi_callback(s_traceGetterCallback);
        }
        else if (cb == new napi_callback(JSValue.s_invokeJSSetter))
        {
            cb = new napi_callback(s_traceSetterCallback);
        }

        // No-Context callbacks are not traced.

        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(name), Format(data)],
            () => (_runtime.CreateFunction(env, name, cb, data, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreatePromise(
        napi_env env, out napi_deferred deferred, out napi_value promise)
    {
        napi_deferred deferredValue = default;
        napi_value resultValue = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.CreatePromise(env, out deferredValue, out resultValue),
                Format(env, resultValue)));
        deferred = deferredValue;
        promise = resultValue;
        return status;
    }

    public override napi_status ResolveDeferred(
        napi_env env, napi_deferred deferred, napi_value resolution)
    {
        return TraceCall(
            [Format(env, resolution)],
            () => _runtime.ResolveDeferred(env, deferred, resolution));
    }

    public override napi_status RejectDeferred(
        napi_env env, napi_deferred deferred, napi_value rejection)
    {
        return TraceCall(
            [Format(env, rejection)],
            () => _runtime.RejectDeferred(env, deferred, rejection));
    }

    #endregion

    #region Value coercion

    public override napi_status CoerceToBool(
        napi_env env, napi_value value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.CoerceToBool(env, value, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CoerceToNumber(
        napi_env env, napi_value value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.CoerceToNumber(env, value, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CoerceToObject(
        napi_env env, napi_value value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.CoerceToObject(env, value, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CoerceToString(
        napi_env env, napi_value value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.CoerceToString(env, value, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Handle scopes

    public override napi_status OpenHandleScope(napi_env env, out napi_handle_scope result)
    {
        napi_handle_scope resultScope = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.OpenHandleScope(env, out resultScope), Format(resultScope)));
        result = resultScope;
        return status;
    }

    public override napi_status CloseHandleScope(napi_env env, napi_handle_scope scope)
    {
        return TraceCall(
            [Format(scope)],
            () => _runtime.CloseHandleScope(env, scope));
    }

    public override napi_status OpenEscapableHandleScope(
        napi_env env, out napi_escapable_handle_scope result)
    {
        napi_escapable_handle_scope resultScope = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.OpenEscapableHandleScope(env, out resultScope), Format(resultScope)));
        result = resultScope;
        return status;
    }

    public override napi_status CloseEscapableHandleScope(
        napi_env env, napi_escapable_handle_scope scope)
    {
        return TraceCall(
            [Format(scope)],
            () => _runtime.CloseEscapableHandleScope(env, scope));
    }

    public override napi_status EscapeHandle(
        napi_env env,
        napi_escapable_handle_scope scope,
        napi_value escapee,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(scope), Format(env, escapee)],
            () => (_runtime.EscapeHandle(env, scope, escapee, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region References

    public override napi_status CreateReference(
        napi_env env, napi_value value, uint initialRefcount, out napi_ref result)
    {
        napi_ref resultRef = default;
        napi_status status = TraceCall(
            [Format(env, value), initialRefcount.ToString()],
            () => (_runtime.CreateReference(env, value, initialRefcount, out resultRef),
                Format(env, resultRef)));
        result = resultRef;
        return status;
    }

    public override napi_status DeleteReference(napi_env env, napi_ref @ref)
    {
        return TraceCall(
            [Format(env, @ref)],
            () => _runtime.DeleteReference(env, @ref));
    }

    public override napi_status RefReference(napi_env env, napi_ref @ref, out uint result)
    {
        uint resultCount = default;
        napi_status status = TraceCall(
            [Format(env, @ref)],
            () => (_runtime.RefReference(env, @ref, out resultCount), resultCount.ToString()));
        result = resultCount;
        return status;
    }

    public override napi_status UnrefReference(napi_env env, napi_ref @ref, out uint result)
    {
        uint resultCount = default;
        napi_status status = TraceCall(
            [Format(env, @ref)],
            () => (_runtime.UnrefReference(env, @ref, out resultCount), resultCount.ToString()));
        result = resultCount;
        return status;
    }

    public override napi_status GetReferenceValue(
        napi_env env, napi_ref @ref, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, @ref) },
            () => (_runtime.GetReferenceValue(env, @ref, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Function calls

    public override napi_status CallFunction(
        napi_env env,
        napi_value recv,
        napi_value func,
        ReadOnlySpan<napi_value> args,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(env, recv) }.Concat(
            args.ToArray().Select((a) => Format(env, a))));

        napi_status status;
        try
        {
            status = _runtime.CallFunction(env, recv, func, args, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status GetCallbackInfo(
        napi_env env, napi_callback_info cbinfo, out int argc, out nint data)
    {
        int resultCount = default;
        nint resultData = default;
        napi_status status = TraceCall(
            [Format(cbinfo)],
            () => (_runtime.GetCallbackInfo(env, cbinfo, out resultCount, out resultData),
                [resultCount.ToString(), Format(resultData)]));
        argc = resultCount;
        data = resultData;
        return status;
    }

    public override napi_status GetCallbackArgs(
        napi_env env, napi_callback_info cbinfo, Span<napi_value> args, out napi_value this_arg)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([Format(cbinfo), $"[{args.Length}]"]);

        napi_status status;
        try
        {
            status = _runtime.GetCallbackArgs(env, cbinfo, args, out this_arg);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, new[] { Format(env, this_arg) }.Concat(
            args.ToArray().Select((a) => Format(env, a))));
        return status;
    }

    public override napi_status GetNewTarget(
        napi_env env, napi_callback_info cbinfo, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(cbinfo)],
            () => (_runtime.GetNewTarget(env, cbinfo, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Object properties

    public override napi_status HasProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(env, key)],
            () => (_runtime.HasProperty(env, js_object, key, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status HasOwnProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(env, key)],
            () => (_runtime.HasOwnProperty(env, js_object, key, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetProperty(
        napi_env env, napi_value js_object, napi_value key, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(env, key)],
            () => (_runtime.GetProperty(env, js_object, key, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status SetProperty(
        napi_env env, napi_value js_object, napi_value key, napi_value value)
    {
        return TraceCall(
            [Format(env, js_object), Format(env, key), Format(env, value)],
            () => _runtime.SetProperty(env, js_object, key, value));
    }

    public override napi_status DeleteProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(env, key)],
            () => (_runtime.DeleteProperty(env, js_object, key, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status HasNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out bool result)
    {
        TraceCall(
            [Format(env, js_object), Format(Encoding.UTF8.GetString(utf8name.ToArray()))]);

        napi_status status;
        try
        {
            status = _runtime.HasNamedProperty(env, js_object, utf8name, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(result)]);
        return status;
    }

    public override napi_status GetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out napi_value result)
    {
        TraceCall(
            [Format(env, js_object), Format(Encoding.UTF8.GetString(utf8name.ToArray()))]);

        napi_status status;
        try
        {
            status = _runtime.GetNamedProperty(env, js_object, utf8name, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status SetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, napi_value value)
    {
        TraceCall(
            new[] {
                Format(env, js_object),
                Format(Encoding.UTF8.GetString(utf8name.ToArray())),
                Format(env, value),
            });

        napi_status status;
        try
        {
            status = _runtime.SetNamedProperty(env, js_object, utf8name, value);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status);
        return status;
    }

    public override napi_status HasElement(
        napi_env env, napi_value js_object, uint index, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), index.ToString()],
            () => (_runtime.HasElement(env, js_object, index, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetElement(
        napi_env env, napi_value js_object, uint index, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), index.ToString()],
            () => (_runtime.GetElement(env, js_object, index, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status SetElement(
        napi_env env, napi_value js_object, uint index, napi_value value)
    {
        return TraceCall(
            [Format(env, js_object), index.ToString(), Format(env, value)],
            () => _runtime.SetElement(env, js_object, index, value));
    }

    public override napi_status DeleteElement(
        napi_env env, napi_value js_object, uint index, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), index.ToString()],
            () => (_runtime.DeleteElement(env, js_object, index, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Property and class definition

    public override napi_status GetPropertyNames(
        napi_env env, napi_value js_object, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object)],
            () => (_runtime.GetPropertyNames(env, js_object, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetAllPropertyNames(
        napi_env env,
        napi_value js_object,
        napi_key_collection_mode key_mode,
        napi_key_filter key_filter,
        napi_key_conversion key_conversion,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[]
            {
                Format(env, js_object),
                key_mode.ToString().Substring(5),
                key_filter.ToString().Substring(5),
                key_conversion.ToString().Substring(5),
            },
            () => (_runtime.GetAllPropertyNames(
                env, js_object, key_mode, key_filter, key_conversion, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status Freeze(napi_env env, napi_value value)
    {
        return TraceCall(
            [Format(env, value)],
            () => _runtime.Freeze(env, value));
    }

    public override napi_status Seal(napi_env env, napi_value value)
    {
        return TraceCall(
            [Format(env, value)],
            () => _runtime.Seal(env, value));
    }

    public override napi_status DefineProperties(
        napi_env env, napi_value js_object, ReadOnlySpan<napi_property_descriptor> properties)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[]
        {
            Format(env, js_object),
            $"[{string.Join(", ", properties.ToArray().Select((p) => Format(env, p)))}]",
        });

        napi_status status;
        try
        {
            status = _runtime.DefineProperties(env, js_object, properties);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status);
        return status;
    }

    public override napi_status DefineClass(
        napi_env env,
        string name,
        napi_callback constructor,
        nint data,
        ReadOnlySpan<napi_property_descriptor> properties,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[]
        {
            Format(name),
            Format(data),
            $"[{string.Join(", ", properties.ToArray().Select((p) => Format(env, p)))}]",
        });

        napi_status status;
        try
        {
            status = _runtime.DefineClass(
                env, name, constructor, data, properties, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status GetPrototype(
        napi_env env,
        napi_value js_object,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object)],
            () => (_runtime.GetPrototype(env, js_object, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status NewInstance(
        napi_env env,
        napi_value constructor,
        ReadOnlySpan<napi_value> args,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(env, constructor) }.Concat(
            args.ToArray().Select((a) => Format(env, a))));

        napi_status status;
        try
        {
            status = _runtime.NewInstance(env, constructor, args, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status InstanceOf(
        napi_env env,
        napi_value js_object,
        napi_value constructor,
        out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(env, constructor)],
            () => (_runtime.InstanceOf(env, js_object, constructor, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status Wrap(
        napi_env env,
        napi_value js_object,
        nint native_object,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_ref result)
    {
        napi_ref resultRef = default;
        napi_status status = TraceCall(
            [Format(env, js_object), Format(native_object)],
            () => (_runtime.Wrap(
                env, js_object, native_object, finalize_cb, finalize_hint, out resultRef),
                Format(env, resultRef)));
        result = resultRef;
        return status;
    }

    public override napi_status Wrap(
        napi_env env,
        napi_value js_object,
        nint native_object,
        napi_finalize finalize_cb,
        nint finalize_hint)
    {
        napi_status status = TraceCall(
            [Format(env, js_object), Format(native_object)],
            () => _runtime.Wrap(env, js_object, native_object, finalize_cb, finalize_hint));
        return status;
    }

    public override napi_status Unwrap(napi_env env, napi_value js_object, out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object)],
            () => (_runtime.Unwrap(env, js_object, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status RemoveWrap(napi_env env, napi_value js_object, out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            [Format(env, js_object)],
            () => (_runtime.RemoveWrap(env, js_object, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status SetObjectTypeTag(
        napi_env env, napi_value value, Guid typeTag)
    {
        napi_status status = TraceCall(
            [Format(env, value), typeTag.ToString()],
            () => _runtime.SetObjectTypeTag(env, value, typeTag));
        return status;
    }

    public override napi_status CheckObjectTypeTag(
        napi_env env, napi_value value, Guid typeTag, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value), typeTag.ToString()],
            () => (_runtime.CheckObjectTypeTag(env, value, typeTag, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Thread-safe functions

    public override napi_status CreateThreadSafeFunction(
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
        out napi_threadsafe_function result)
    {
        napi_threadsafe_function resultValue = default;
        napi_status status = TraceCall(
            [Format(env, func)],
            () => (_runtime.CreateThreadSafeFunction(
                env,
                func,
                asyncResource,
                asyncResourceName,
                maxQueueSize,
                initialThreadCount,
                threadFinalizeData,
                threadFinalizeCallback,
                context,
                callJSCallback,
                out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CallThreadSafeFunction(
        napi_threadsafe_function func,
        nint data,
        napi_threadsafe_function_call_mode isBlocking)
    {
        return TraceCall(
            [Format(func), Format(data), isBlocking.ToString().Substring(10)],
            () => _runtime.CallThreadSafeFunction(func, data, isBlocking));
    }

    public override napi_status GetThreadSafeFunctionContext(
        napi_threadsafe_function func,
        out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            [Format(func)],
            () => (_runtime.GetThreadSafeFunctionContext(func, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status AcquireThreadSafeFunction(napi_threadsafe_function func)
    {
        return TraceCall(
            [Format(func)],
            () => _runtime.AcquireThreadSafeFunction(func));
    }

    public override napi_status ReleaseThreadSafeFunction(
        napi_threadsafe_function func,
        napi_threadsafe_function_release_mode mode)
    {
        return TraceCall(
            [Format(func), mode.ToString().Substring(10)],
            () => _runtime.ReleaseThreadSafeFunction(func, mode));
    }

    public override napi_status RefThreadSafeFunction(napi_env env, napi_threadsafe_function func)
    {
        return TraceCall(
            [Format(func)],
            () => _runtime.RefThreadSafeFunction(env, func));
    }

    public override napi_status UnrefThreadSafeFunction(napi_env env, napi_threadsafe_function func)
    {
        return TraceCall(
            [Format(func)],
            () => _runtime.UnrefThreadSafeFunction(env, func));
    }

    #endregion

    #region Async work

    public override napi_status AsyncInit(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        out napi_async_context result)
    {
        napi_async_context resultValue = default;
        napi_status status = TraceCall(
            [Format(env, asyncResource)],
            () => (_runtime.AsyncInit(
                env,
                asyncResource,
                asyncResourceName,
                out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status AsyncDestroy(napi_env env, napi_async_context asyncContext)
    {
        return TraceCall(
            [Format(asyncContext)],
            () => _runtime.AsyncDestroy(env, asyncContext));
    }

    public override napi_status CreateAsyncWork(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        napi_async_execute_callback execute,
        napi_async_complete_callback complete,
        nint data, out napi_async_work result)
    {
        napi_async_work resultValue = default;
        napi_status status = TraceCall(
            [Format(env, asyncResource)],
            () => (_runtime.CreateAsyncWork(
                env,
                asyncResource,
                asyncResourceName,
                execute,
                complete,
                data,
                out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status QueueAsyncWork(napi_env env, napi_async_work work)
    {
        return TraceCall(
            [Format(work)],
            () => _runtime.QueueAsyncWork(env, work));
    }

    public override napi_status DeleteAsyncWork(napi_env env, napi_async_work work)
    {
        return TraceCall(
            [Format(work)],
            () => _runtime.DeleteAsyncWork(env, work));
    }

    public override napi_status CancelAsyncWork(napi_env env, napi_async_work work)
    {
        return TraceCall(
            [Format(work)],
            () => _runtime.CancelAsyncWork(env, work));
    }

    public override napi_status MakeCallback(
        napi_env env,
        napi_async_context asyncContext,
        napi_value recv,
        napi_value func,
        Span<napi_value> args,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(asyncContext), Format(env, recv), Format(env, func) }.Concat(
            args.ToArray().Select((a) => Format(env, a))));

        napi_status status;
        try
        {
            status = _runtime.MakeCallback(env, asyncContext, recv, func, args, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status OpenCallbackScope(
        napi_env env,
        napi_value resourceObject,
        napi_async_context asyncContext,
        out napi_callback_scope result)
    {
        napi_callback_scope resultValue = default;
        napi_status status = TraceCall(
            [Format(env, resourceObject), Format(asyncContext)],
            () => (_runtime.OpenCallbackScope(
                env,
                resourceObject,
                asyncContext,
                out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CloseCallbackScope(napi_env env, napi_callback_scope scope)
    {
        return TraceCall(
            [Format(scope)],
            () => _runtime.CloseCallbackScope(env, scope));
    }

    #endregion

    #region Cleanup hooks

    public override napi_status AddAsyncCleanupHook(
        napi_env env,
        napi_async_cleanup_hook hook,
        nint arg,
        out napi_async_cleanup_hook_handle result)
    {
        napi_async_cleanup_hook_handle resultValue = default;
        napi_status status = TraceCall(
            [Format(env)],
            () => (_runtime.AddAsyncCleanupHook(
                env,
                hook,
                arg,
                out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status RemoveAsyncCleanupHook(
        napi_async_cleanup_hook_handle removeHandle)
    {
        return TraceCall(
            [Format(removeHandle)],
            () => _runtime.RemoveAsyncCleanupHook(removeHandle));
    }

    public override napi_status AddEnvCleanupHook(
        napi_env env,
        napi_cleanup_hook callback,
        nint userData)
    {
        return TraceCall(
            [Format(env)],
            () => _runtime.AddEnvCleanupHook(env, callback, userData));
    }

    public override napi_status RemoveEnvCleanupHook(
        napi_env env,
        napi_cleanup_hook callback,
        nint userData)
    {
        return TraceCall(
            [Format(env)],
            () => _runtime.RemoveEnvCleanupHook(env, callback, userData));
    }

    #endregion

    #region Buffers

    public override napi_status IsBuffer(napi_env env, napi_value value, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.IsBuffer(env, value, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateBuffer(napi_env env, Span<byte> data, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([$"[{data.Length}]"]);

        napi_status status;
        try
        {
            status = _runtime.CreateBuffer(env, data, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status CreateBufferCopy(
        napi_env env, ReadOnlySpan<byte> data, out nint resultData, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([$"[{data.Length}]"]);

        napi_status status;
        try
        {
            status = _runtime.CreateBufferCopy(env, data, out resultData, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(resultData), Format(env, result)]);
        return status;
    }

    public override napi_status CreateExternalBuffer(
        napi_env env,
        Span<byte> data,
        napi_finalize finalizeCallback,
        nint finalizeHint,
        out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall([$"[{data.Length}]"]);

        napi_status status;
        try
        {
            status = _runtime.CreateExternalBuffer(
                env, data, finalizeCallback, finalizeHint, out result);
        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(status, [Format(env, result)]);
        return status;
    }

    public override napi_status GetBufferInfo(
        napi_env env, napi_value value, out nint data, out nuint length)
    {
        nint dataValue = default;
        nuint lengthValue = default;
        napi_status status = TraceCall(
            [Format(env, value)],
            () => (_runtime.GetBufferInfo(env, value, out dataValue, out lengthValue),
                [dataValue.ToString(), lengthValue.ToString()]));
        data = dataValue;
        length = lengthValue;
        return status;
    }

    #endregion

    #region Misc Node.js functions

    [DoesNotReturn]
    public override void FatalError(string location, string message)
    {
        TraceCall([location, message]);
        _runtime.FatalError(location, message);
    }

    public override napi_status FatalException(napi_env env, napi_value err)
    {
        return TraceCall(
            [Format(env, err)],
            () => _runtime.FatalException(env, err));
    }

    public override napi_status GetUVEventLoop(napi_env env, out uv_loop_t result)
    {
        uv_loop_t resultValue = default;
        napi_status status = TraceCall(
            [Format(env)],
            () => (_runtime.GetUVEventLoop(env, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override void RegisterModule(ref napi_module module)
    {
        TraceCall(Array.Empty<string>());

        try
        {
            _runtime.RegisterModule(ref module);

        }
        catch (Exception ex)
        {
            TraceException(ex);
            throw;
        }

        TraceReturn(napi_status.napi_ok);
    }

    public override napi_status GetModuleFileName(napi_env env, out string result)
    {
        string resultValue = default!;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetModuleFileName(env, out resultValue), resultValue));
        result = resultValue;
        return status;
    }

    public override napi_status GetNodeVersion(napi_env env, out napi_node_version result)
    {
        napi_node_version resultValue = default;
        napi_status status = TraceCall(
            [Format(env)],
            () => _runtime.GetNodeVersion(env, out resultValue));
        result = resultValue;
        return status;
    }

    #endregion

    #region Embedding

    public override napi_status CreatePlatform(
        string[]? args, string[]? execArgs, Action<string>? errorHandler, out napi_platform result)
    {
        napi_platform resultValue = default;
        napi_status status = TraceCall(
            [
                $"[{string.Join(", ", args ?? [])}]",
                $"[{string.Join(", ", execArgs ?? [])}]",
            ],
            () => (_runtime.CreatePlatform(args, execArgs, errorHandler, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status DestroyPlatform(napi_platform platform)
    {
        return TraceCall(
            [Format(platform)],
            () => _runtime.DestroyPlatform(platform));
    }

    public override napi_status CreateEnvironment(
        napi_platform platform,
        Action<string>? errorHandler,
        string? mainScript,
        out napi_env result)
    {
        napi_env resultValue = default;
        napi_status status = TraceCall(
            [Format(platform), Format(mainScript)],
            () => (_runtime.CreateEnvironment(platform, errorHandler, mainScript, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status DestroyEnvironment(napi_env env, out int exitCode)
    {
        int exitCodeValue = default;
        napi_status status = TraceCall(
            [Format(env)],
            () => (_runtime.DestroyEnvironment(env, out exitCodeValue),
                exitCodeValue.ToString()));
        exitCode = exitCodeValue;
        return status;
    }

    public override napi_status RunEnvironment(napi_env env)
    {
        return TraceCall([Format(env)], () => _runtime.RunEnvironment(env));
    }

    public override napi_status AwaitPromise(
        napi_env env, napi_value promise, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            [Format(env, promise)],
            () => (_runtime.AwaitPromise(env, promise, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion
}
