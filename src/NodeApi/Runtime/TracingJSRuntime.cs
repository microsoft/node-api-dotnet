// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Runtime;

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
    private readonly TraceSource _trace;

    public TracingJSRuntime(JSRuntime runtime, TraceSource trace)
    {
        _runtime = runtime;
        _trace = trace;
    }

    private static string Format(napi_env env)
    {
        return env.Handle.ToString("X16");
    }

    private static string Format(napi_handle_scope scope)
    {
        return scope.Handle.ToString("X16");
    }

    private static string Format(napi_escapable_handle_scope scope)
    {
        return scope.Handle.ToString("X16");
    }

    private string GetValueString(napi_env env, napi_value value)
    {
        if (_runtime.GetValueStringUtf16(env, value, Span<char>.Empty, out int length) ==
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

    private string Format(napi_env env, napi_value value)
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

                    if (_runtime.CreateString(env, "length".AsSpan(), out napi_value lengthName) ==
                        napi_status.napi_ok &&
                        _runtime.GetProperty(env, value, lengthName, out napi_value size) ==
                        napi_status.napi_ok)
                    {
                        valueString = $" [{size}]";
                    }
                }
                else if (_runtime.IsPromise(env, value, out bool isPromise) ==
                    napi_status.napi_ok && isPromise)
                {
                    valueString = " {promise}";
                }
                break;

            case napi_valuetype.napi_function:
                if (_runtime.CreateString(env, "name".AsSpan(), out napi_value nameName) ==
                    napi_status.napi_ok &&
                    _runtime.GetProperty(env, value, nameName, out napi_value name) ==
                    napi_status.napi_ok)
                {
                    valueString = $" {GetValueString(env, name)}()";
                }
                break;
        };

        return $"{value.Handle:X16} {valueType.ToString().Substring(5)}{valueString}";
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
        return value == null ? "null" : $"\"{value}\"";
    }

    private static string Format(bool? value)
    {
        return value == null ? "null" : value.Value ? "true" : "false";
    }

    private void TraceCall(
        IEnumerable<string> args,
        [CallerMemberName] string name = "")
    {
        // The env arg is not traced; would it be helpful?
        _trace.TraceEvent(
            TraceEventType.Information,
            CallTrace,
            "< {0}({1})",
            name,
            string.Join(", ", args));
    }

    private void TraceReturn(
        napi_status status,
        IEnumerable<string>? results = null,
        [CallerMemberName] string name = "")
    {
        _trace.TraceEvent(
            status == napi_status.napi_ok ? TraceEventType.Information : TraceEventType.Warning,
            ReturnTrace,
            "> {0}({1})",
            name,
            status != napi_status.napi_ok || results == null ?
                status.ToString().Substring(5) : string.Join(", ", results));
    }

    private void TraceException(
        Exception ex,
        [CallerMemberName] string name = "")
    {
        _trace.TraceEvent(
            TraceEventType.Error,
            ExceptionTrace,
            $"> {0}({1}: {2})",
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
            return (status, new[] { result });
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

    public static void TraceCallback()
    {
        // TODO: Trace callbacks from JS to .NET
    }

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
            new[] { Format(env, script) },
            () => (_runtime.RunScript(env, script, out value), Format(env, value)));
        result = value;
        return status;
    }

    #region Instance data

    public override napi_status GetInstanceData(
        napi_env env,
        out nint result)
    {
        nint value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetInstanceData(env, out value), Format(value)));
        result = value;
        return status;
    }

    public override napi_status SetInstanceData(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint)
    {
        napi_status status = TraceCall(
            new[] { Format(data) },
            () => _runtime.SetInstanceData(env, data, finalize_cb, finalize_hint));
        return status;
    }

    #endregion

    #region Error handling

    public override napi_status CreateError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            new[] { Format(env, code), Format(env, msg) },
            () => (_runtime.CreateError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateTypeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            new[] { Format(env, code), Format(env, msg) },
            () => (_runtime.CreateTypeError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateRangeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            new[] { Format(env, code), Format(env, msg) },
            () => (_runtime.CreateRangeError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status CreateSyntaxError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            new[] { Format(env, code), Format(env, msg) },
            () => (_runtime.CreateSyntaxError(env, code, msg, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status Throw(napi_env env, napi_value error)
    {
        return TraceCall(
            new[] { Format(env, error) },
            () => _runtime.Throw(env, error));
    }
    public override napi_status ThrowError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            new[] { Format(code), Format(msg) },
            () => _runtime.ThrowError(env, code, msg));
    }

    public override napi_status ThrowTypeError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            new[] { Format(code), Format(msg) },
            () => _runtime.ThrowTypeError(env, code, msg));
    }

    public override napi_status ThrowRangeError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            new[] { Format(code), Format(msg) },
            () => _runtime.ThrowRangeError(env, code, msg));
    }

    public override napi_status ThrowSyntaxError(napi_env env, string? code, string msg)
    {
        return TraceCall(
            new[] { Format(code), Format(msg) },
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
        napi_env env, out napi_extended_error_info result)
    {
        napi_extended_error_info value = default;
        napi_status status = TraceCall(
            Array.Empty<string>(),
            () => (_runtime.GetLastErrorInfo(env, out value), Format(value.error_message == null ?
                null : Marshal.PtrToStringAnsi((nint)value.error_message))));
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
            new[] { Format(env, value) },
            () => (_runtime.GetValueType(env, value, out valueType),
                valueType.ToString().Substring(5)));
        result = valueType;
        return status;
    }

    public override napi_status IsDate(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsDate(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsPromise(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsPromise(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsError(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsError(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsArray(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsArray(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsArrayBuffer(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsArrayBuffer(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsDetachedArrayBuffer(
        napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsDetachedArrayBuffer(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsTypedArray(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.IsTypedArray(env, value, out isValue), Format(isValue)));
        result = isValue;
        return status;
    }

    public override napi_status IsDataView(napi_env env, napi_value value, out bool result)
    {
        bool isValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
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
            new[] { Format(env, value) },
            () => (_runtime.GetValueDouble(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueInt32(napi_env env, napi_value value, out int result)
    {
        int resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetValueInt32(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueUInt32(napi_env env, napi_value value, out uint result)
    {
        uint resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetValueUInt32(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueInt64(napi_env env, napi_value value, out long result)
    {
        long resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetValueInt64(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueBool(napi_env env, napi_value value, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetValueBool(env, value, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueStringUtf8(
        napi_env env, napi_value value, Span<byte> buf, out int result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(env, value) });

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
            buf.Length == 0 ? new[] { result.ToString() } : new[]
            {
                Format(Encoding.UTF8.GetString(buf.ToArray())),
                result.ToString(),
            });
        return status;
    }

    public override napi_status GetValueStringUtf16(
        napi_env env, napi_value value, Span<char> buf, out int result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(env, value) });

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
            buf.Length == 0 ? new[] { result.ToString() } : new[]
            {
                Format(new string(buf.ToArray())),
                result.ToString(),
            });
        return status;
    }

    public override napi_status GetValueDate(napi_env env, napi_value value, out double result)
    {
        double resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetValueDate(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetSymbolFor(napi_env env, string name, out napi_value result)
    {
        napi_value value = default;
        napi_status status = TraceCall(
            new[] { Format(name) },
            () => (_runtime.GetSymbolFor(env, name, out value), Format(env, value)));
        result = value;
        return status;
    }

    public override napi_status GetArrayLength(napi_env env, napi_value value, out int result)
    {
        int resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, value) },
            () => (_runtime.GetArrayLength(env, value, out resultValue), resultValue.ToString()));
        result = resultValue;
        return status;
    }

    public override napi_status GetValueArrayBuffer(
        napi_env env, napi_value arraybuffer, out nint data)
    {
        nint resultData = default;
        napi_status status = TraceCall(
            new[] { Format(env, arraybuffer) },
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
            new[] { Format(env, typedarray) },
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
            new[] { Format(env, dataview) },
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
            new[] { Format(env, value) },
            () => (_runtime.GetValueExternal(env, value, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status StrictEquals(
        napi_env env, napi_value lhs, napi_value rhs, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, lhs), Format(env, rhs) },
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
            new[] { Format(value) },
            () => (_runtime.GetBoolean(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, double value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { value.ToString() },
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, int value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { value.ToString() },
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, uint value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { value.ToString() },
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateNumber(napi_env env, long value, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { value.ToString() },
            () => (_runtime.CreateNumber(env, value, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<byte> utf8Str, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(Encoding.UTF8.GetString(utf8Str.ToArray())) });

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

        TraceReturn(status, new[] { Format(env, result) });
        return status;
    }

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<char> utf16Str, out napi_value result)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { Format(new string(utf16Str.ToArray())) });

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

        TraceReturn(status, new[] { Format(env, result) });
        return status;
    }

    public override napi_status CreateDate(napi_env env, double time, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { time.ToString() },
            () => (_runtime.CreateNumber(env, time, out resultValue), Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateSymbol(
        napi_env env, napi_value description, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, description) },
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
            new[] { length.ToString() },
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
            new[] { byte_length.ToString() },
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
            new[] { "external:" + byte_length },
            () => (_runtime.CreateArrayBuffer(
                env, external_data, byte_length, finalize_cb, finalize_hint, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status DetachArrayBuffer(napi_env env, napi_value arraybuffer)
    {
        return TraceCall(
            new[] { Format(env, arraybuffer) },
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
            new[] { Format(data) },
            () => (_runtime.CreateExternal(
                env, data, finalize_cb, finalize_hint, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status CreateFunction(
       napi_env env,
       string? name,
       napi_callback cb,
       nint data,
       out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(name), Format(data) },
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
            new[] { Format(env, resolution) },
            () => _runtime.ResolveDeferred(env, deferred, resolution));
    }

    public override napi_status RejectDeferred(
        napi_env env, napi_deferred deferred, napi_value rejection)
    {
        return TraceCall(
            new[] { Format(env, rejection) },
            () => _runtime.RejectDeferred(env, deferred, rejection));
    }

    #endregion

    #region Value coercion

    // TODO

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
            new[] { Format(scope) },
            () => _runtime.CloseHandleScope(env, scope));
    }

    public override napi_status OpenEscapableHandleScope(
        napi_env env,
        out napi_escapable_handle_scope result)
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
            new[] { Format(scope) },
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
            new[] { Format(scope), Format(env, escapee) },
            () => (_runtime.EscapeHandle(env, scope, escapee, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region References

    public override napi_status CreateReference(
        napi_env env,
        napi_value value,
        uint initial_refcount,
        out napi_ref result)
    {
        napi_ref resultRef = default;
        napi_status status = TraceCall(
            new[] { Format(env, value), initial_refcount.ToString() },
            () => (_runtime.CreateReference(env, value, initial_refcount, out resultRef),
                Format(env, resultRef)));
        result = resultRef;
        return status;
    }

    public override napi_status DeleteReference(napi_env env, napi_ref @ref)
    {
        return TraceCall(
            new[] { Format(env, @ref) },
            () => _runtime.DeleteReference(env, @ref));
    }

    public override napi_status RefReference(napi_env env, napi_ref @ref, out uint result)
    {
        uint resultCount = default;
        napi_status status = TraceCall(
            new[] { Format(env, @ref) },
            () => (_runtime.RefReference(env, @ref, out resultCount), resultCount.ToString()));
        result = resultCount;
        return status;
    }

    public override napi_status UnrefReference(napi_env env, napi_ref @ref, out uint result)
    {
        uint resultCount = default;
        napi_status status = TraceCall(
            new[] { Format(env, @ref) },
            () => (_runtime.UnrefReference(env, @ref, out resultCount), resultCount.ToString()));
        result = resultCount;
        return status;
    }

    public override napi_status GetReferenceValue(
        napi_env env, napi_ref @ref, out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { $"@{@ref:X16}" },
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

        TraceReturn(status, new[] { Format(env, result) });
        return status;
    }

    public override napi_status GetCallbackInfo(
        napi_env env,
        napi_callback_info cbinfo,
        out int argc,
        out nint data)
    {
        int resultCount = default;
        nint resultData = default;
        napi_status status = TraceCall(
            new[] { $"{cbinfo.Handle:X16}" },
            () => (_runtime.GetCallbackInfo(env, cbinfo, out resultCount, out resultData),
                new[] { resultCount.ToString(), Format(resultData) }));
        argc = resultCount;
        data = resultData;
        return status;
    }

    public override napi_status GetCallbackArgs(
        napi_env env,
        napi_callback_info cbinfo,
        Span<napi_value> args,
        out napi_value this_arg)
    {
        // The combined TraceCall() can't be used with Span<T>.
        TraceCall(new[] { $"{cbinfo.Handle:X16}", $"[{args.Length}]" });

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

    #endregion

    #region Object properties

    public override napi_status HasProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object), Format(env, key) },
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
            new[] { Format(env, js_object), Format(env, key) },
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
            new[] { Format(env, js_object), Format(env, key) },
            () => (_runtime.GetProperty(env, js_object, key, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status SetProperty(
        napi_env env, napi_value js_object, napi_value key, napi_value value)
    {
        return TraceCall(
            new[] { Format(env, js_object), Format(env, key), Format(env, value) },
            () => _runtime.SetProperty(env, js_object, key, value));
    }

    public override napi_status DeleteProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object), Format(env, key) },
            () => (_runtime.DeleteProperty(env, js_object, key, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status HasNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out bool result)
    {
        TraceCall(
            new[] { Format(env, js_object), Format(Encoding.UTF8.GetString(utf8name.ToArray())) });

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

        TraceReturn(status, new[] { Format(result) });
        return status;
    }

    public override napi_status GetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out napi_value result)
    {
        TraceCall(
            new[] { Format(env, js_object), Format(Encoding.UTF8.GetString(utf8name.ToArray())) });

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

        TraceReturn(status, new[] { Format(env, result) });
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
            new[] { Format(env, js_object), index.ToString() },
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
            new[] { Format(env, js_object), index.ToString() },
            () => (_runtime.GetElement(env, js_object, index, out resultValue),
                Format(env, resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status SetElement(
        napi_env env, napi_value js_object, uint index, napi_value value)
    {
        return TraceCall(
            new[] { Format(env, js_object), index.ToString(), Format(env, value) },
            () => _runtime.SetElement(env, js_object, index, value));
    }

    public override napi_status DeleteElement(
        napi_env env, napi_value js_object, uint index, out bool result)
    {
        bool resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object), index.ToString() },
            () => (_runtime.DeleteElement(env, js_object, index, out resultValue),
                Format(resultValue)));
        result = resultValue;
        return status;
    }

    #endregion

    #region Property and class definition

    public override napi_status GetPropertyNames(
        napi_env env,
        napi_value js_object,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object) },
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

    public override napi_status DefineProperties(
        napi_env env,
        napi_value js_object,
        ReadOnlySpan<napi_property_descriptor> properties)
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

        TraceReturn(status, new[] { Format(env, result) });
        return status;
    }

    public override napi_status GetPrototype(
        napi_env env,
        napi_value js_object,
        out napi_value result)
    {
        napi_value resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object) },
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

        TraceReturn(status, new[] { Format(env, result) });
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
            new[] { Format(env, js_object), Format(env, constructor) },
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
            new[] { Format(env, js_object), Format(native_object) },
            () => (_runtime.Wrap(
                env, js_object, native_object, finalize_cb, finalize_hint, out resultRef),
                Format(env, resultRef)));
        result = resultRef;
        return status;
    }

    public override napi_status Unwrap(napi_env env, napi_value js_object, out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object) },
            () => (_runtime.Unwrap(env, js_object, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    public override napi_status RemoveWrap(napi_env env, napi_value js_object, out nint result)
    {
        nint resultValue = default;
        napi_status status = TraceCall(
            new[] { Format(env, js_object) },
            () => (_runtime.RemoveWrap(env, js_object, out resultValue), Format(resultValue)));
        result = resultValue;
        return status;
    }

    #endregion
}
