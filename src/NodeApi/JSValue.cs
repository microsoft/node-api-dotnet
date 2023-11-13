// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSValue : IEquatable<JSValue>
{
    private readonly napi_value _handle = default;
    private readonly JSValueScope? _scope = null;

    public readonly JSValueScope Scope => _scope ?? JSValueScope.Current;

    internal JSRuntime Runtime => Scope.Runtime;

    /// <summary>
    /// Creates an empty instance of <see cref="JSValue" />, which implicitly converts to
    /// <see cref="JSValue.Undefined" /> when used in any scope.
    /// </summary>
    public JSValue() { }

    /// <summary>
    /// Creates a new instance of <see cref="JSValue" /> from a handle in the current scope.
    /// </summary>
    /// <remarks>
    /// WARNING: A JS value handle is a pointer to a location in memory, so an invalid handle here
    /// may cause an attempt to access an invalid memory location.
    /// </remarks>
    public JSValue(napi_value handle) : this(handle, JSValueScope.Current) { }

    /// <summary>
    /// Creates a new instance of <see cref="JSValue" /> from a handle in the specified scope.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the scope is null (unless the handle
    /// is also null).</exception>
    /// <remarks>
    /// WARNING: A JS value handle is a pointer to a location in memory, so an invalid handle here
    /// may cause an attempt to access an invalid memory location.
    /// </remarks>
    public JSValue(napi_value handle, JSValueScope? scope)
    {
        if (!handle.IsNull && scope is null) throw new ArgumentNullException(nameof(scope));
        _handle = handle;
        _scope = scope;
    }

    public napi_value Handle
    {
        get
        {
            if (_scope == null)
            {
                // If the scope is null, this is an empty (uninitialized) instance.
                // Implicitly convert to the JS `undefined` value.
                return Undefined._handle;
            }

            // Ensure the scope is valid and on the current thread (environment).
            _scope.CheckDisposed();
            _scope.CheckThreadAccess();

            // The handle must be non-null when the scope is non-null.
            return _handle;
        }
    }

    public static implicit operator JSValue(napi_value handle) => new(handle);
    public static implicit operator JSValue?(napi_value handle) => handle.Handle != default ? new(handle) : default;
    public static explicit operator napi_value(JSValue value) => value.Handle;
    public static explicit operator napi_value(JSValue? value) => value?.Handle ?? default;

    /// <summary>
    /// Gets the environment handle for the current <see cref="JSValue" /> instance.
    /// </summary>
    internal napi_env Env => (napi_env)Scope;

    /// <summary>
    /// Gets the environment handle for the current thread scope. For use only in static methods;
    /// for instance methods use <see cref="Env" /> instead.
    /// </summary>
    internal static napi_env CurrentEnv => (napi_env)JSValueScope.Current;

    public static JSValue Undefined
        => JSValueScope.CurrentRuntime.GetUndefined(CurrentEnv, out napi_value result).ThrowIfFailed(result);
    public static JSValue Null
        => JSValueScope.CurrentRuntime.GetNull(CurrentEnv, out napi_value result).ThrowIfFailed(result);
    public static JSValue Global
        => JSValueScope.CurrentRuntime.GetGlobal(CurrentEnv, out napi_value result).ThrowIfFailed(result);
    public static JSValue True => GetBoolean(true);
    public static JSValue False => GetBoolean(false);
    public static JSValue GetBoolean(bool value)
        => JSValueScope.CurrentRuntime.GetBoolean(CurrentEnv, value, out napi_value result).ThrowIfFailed(result);

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
        => JSValueScope.CurrentRuntime.CreateObject(CurrentEnv, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateArray()
        => JSValueScope.CurrentRuntime.CreateArray(CurrentEnv, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateArray(int length)
        => JSValueScope.CurrentRuntime.CreateArray(CurrentEnv, length, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateNumber(double value)
        => JSValueScope.CurrentRuntime.CreateNumber(CurrentEnv, value, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateNumber(int value)
        => JSValueScope.CurrentRuntime.CreateNumber(CurrentEnv, value, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateNumber(uint value)
        => JSValueScope.CurrentRuntime.CreateNumber(CurrentEnv, value, out napi_value result)
        .ThrowIfFailed(result);

    public static JSValue CreateNumber(long value)
        => JSValueScope.CurrentRuntime.CreateNumber(CurrentEnv, value, out napi_value result)
        .ThrowIfFailed(result);

    public static unsafe JSValue CreateStringUtf8(ReadOnlySpan<byte> value)
    {
        fixed (byte* spanPtr = value)
        {
            return JSValueScope.CurrentRuntime.CreateString(CurrentEnv, value, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf16(ReadOnlySpan<char> value)
    {
        fixed (char* spanPtr = value)
        {
            return JSValueScope.CurrentRuntime.CreateString(CurrentEnv, value, out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf16(string value)
    {
        fixed (char* spanPtr = value)
        {
            return JSValueScope.CurrentRuntime.CreateString(CurrentEnv, value.AsSpan(), out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static JSValue CreateSymbol(JSValue description)
        => JSValueScope.CurrentRuntime.CreateSymbol(
            CurrentEnv, (napi_value)description, out napi_value result).ThrowIfFailed(result);

    public static JSValue SymbolFor(string name)
    {
        return JSValueScope.CurrentRuntime.GetSymbolFor(CurrentEnv, name, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CreateFunction(
        string? name,
        napi_callback callback,
        nint data)
    {
        return JSValueScope.CurrentRuntime.CreateFunction(
            CurrentEnv, name, callback, data, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CreateFunction(
        string? name, JSCallback callback, object? callbackData = null)
    {
        GCHandle descriptorHandle = JSRuntimeContext.Current.AllocGCHandle(
            new JSCallbackDescriptor(name, callback, callbackData));
        JSValue func = CreateFunction(
            name,
            new napi_callback(
                JSValueScope.Current?.ScopeType == JSValueScopeType.NoContext ?
                s_invokeJSCallbackNC : s_invokeJSCallback),
            (nint)descriptorHandle);
        func.AddGCHandleFinalizer((nint)descriptorHandle);
        return func;
    }

    public static JSValue CreateError(JSValue? code, JSValue message)
        => JSValueScope.CurrentRuntime.CreateError(CurrentEnv, (napi_value)code, (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateTypeError(JSValue? code, JSValue message)
        => JSValueScope.CurrentRuntime.CreateTypeError(CurrentEnv, (napi_value)code, (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateRangeError(JSValue? code, JSValue message)
        => JSValueScope.CurrentRuntime.CreateRangeError(CurrentEnv, (napi_value)code, (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateSyntaxError(JSValue? code, JSValue message)
        => JSValueScope.CurrentRuntime.CreateSyntaxError(CurrentEnv, (napi_value)code, (napi_value)message,
            out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateExternal(object value)
    {
        JSValueScope currentScope = JSValueScope.Current;
        GCHandle valueHandle = currentScope.RuntimeContext.AllocGCHandle(value);
        return JSValueScope.CurrentRuntime.CreateExternal(
            (napi_env)currentScope,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            currentScope.RuntimeContextHandle,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CreateArrayBuffer(int byteLength)
    {
        JSValueScope.CurrentRuntime.CreateArrayBuffer(CurrentEnv, byteLength, out nint _, out napi_value result)
            .ThrowIfFailed();
        return result;
    }

    public static unsafe JSValue CreateArrayBuffer(ReadOnlySpan<byte> data)
    {
        JSValueScope.CurrentRuntime.CreateArrayBuffer(CurrentEnv, data.Length, out nint buffer, out napi_value result)
            .ThrowIfFailed();
        data.CopyTo(new Span<byte>((void*)buffer, data.Length));
        return result;
    }

    public static unsafe JSValue CreateExternalArrayBuffer<T>(
        Memory<T> memory, object? external = null) where T : struct
    {
        var pinnedMemory = new PinnedMemory<T>(memory, external);
        return JSValueScope.CurrentRuntime.CreateArrayBuffer(
            CurrentEnv,
            (nint)pinnedMemory.Pointer,
            pinnedMemory.Length,
            // We pass object to finalize as a hint parameter
            new napi_finalize(s_finalizeGCHandleToPinnedMemory),
            (nint)pinnedMemory.RuntimeContext.AllocGCHandle(pinnedMemory),
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CreateDataView(int length, JSValue arrayBuffer, int byteOffset)
        => JSValueScope.CurrentRuntime.CreateDataView(
            CurrentEnv, length, (napi_value)arrayBuffer, byteOffset, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateTypedArray(
        JSTypedArrayType type, int length, JSValue arrayBuffer, int byteOffset)
        => JSValueScope.CurrentRuntime.CreateTypedArray(
            CurrentEnv,
            (napi_typedarray_type)type,
            length,
            (napi_value)arrayBuffer,
            byteOffset,
            out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreatePromise(out JSPromise.Deferred deferred)
    {
        JSValueScope.CurrentRuntime.CreatePromise(CurrentEnv, out napi_deferred deferred_, out napi_value promise)
            .ThrowIfFailed();
        deferred = new JSPromise.Deferred(deferred_);
        return promise;
    }

    public static JSValue CreateDate(double time)
        => JSValueScope.CurrentRuntime.CreateDate(CurrentEnv, time, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(long value)
        => JSValueScope.CurrentRuntime.CreateBigInt(CurrentEnv, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(ulong value)
        => JSValueScope.CurrentRuntime.CreateBigInt(CurrentEnv, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(int signBit, ReadOnlySpan<ulong> words)
    {
        return JSValueScope.CurrentRuntime.CreateBigInt(CurrentEnv, signBit, words, out napi_value result)
            .ThrowIfFailed(result);
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
    public static implicit operator JSValue(bool? value) => ValueOrDefault(value, value => GetBoolean(value));
    public static implicit operator JSValue(sbyte? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(byte? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(short? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(ushort? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(int? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(uint? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(long? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(ulong? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(float? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(double? value) => ValueOrDefault(value, value => CreateNumber(value));
    public static implicit operator JSValue(string value) => value == null ? default : CreateStringUtf16(value);
    public static implicit operator JSValue(char[] value) => value == null ? default : CreateStringUtf16(value);
    public static implicit operator JSValue(Span<char> value) => CreateStringUtf16(value);
    public static implicit operator JSValue(ReadOnlySpan<char> value) => CreateStringUtf16(value);
    public static implicit operator JSValue(byte[] value) => value == null ? default : CreateStringUtf8(value);
    public static implicit operator JSValue(Span<byte> value) => CreateStringUtf8(value);
    public static implicit operator JSValue(ReadOnlySpan<byte> value) => CreateStringUtf8(value);

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
    public static explicit operator bool?(JSValue value) => ValueOrDefault(value, value => value.GetValueBool());
    public static explicit operator sbyte?(JSValue value) => ValueOrDefault(value, value => (sbyte)value.GetValueInt32());
    public static explicit operator byte?(JSValue value) => ValueOrDefault(value, value => (byte)value.GetValueUInt32());
    public static explicit operator short?(JSValue value) => ValueOrDefault(value, value => (short)value.GetValueInt32());
    public static explicit operator ushort?(JSValue value) => ValueOrDefault(value, value => (ushort)value.GetValueUInt32());
    public static explicit operator int?(JSValue value) => ValueOrDefault(value, value => value.GetValueInt32());
    public static explicit operator uint?(JSValue value) => ValueOrDefault(value, value => value.GetValueUInt32());
    public static explicit operator long?(JSValue value) => ValueOrDefault(value, value => value.GetValueInt64());
    public static explicit operator ulong?(JSValue value) => ValueOrDefault(value, value => (ulong)value.GetValueInt64());
    public static explicit operator float?(JSValue value) => ValueOrDefault(value, value => (float)value.GetValueDouble());
    public static explicit operator double?(JSValue value) => ValueOrDefault(value, value => value.GetValueDouble());

    private static JSValue ValueOrDefault<T>(T? value, Func<T, JSValue> convert) where T : struct
        => value.HasValue ? convert(value.Value) : default;

    private static T? ValueOrDefault<T>(JSValue value, Func<JSValue, T> convert) where T : struct
        => value.IsNullOrUndefined() ? default : convert(value);

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
