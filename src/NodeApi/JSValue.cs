using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSValue : IEquatable<JSValue>
{
    private readonly napi_value _handle;
    private readonly JSValueScope? _scope;

    public readonly JSValueScope Scope =>
        _scope ?? JSValueScope.Current ?? throw new InvalidOperationException("No current scope");

    public JSValue() { }

    public JSValue(napi_value handle) : this(handle, JSValueScope.Current)
    {
    }

    public JSValue(napi_value handle, JSValueScope? scope)
    {
        if (!handle.IsNull)
        {
            ArgumentNullException.ThrowIfNull(scope);
        }
        _handle = handle;
        _scope = scope;
    }

    public napi_value? Handle
        => !Scope.IsDisposed ? (_handle.Handle != nint.Zero ? _handle : Undefined._handle) : null;

    public napi_value GetCheckedHandle()
        => Handle ?? throw new InvalidOperationException(
                        "The value handle is invalid because its scope is closed");

    private static napi_env Env => (napi_env)JSValueScope.Current;

    public static JSValue Undefined
        => napi_get_undefined(Env, out napi_value result).ThrowIfFailed(result);
    public static JSValue Null
        => napi_get_null(Env, out napi_value result).ThrowIfFailed(result);
    public static JSValue Global
        => napi_get_global(Env, out napi_value result).ThrowIfFailed(result);
    public static JSValue True => GetBoolean(true);
    public static JSValue False => GetBoolean(false);
    public static JSValue GetBoolean(bool value)
        => napi_get_boolean(Env, value, out napi_value result).ThrowIfFailed(result);

    public JSObject Properties => (JSObject)this;

    public JSArray Items => (JSArray)this;

    public JSValue this[JSValue name]
    {
        get => this.GetProperty(name);
        set => this.SetProperty(name, value);
    }

    public JSValue this[string name]
    {
        get => this.GetProperty(name);
        set => this.SetProperty(name, value);
    }

    public JSValue this[int index]
    {
        get => this.GetElement(index);
        set => this.SetElement(index, value);
    }

    public static JSValue CreateObject()
        => napi_create_object(Env, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateArray()
        => napi_create_array(Env, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateArray(int length)
        => napi_create_array_with_length(Env, (nuint)length, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateNumber(double value)
        => napi_create_double(Env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(int value)
        => napi_create_int32(Env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(uint value)
        => napi_create_uint32(Env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(long value)
        => napi_create_int64(Env, value, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateStringLatin1(ReadOnlySpan<byte> value)
    {
        fixed (byte* spanPtr = value)
        {
            return napi_create_string_latin1(
                Env, spanPtr, (nuint)value.Length, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf8(ReadOnlySpan<byte> value)
    {
        fixed (byte* spanPtr = value)
        {
            return napi_create_string_utf8(
                Env, spanPtr, (nuint)value.Length, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf16(ReadOnlySpan<char> value)
    {
        fixed (char* spanPtr = value)
        {
            return napi_create_string_utf16(
                Env, spanPtr, (nuint)value.Length, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static JSValue CreateSymbol(JSValue description)
        => napi_create_symbol(
            Env, (napi_value)description, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue SymbolFor(ReadOnlySpan<byte> utf8Name)
    {
        fixed (byte* name = utf8Name)
        {
            return node_api_symbol_for(Env, name, (nuint)utf8Name.Length, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateFunction(
        ReadOnlySpan<byte> utf8Name,
        napi_callback callback,
        nint data)
    {
        fixed (byte* namePtr = utf8Name)
        {
            return napi_create_function(
                Env, namePtr, (nuint)utf8Name.Length, callback, data, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateFunction(
        ReadOnlySpan<byte> utf8Name, JSCallback callback, object? callbackData = null)
    {
        GCHandle descriptorHandle = GCHandle.Alloc(
            new JSCallbackDescriptor(callback, callbackData));
        JSValue func = CreateFunction(
            utf8Name,
            new napi_callback(
                JSValueScope.Current?.ScopeType == JSValueScopeType.RootNoContext ?
                &InvokeJSCallbackNoContext : &InvokeJSCallback),
            (nint)descriptorHandle);
        func.AddGCHandleFinalizer((nint)descriptorHandle);
        return func;
    }

    public static JSValue CreateFunction(
        string name, JSCallback callback, object? callbackData = null)
    {
        int byteCount = Encoding.UTF8.GetByteCount(name);
        Span<byte> utf8Name = stackalloc byte[byteCount];
        Encoding.UTF8.GetBytes(name, utf8Name);
        return CreateFunction(utf8Name, callback, callbackData);
    }

    public static JSValue CreateError(JSValue? code, JSValue message)
        => napi_create_error(Env, code.AsNapiValueOrNull(), (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateTypeError(JSValue? code, JSValue message)
        => napi_create_type_error(Env, code.AsNapiValueOrNull(), (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateRangeError(JSValue? code, JSValue message)
        => napi_create_range_error(Env, code.AsNapiValueOrNull(), (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateSyntaxError(JSValue? code, JSValue message)
        => node_api_create_syntax_error(Env, code.AsNapiValueOrNull(), (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateExternal(object value)
    {
        GCHandle valueHandle = GCHandle.Alloc(value);
        return napi_create_external(
            Env,
            (nint)valueHandle,
            new napi_finalize(&FinalizeGCHandle),
            nint.Zero,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CreateArrayBuffer(int byteLength)
    {
        napi_create_arraybuffer(Env, (nuint)byteLength, out nint _, out napi_value result)
            .ThrowIfFailed();
        return result;
    }

    public static unsafe JSValue CreateArrayBuffer(ReadOnlySpan<byte> data)
    {
        napi_create_arraybuffer(Env, (nuint)data.Length, out nint buffer, out napi_value result)
            .ThrowIfFailed();
        data.CopyTo(new Span<byte>((void*)buffer, data.Length));
        return result;
    }

    public static unsafe JSValue CreateExternalArrayBuffer<T>(
        Memory<T> memory, object? external = null) where T : struct
    {
        var pinnedMemory = new PinnedMemory<T>(memory, external);
        return napi_create_external_arraybuffer(
            Env,
            (nint)pinnedMemory.Pointer,
            (nuint)pinnedMemory.Length,
            // We pass object to finalize as a hint parameter
            new napi_finalize(&FinalizeHintHandle),
            (nint)GCHandle.Alloc(pinnedMemory),
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CreateDataView(int length, JSValue arrayBuffer, int byteOffset)
        => napi_create_dataview(
            Env, (nuint)length, (napi_value)arrayBuffer, (nuint)byteOffset, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateTypedArray(
        JSTypedArrayType type, int length, JSValue arrayBuffer, int byteOffset)
        => napi_create_typedarray(
            Env,
            (napi_typedarray_type)type,
            (nuint)length,
            (napi_value)arrayBuffer,
            (nuint)byteOffset,
            out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreatePromise(out JSPromise.Deferred deferred)
    {
        napi_create_promise(Env, out napi_deferred deferred_, out napi_value promise)
            .ThrowIfFailed();
        deferred = new JSPromise.Deferred(deferred_);
        return promise;
    }

    public static JSValue CreateDate(double time)
        => napi_create_date(Env, time, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(long value)
        => napi_create_bigint_int64(Env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(ulong value)
        => napi_create_bigint_uint64(Env, value, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateBigInt(int signBit, ReadOnlySpan<ulong> words)
    {
        fixed (ulong* wordPtr = words)
        {
            return napi_create_bigint_words(
                Env, signBit, (nuint)words.Length, wordPtr, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static implicit operator JSValue(bool value) => GetBoolean(value);
    public static implicit operator JSValue(sbyte value) => CreateNumber(value);
    public static implicit operator JSValue(byte value) => CreateNumber(value);
    public static implicit operator JSValue(short value) => CreateNumber(value);
    public static implicit operator JSValue(ushort value) => CreateNumber(value);
    public static implicit operator JSValue(int value) => CreateNumber(value);
    public static implicit operator JSValue(uint value) => CreateNumber(value);
    public static implicit operator JSValue(long value) => CreateNumber(value);
    public static implicit operator JSValue(ulong value) => CreateNumber(value);
    public static implicit operator JSValue(float value) => CreateNumber(value);
    public static implicit operator JSValue(double value) => CreateNumber(value);
    public static implicit operator JSValue(bool? value) => ValueOrNull(value, value => GetBoolean(value));
    public static implicit operator JSValue(sbyte? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(byte? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(short? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(ushort? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(int? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(uint? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(long? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(ulong? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(float? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(double? value) => ValueOrNull(value, value => CreateNumber(value));
    public static implicit operator JSValue(string value) => value == null ? JSValue.Null : CreateStringUtf16(value);
    public static implicit operator JSValue(char[] value) => value == null ? JSValue.Null : CreateStringUtf16(value);
    public static implicit operator JSValue(Span<char> value) => CreateStringUtf16(value);
    public static implicit operator JSValue(ReadOnlySpan<char> value) => CreateStringUtf16(value);
    public static implicit operator JSValue(byte[] value) => value == null ? JSValue.Null : CreateStringUtf8(value);
    public static implicit operator JSValue(Span<byte> value) => CreateStringUtf8(value);
    public static implicit operator JSValue(ReadOnlySpan<byte> value) => CreateStringUtf8(value);
    public static implicit operator JSValue(JSCallback callback) => CreateFunction("Unknown", callback);

    public static explicit operator bool(JSValue value) => value.GetValueBool();
    public static explicit operator sbyte(JSValue value) => (sbyte)value.GetValueInt32();
    public static explicit operator byte(JSValue value) => (byte)value.GetValueUInt32();
    public static explicit operator short(JSValue value) => (short)value.GetValueInt32();
    public static explicit operator ushort(JSValue value) => (ushort)value.GetValueUInt32();
    public static explicit operator int(JSValue value) => value.GetValueInt32();
    public static explicit operator uint(JSValue value) => value.GetValueUInt32();
    public static explicit operator long(JSValue value) => value.GetValueInt64();
    public static explicit operator ulong(JSValue value) => (ulong)value.GetValueInt64();
    public static explicit operator float(JSValue value) => (float)value.GetValueDouble();
    public static explicit operator double(JSValue value) => value.GetValueDouble();
    public static explicit operator string(JSValue value) => value.IsNullOrUndefined() ? null! : value.GetValueStringUtf16();
    public static explicit operator char[](JSValue value) => value.IsNullOrUndefined() ? null! : value.GetValueStringUtf16AsCharArray();
    public static explicit operator byte[](JSValue value) => value.IsNullOrUndefined() ? null! : value.GetValueStringUtf8();
    public static explicit operator bool?(JSValue value) => ValueOrNull(value, value => value.GetValueBool());
    public static explicit operator sbyte?(JSValue value) => ValueOrNull(value, value => (sbyte)value.GetValueInt32());
    public static explicit operator byte?(JSValue value) => ValueOrNull(value, value => (byte)value.GetValueUInt32());
    public static explicit operator short?(JSValue value) => ValueOrNull(value, value => (short)value.GetValueInt32());
    public static explicit operator ushort?(JSValue value) => ValueOrNull(value, value => (ushort)value.GetValueUInt32());
    public static explicit operator int?(JSValue value) => ValueOrNull(value, value => value.GetValueInt32());
    public static explicit operator uint?(JSValue value) => ValueOrNull(value, value => value.GetValueUInt32());
    public static explicit operator long?(JSValue value) => ValueOrNull(value, value => value.GetValueInt64());
    public static explicit operator ulong?(JSValue value) => ValueOrNull(value, value => (ulong)value.GetValueInt64());
    public static explicit operator float?(JSValue value) => ValueOrNull(value, value => (float)value.GetValueDouble());
    public static explicit operator double?(JSValue value) => ValueOrNull(value, value => value.GetValueDouble());

    public static explicit operator napi_value(JSValue value) => value.GetCheckedHandle();
    public static implicit operator JSValue(napi_value handle) => new(handle);

    public static explicit operator napi_value(JSValue? value) => value?.Handle ?? new napi_value(nint.Zero);
    public static implicit operator JSValue?(napi_value handle) => handle.Handle != nint.Zero ? new JSValue(handle) : (JSValue?)null;

    private static JSValue ValueOrNull<T>(T? value, Func<T, JSValue> convert) where T : struct
        => value.HasValue ? convert(value.Value) : JSValue.Null;

    private static T? ValueOrNull<T>(JSValue value, Func<JSValue, T> convert) where T : struct
        => value.IsNullOrUndefined() ? null : convert(value);

    /// <summary>
    /// Delegate that provides a conversion from some type to a JS value.
    /// </summary>
    public delegate JSValue From<T>(T value);

    /// <summary>
    /// Delegate that provides a conversion from a JS value to some type.
    /// </summary>
    public delegate T To<T>(JSValue value);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSValue a, JSValue b) => a.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSValue a, JSValue b) => !a.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => this.StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }
}

internal static class JSValueExtensions
{
    public static napi_value AsNapiValueOrNull(this JSValue? value)
        => value is not null ? (napi_value)value.Value : napi_value.Null;
}
