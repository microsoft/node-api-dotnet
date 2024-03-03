// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.JSValueScope;
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
    public JSValue()
    {
        _handle = default;
        _scope = null;
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSValue" /> from a handle in the current scope.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
    /// <remarks>
    /// WARNING: A JS value handle is a pointer to a location in memory, so an invalid handle here
    /// may cause an attempt to access an invalid memory location.
    /// </remarks>
    public JSValue(napi_value handle) : this(handle, Current) { }

    /// <summary>
    /// Creates a new instance of <see cref="JSValue" /> from a handle in the specified scope.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the handle is null</exception>
    /// <remarks>
    /// WARNING: A JS value handle is a pointer to a location in memory, so an invalid handle here
    /// may cause an attempt to access an invalid memory location.
    /// </remarks>
    public JSValue(napi_value handle, JSValueScope scope)
    {
        if (handle.IsNull) throw new ArgumentNullException(nameof(handle));
        _handle = handle;
        _scope = scope;
    }

    /// <summary>
    /// Gets the value handle, or throws an exception if the value scope is disposed or
    /// access from the current thread is invalid.
    /// </summary>
    /// <exception cref="JSValueScopeClosedException">The scope has been closed.</exception>
    /// <exception cref="JSInvalidThreadAccessException">The scope is not valid on the current
    /// thread.</exception>
    public napi_value Handle
    {
        get
        {
            if (_scope == null)
            {
                // If the scope is null, this is an empty (uninitialized) instance.
                // Implicitly convert to the JS `undefined` value.
                return GetCurrentRuntime(out napi_env env)
                    .GetUndefined(env, out napi_value result).ThrowIfFailed(result);
            }

            // Ensure the scope is valid and on the current thread (environment).
            _scope.ThrowIfDisposed();
            _scope.ThrowIfInvalidThreadAccess();

            // The handle must be non-null when the scope is non-null.
            return _handle;
        }
    }

    public static implicit operator JSValue(napi_value handle) => new(handle);
    public static implicit operator JSValue?(napi_value handle) => handle.Handle != default ? new(handle) : default;
    public static explicit operator napi_value(JSValue value) => value.Handle;
    public static explicit operator napi_value(JSValue? value) => value?.Handle ?? default;

    public static JSValue Undefined => default;
    public static JSValue Null => GetCurrentRuntime(out napi_env env)
        .GetNull(env, out napi_value result).ThrowIfFailed(result);
    public static JSValue Global => GetCurrentRuntime(out napi_env env)
        .GetGlobal(env, out napi_value result).ThrowIfFailed(result);
    public static JSValue True => GetBoolean(true);
    public static JSValue False => GetBoolean(false);
    public static JSValue GetBoolean(bool value) => GetCurrentRuntime(out napi_env env)
        .GetBoolean(env, value, out napi_value result).ThrowIfFailed(result);

    public JSObject Properties => (JSObject)this;

    public JSArray Items => (JSArray)this;

    public JSValue this[JSValue name]
    {
        get => GetProperty(name);
        set => SetProperty(name, value);
    }

    public JSValue this[string name]
    {
        get => GetProperty(name);
        set => SetProperty(name, value);
    }

    public JSValue this[int index]
    {
        get => GetElement(index);
        set => SetElement(index, value);
    }

    public static JSValue CreateObject() => GetCurrentRuntime(out napi_env env)
        .CreateObject(env, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateArray() => GetCurrentRuntime(out napi_env env)
        .CreateArray(env, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateArray(int length) => GetCurrentRuntime(out napi_env env)
        .CreateArray(env, length, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(double value) => GetCurrentRuntime(out napi_env env)
        .CreateNumber(env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(int value) => GetCurrentRuntime(out napi_env env)
        .CreateNumber(env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(uint value) => GetCurrentRuntime(out napi_env env)
        .CreateNumber(env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateNumber(long value) => GetCurrentRuntime(out napi_env env)
        .CreateNumber(env, value, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateStringUtf8(ReadOnlySpan<byte> value)
    {
        fixed (byte* spanPtr = value)
        {
            return GetCurrentRuntime(out napi_env env)
                .CreateString(env, value, out napi_value result).ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf16(ReadOnlySpan<char> value)
    {
        fixed (char* spanPtr = value)
        {
            return GetCurrentRuntime(out napi_env env)
                .CreateString(env, value, out napi_value result).ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CreateStringUtf16(string value)
    {
        fixed (char* spanPtr = value)
        {
            return GetCurrentRuntime(out napi_env env)
                .CreateString(env, value.AsSpan(), out napi_value result).ThrowIfFailed(result);
        }
    }

    public static JSValue CreateSymbol(JSValue description) => GetCurrentRuntime(out napi_env env)
        .CreateSymbol(env, (napi_value)description, out napi_value result).ThrowIfFailed(result);

    public static JSValue SymbolFor(string name) => GetCurrentRuntime(out napi_env env)
        .GetSymbolFor(env, name, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateFunction(
        string? name,
        napi_callback callback,
        nint data)
    {
        return GetCurrentRuntime(out napi_env env)
            .CreateFunction(env, name, callback, data, out napi_value result).ThrowIfFailed(result);
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
        => GetCurrentRuntime(out napi_env env)
            .CreateError(env, (napi_value)code, (napi_value)message, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateTypeError(JSValue? code, JSValue message)
        => GetCurrentRuntime(out napi_env env)
            .CreateTypeError(env, (napi_value)code, (napi_value)message, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateRangeError(JSValue? code, JSValue message)
        => GetCurrentRuntime(out napi_env env)
            .CreateRangeError(env, (napi_value)code, (napi_value)message, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateSyntaxError(JSValue? code, JSValue message)
        => GetCurrentRuntime(out napi_env env)
            .CreateSyntaxError(env, (napi_value)code, (napi_value)message, out napi_value result)
        .ThrowIfFailed(result);

    public static unsafe JSValue CreateExternal(object value)
    {
        JSValueScope currentScope = JSValueScope.Current;
        GCHandle valueHandle = currentScope.RuntimeContext.AllocGCHandle(value);
        return currentScope.Runtime.CreateExternal(
            currentScope.UncheckedEnvironmentHandle,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            currentScope.RuntimeContextHandle,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CreateArrayBuffer(int byteLength)
        => GetCurrentRuntime(out napi_env env)
            .CreateArrayBuffer(env, byteLength, out nint _, out napi_value result)
            .ThrowIfFailed(result);

    public static unsafe JSValue CreateArrayBuffer(ReadOnlySpan<byte> data)
    {
        GetCurrentRuntime(out napi_env env)
            .CreateArrayBuffer(env, data.Length, out nint buffer, out napi_value result)
            .ThrowIfFailed();
        data.CopyTo(new Span<byte>((void*)buffer, data.Length));
        return result;
    }

    public static unsafe JSValue CreateExternalArrayBuffer<T>(
        Memory<T> memory, object? external = null) where T : struct
    {
        var pinnedMemory = new PinnedMemory<T>(memory, external);
        return GetCurrentRuntime(out napi_env env).CreateArrayBuffer(
            env,
            (nint)pinnedMemory.Pointer,
            pinnedMemory.Length,
            // We pass object to finalize as a hint parameter
            new napi_finalize(s_finalizeGCHandleToPinnedMemory),
            (nint)pinnedMemory.RuntimeContext.AllocGCHandle(pinnedMemory),
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CreateDataView(int length, JSValue arrayBuffer, int byteOffset)
        => GetCurrentRuntime(out napi_env env)
            .CreateDataView(env, length, (napi_value)arrayBuffer, byteOffset, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreateTypedArray(
        JSTypedArrayType type, int length, JSValue arrayBuffer, int byteOffset)
        => GetCurrentRuntime(out napi_env env).CreateTypedArray(
            env,
            (napi_typedarray_type)type,
            length,
            (napi_value)arrayBuffer,
            byteOffset,
            out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CreatePromise(out JSPromise.Deferred deferred)
    {
        GetCurrentRuntime(out napi_env env)
            .CreatePromise(env, out napi_deferred deferred_, out napi_value promise)
            .ThrowIfFailed();
        deferred = new JSPromise.Deferred(deferred_);
        return promise;
    }

    public static JSValue CreateDate(double time) => GetCurrentRuntime(out napi_env env)
        .CreateDate(env, time, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(long value) => GetCurrentRuntime(out napi_env env)
        .CreateBigInt(env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(ulong value) => GetCurrentRuntime(out napi_env env)
        .CreateBigInt(env, value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CreateBigInt(int signBit, ReadOnlySpan<ulong> words)
        => GetCurrentRuntime(out napi_env env)
            .CreateBigInt(env, signBit, words, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue CreateBigInt(BigInteger value)
    {
        // .Net Framework 4.7.2 does not support Span-related methods for BigInteger.
        int sign = value.Sign == -1 ? 1 : 0;
        if (value.Sign == -1)
        {
            value = -value;
        }
#if !NETFRAMEWORK
        int byteCount = value.GetByteCount(isUnsigned: true);
#else
        byte[] bytes = value.ToByteArray();
        int byteCount = bytes.Length;
#endif
        int wordCount = (byteCount + sizeof(ulong) - 1) / sizeof(ulong);
        Span<byte> byteSpan = stackalloc byte[wordCount * sizeof(ulong)];
#if !NETFRAMEWORK
        if (!value.TryWriteBytes(byteSpan, out int bytesWritten, isUnsigned: true))
        {
            throw new Exception("Cannot write BigInteger bytes");
        }
#endif
        fixed (byte* bytePtr = byteSpan)
        {
#if NETFRAMEWORK
            Marshal.Copy(bytes, 0, (nint)bytePtr, bytes.Length);
#endif
            ReadOnlySpan<ulong> words = new(bytePtr, wordCount);
            return CreateBigInt(sign, words);
        }
    }

    public unsafe BigInteger ToBigInteger()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetBigIntWordCount(env, handle, out nuint wordCount).ThrowIfFailed();
        Span<ulong> words = stackalloc ulong[(int)(wordCount > 0 ? wordCount : 1)];
        int byteCount = (int)wordCount * sizeof(ulong);
        runtime.GetBigIntWords(env, handle, out int sign, words, out _).ThrowIfFailed();
        fixed (ulong* wordPtr = words)
        {
#if !NETFRAMEWORK
            BigInteger result = new(new ReadOnlySpan<byte>(wordPtr, byteCount), isUnsigned: true);
#else
            byte[] bytes = new byte[byteCount];
            Marshal.Copy((nint)wordPtr, bytes, 0, byteCount);
            BigInteger result = new(bytes);
#endif
            return sign == 1 ? -result : result;
        }
    }

    public JSValueType TypeOf() => _handle.IsNull
        ? JSValueType.Undefined
        : GetRuntime(out napi_env env).GetValueType(env, _handle, out napi_valuetype result)
            .ThrowIfFailed((JSValueType)result);

    public bool IsUndefined() => TypeOf() == JSValueType.Undefined;

    public bool IsNull() => TypeOf() == JSValueType.Null;

    public bool IsNullOrUndefined() => TypeOf() switch
    {
        JSValueType.Null => true,
        JSValueType.Undefined => true,
        _ => false,
    };

    public bool IsBoolean() => TypeOf() == JSValueType.Boolean;

    public bool IsNumber() => TypeOf() == JSValueType.Number;

    public bool IsString() => TypeOf() == JSValueType.String;

    public bool IsSymbol() => TypeOf() == JSValueType.Symbol;

    public bool IsObject() => TypeOf() switch
    {
        JSValueType.Object => true,
        JSValueType.Function => true,
        _ => false,
    };

    public bool IsFunction() => TypeOf() == JSValueType.Function;

    public bool IsExternal() => TypeOf() == JSValueType.External;

    public bool IsBigInt() => TypeOf() == JSValueType.BigInt;

    public bool Is<TValue>() where TValue : struct
#if NET7_0_OR_GREATER
        , IJSValue<TValue> => TValue.CanBeConvertedFrom(this);
#else
        => IJSValueShim<TValue>.CanBeConvertedFrom(this);
#endif

    public TValue? As<TValue>() where TValue : struct
#if NET7_0_OR_GREATER
        , IJSValue<TValue>
        => TValue.CanBeConvertedFrom(this) ? TValue.CreateUnchecked(this) : default;
#else
        => IJSValueShim<TValue>.CanBeConvertedFrom(this)
            ? IJSValueShim<TValue>.CreateUnchecked(this) : default;
#endif

    public TValue AsUnchecked<TValue>() where TValue : struct
#if NET7_0_OR_GREATER
        , IJSValue<TValue> => TValue.CreateUnchecked(this);
#else
        => IJSValueShim<TValue>.CreateUnchecked(this);
#endif

    public double GetValueDouble() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueDouble(env, handle, out double result).ThrowIfFailed(result);

    public int GetValueInt32() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueInt32(env, handle, out int result).ThrowIfFailed(result);

    public uint GetValueUInt32() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueUInt32(env, handle, out uint result).ThrowIfFailed(result);

    public long GetValueInt64() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueInt64(env, handle, out long result).ThrowIfFailed(result);

    public bool GetValueBool() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueBool(env, handle, out bool result).ThrowIfFailed(result);

    public int GetValueStringUtf8(Span<byte> buffer)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetValueStringUtf8(env, handle, buffer, out int result)
            .ThrowIfFailed(result);

    public byte[] GetValueStringUtf8()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetValueStringUtf8(env, handle, [], out int length).ThrowIfFailed();
        byte[] result = new byte[length + 1];
        runtime.GetValueStringUtf8(env, handle, new Span<byte>(result), out _).ThrowIfFailed();
        // Remove the zero terminating character
        Array.Resize(ref result, length);
        return result;
    }

    public unsafe int GetValueStringUtf16(Span<char> buffer)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetValueStringUtf16(env, handle, buffer, out int result)
            .ThrowIfFailed(result);

    public char[] GetValueStringUtf16AsCharArray()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetValueStringUtf16(env, handle, [], out int length).ThrowIfFailed();
        char[] result = new char[length + 1];
        runtime.GetValueStringUtf16(env, handle, new Span<char>(result), out _).ThrowIfFailed();
        // Remove the zero terminating character
        Array.Resize(ref result, length);
        return result;
    }

    public unsafe string GetValueStringUtf16()
    {
#if NETFRAMEWORK
        return new string(GetValueStringUtf16AsCharArray());
#else
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetValueStringUtf16(env, handle, [], out int length).ThrowIfFailed();
        return string.Create(length, runtime, (span, runtime) =>
        {
            fixed (void* ptr = span)
            {
                runtime.GetValueStringUtf16(
                    env, handle, new Span<char>(ptr, span.Length + 1), out _).ThrowIfFailed();
            }
        });
#endif
    }

    public JSValue CoerceToBoolean() => GetRuntime(out napi_env env, out napi_value handle)
        .CoerceToBool(env, handle, out napi_value result).ThrowIfFailed(result);

    public JSValue CoerceToNumber() => GetRuntime(out napi_env env, out napi_value handle)
        .CoerceToNumber(env, handle, out napi_value result).ThrowIfFailed(result);

    public JSValue CoerceToObject() => GetRuntime(out napi_env env, out napi_value handle)
        .CoerceToObject(env, handle, out napi_value result).ThrowIfFailed(result);

    public JSValue CoerceToString() => GetRuntime(out napi_env env, out napi_value handle)
        .CoerceToString(env, handle, out napi_value result).ThrowIfFailed(result);

    public JSValue GetPrototype() => GetRuntime(out napi_env env, out napi_value handle)
        .GetPrototype(env, handle, out napi_value result).ThrowIfFailed(result);


    public JSValue GetPropertyNames() => GetRuntime(out napi_env env, out napi_value handle)
        .GetPropertyNames(env, handle, out napi_value result).ThrowIfFailed(result);

    public void SetProperty(JSValue key, JSValue value)
        => GetRuntime(out napi_env env, out napi_value handle)
            .SetProperty(env, handle, key.Handle, value.Handle).ThrowIfFailed();

    public bool HasProperty(JSValue key)
        => GetRuntime(out napi_env env, out napi_value handle)
            .HasProperty(env, handle, key.Handle, out bool result).ThrowIfFailed(result);

    public JSValue GetProperty(JSValue key)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetProperty(env, handle, key.Handle, out napi_value result).ThrowIfFailed(result);

    public bool DeleteProperty(JSValue key)
        => GetRuntime(out napi_env env, out napi_value handle)
            .DeleteProperty(env, handle, key.Handle, out bool result).ThrowIfFailed(result);

    public bool HasOwnProperty(JSValue key)
        => GetRuntime(out napi_env env, out napi_value handle)
            .HasOwnProperty(env, handle, key.Handle, out bool result).ThrowIfFailed(result);

    public void SetElement(int index, JSValue value)
        => GetRuntime(out napi_env env, out napi_value handle)
            .SetElement(env, handle, (uint)index, value.Handle).ThrowIfFailed();

    public bool HasElement(int index)
        => GetRuntime(out napi_env env, out napi_value handle)
            .HasElement(env, handle, (uint)index, out bool result).ThrowIfFailed(result);

    public JSValue GetElement(int index)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetElement(env, handle, (uint)index, out napi_value result).ThrowIfFailed(result);

    public bool DeleteElement(int index)
        => GetRuntime(out napi_env env, out napi_value handle)
            .DeleteElement(env, handle, (uint)index, out bool result).ThrowIfFailed(result);

    public unsafe void DefineProperties(IReadOnlyCollection<JSPropertyDescriptor> descriptors)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        nint[] descriptorHandles = ToUnmanagedPropertyDescriptors(
            string.Empty,
            descriptors,
            (_, descriptorsPtr) => runtime.DefineProperties(env, handle, descriptorsPtr)
            .ThrowIfFailed());
        foreach (nint descriptorHandle in descriptorHandles)
        {
            AddGCHandleFinalizer(descriptorHandle);
        }
    }

    public unsafe void DefineProperties(params JSPropertyDescriptor[] descriptors)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        nint[] descriptorHandles = ToUnmanagedPropertyDescriptors(
            string.Empty,
            descriptors,
            (_, descriptorsPtr) => runtime.DefineProperties(env, handle, descriptorsPtr)
            .ThrowIfFailed());
        foreach (nint descriptorHandle in descriptorHandles)
        {
            AddGCHandleFinalizer(descriptorHandle);
        }
    }

    public bool IsArray() => GetRuntime(out napi_env env, out napi_value handle)
        .IsArray(env, handle, out bool result).ThrowIfFailed(result);

    public int GetArrayLength() => GetRuntime(out napi_env env, out napi_value handle)
        .GetArrayLength(env, handle, out int result).ThrowIfFailed(result);

    // Internal because JSValue structs all implement IEquatable<JSValue>, which calls this method.
    internal bool StrictEquals(JSValue other) => GetRuntime(out napi_env env, out napi_value handle)
        .StrictEquals(env, handle, other.Handle, out bool result).ThrowIfFailed(result);

    public unsafe JSValue Call()
        => GetRuntime(out napi_env env, out napi_value handle, out JSRuntime runtime)
            .CallFunction(
                env,
                GetUndefined(runtime, env),
                handle,
                new ReadOnlySpan<napi_value>(),
                out napi_value result).ThrowIfFailed(result);

    public unsafe JSValue Call(JSValue thisArg)
        => GetRuntime(out napi_env env, out napi_value handle)
            .CallFunction(env, thisArg.Handle, handle, [], out napi_value result)
            .ThrowIfFailed(result);

    public unsafe JSValue Call(JSValue thisArg, JSValue arg0)
    {
        Span<napi_value> args = stackalloc napi_value[] { arg0.Handle };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.CallFunction(env, thisArg.Handle, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue Call(JSValue thisArg, JSValue arg0, JSValue arg1)
    {
        Span<napi_value> args = stackalloc napi_value[] { arg0.Handle, arg1.Handle };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.CallFunction(env, thisArg.Handle, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue Call(JSValue thisArg, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        Span<napi_value> args = stackalloc napi_value[]
        {
            arg0.Handle,
            arg1.Handle,
            arg2.Handle
        };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.CallFunction(env, thisArg.Handle, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue Call(JSValue thisArg, params JSValue[] args)
        => Call(thisArg, new ReadOnlySpan<JSValue>(args));

    public unsafe JSValue Call(JSValue thisArg, ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        Span<napi_value> argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = args[i].Handle;
        }

        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.CallFunction(
            env,
            thisArg.Handle,
            handle,
            argv,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue Call(napi_value thisArg, ReadOnlySpan<napi_value> args)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.CallFunction(
            env,
            thisArg,
            handle,
            args,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue CallAsConstructor()
        => GetRuntime(out napi_env env, out napi_value handle)
            .NewInstance(env, handle, [], out napi_value result)
            .ThrowIfFailed(result);

    public unsafe JSValue CallAsConstructor(JSValue arg0)
    {
        napi_value argValue0 = arg0.Handle;
        Span<napi_value> args = stackalloc napi_value[1] { argValue0 };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.NewInstance(env, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue CallAsConstructor(JSValue arg0, JSValue arg1)
    {
        Span<napi_value> args = stackalloc napi_value[2] { arg0.Handle, arg1.Handle };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.NewInstance(env, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue CallAsConstructor(JSValue arg0, JSValue arg1, JSValue arg2)
    {
        Span<napi_value> args = stackalloc napi_value[3] {
            arg0.Handle,
            arg1.Handle,
            arg2.Handle
        };
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.NewInstance(env, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue CallAsConstructor(params JSValue[] args)
        => CallAsConstructor(new ReadOnlySpan<JSValue>(args));

    public unsafe JSValue CallAsConstructor(ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        Span<napi_value> argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = args[i].Handle;
        }

        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.NewInstance(env, handle, argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public unsafe JSValue CallAsConstructor(ReadOnlySpan<napi_value> args)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        return runtime.NewInstance(env, handle, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public JSValue CallMethod(JSValue methodName)
        => GetProperty(methodName).Call(this);

    public JSValue CallMethod(JSValue methodName, JSValue arg0)
        => GetProperty(methodName).Call(this, arg0);

    public JSValue CallMethod(
        JSValue methodName, JSValue arg0, JSValue arg1)
        => GetProperty(methodName).Call(this, arg0, arg1);

    public JSValue CallMethod(JSValue methodName, JSValue arg0, JSValue arg1, JSValue arg2)
        => GetProperty(methodName).Call(this, arg0, arg1, arg2);

    public JSValue CallMethod(JSValue methodName, params JSValue[] args)
        => GetProperty(methodName).Call(this, args);

    public JSValue CallMethod(JSValue methodName, ReadOnlySpan<JSValue> args)
        => GetProperty(methodName).Call(this, args);

    public JSValue CallMethod(JSValue methodName, ReadOnlySpan<napi_value> args)
        => GetProperty(methodName).Call(Handle, args);

    public bool InstanceOf(JSValue constructor)
        => GetRuntime(out napi_env env, out napi_value handle)
            .InstanceOf(env, handle, constructor.Handle, out bool result)
            .ThrowIfFailed(result);

    public static unsafe JSValue DefineClass(
        string name,
        napi_callback callback,
        nint data,
        ReadOnlySpan<napi_property_descriptor> descriptors)
        => GetCurrentRuntime(out napi_env env)
            .DefineClass(env, name, callback, data, descriptors, out napi_value result)
            .ThrowIfFailed(result);

    public static unsafe JSValue DefineClass(
        string name,
        JSCallbackDescriptor constructorDescriptor,
        params JSPropertyDescriptor[] propertyDescriptors)
    {
        GCHandle descriptorHandle = JSRuntimeContext.Current.AllocGCHandle(constructorDescriptor);
        JSValue? func = null;
        napi_callback callback = new(
            Current?.ScopeType == JSValueScopeType.NoContext
            ? s_invokeJSCallbackNC : s_invokeJSCallback);

        nint[] handles = ToUnmanagedPropertyDescriptors(
            name, propertyDescriptors, (name, descriptorsPtr) =>
            {
                func = DefineClass(name, callback, (nint)descriptorHandle, descriptorsPtr);
            });
        func!.Value.AddGCHandleFinalizer((nint)descriptorHandle);
        Array.ForEach(handles, handle => func!.Value.AddGCHandleFinalizer(handle));
        return func!.Value;
    }

    /// <summary>
    /// Attaches an object to this JSValue.
    /// </summary>
    /// <param name="value">The object to be wrapped.</param>
    /// <returns>Copy of this JSValue struct.</returns>
    public unsafe JSValue Wrap(object value)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        GCHandle valueHandle = _scope!.RuntimeContext.AllocGCHandle(value);
        runtime.Wrap(
            env,
            handle,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            _scope!.RuntimeContextHandle).ThrowIfFailed();
        return this;
    }

    /// <summary>
    /// Attaches an object to this JSValue.
    /// </summary>
    /// <param name="value">The object to be wrapped.</param>
    /// <param name="wrapperWeakRef">Returns a weak reference to the JS wrapper.</param>
    /// <returns>The JS wrapper.</returns>
    public unsafe JSValue Wrap(object value, out JSReference wrapperWeakRef)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        GCHandle valueHandle = _scope!.RuntimeContext.AllocGCHandle(value);
        runtime.Wrap(
            env,
            handle,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            _scope!.RuntimeContextHandle,
            out napi_ref weakRef).ThrowIfFailed();
        wrapperWeakRef = new JSReference(weakRef, isWeak: true);
        return this;
    }

    /// <summary>
    /// Attempts to get the object that was previously attached to this JSValue.
    /// </summary>
    /// <returns>The unwrapped object, or null if nothing was wrapped.</returns>
    public object? TryUnwrap()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        napi_status status = runtime.Unwrap(env, handle, out nint result);

        // The invalid arg error code is returned if there was nothing to unwrap. It doesn't
        // distinguish from an invalid handle, but either way the unwrap failed.
        if (status == napi_status.napi_invalid_arg)
        {
            return null;
        }

        status.ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target;
    }

    /// <summary>
    /// Gets the object that was previously attached to this JSValue.
    /// (Throws an exception if unwrapping failed.)
    /// </summary>
    public object Unwrap(string? unwrapType = null)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        napi_status status = runtime.Unwrap(env, handle, out nint result);

        if (status == napi_status.napi_invalid_arg && unwrapType != null)
        {
            throw new JSException(new JSError($"Failed to unwrap object of type '{unwrapType}'"));
        }

        status.ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    /// <summary>
    /// Detaches an object from this JSValue.
    /// </summary>
    /// <param name="value">Returns the wrapped object, or null if nothing was wrapped.</param>
    /// <returns>True if a wrapped object was found and removed, else false.</returns>
    public bool RemoveWrap(out object? value)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        napi_status status = runtime.RemoveWrap(env, handle, out nint result);

        // The invalid arg error code is returned if there was nothing to remove.
        if (status == napi_status.napi_invalid_arg)
        {
            value = null;
            return false;
        }

        status.ThrowIfFailed();
        value = GCHandle.FromIntPtr(result).Target;
        return true;
    }

    /// <summary>
    /// Gets the object that is represented as an external value.
    /// (Throws if the JS value is not an external value.)
    /// </summary>
    public unsafe object GetValueExternal()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetValueExternal(env, handle, out nint result).ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    /// <summary>
    /// Gets the object that is represented as an external value, or null if the JS value
    /// is not an external value.
    /// </summary>
    public unsafe object? TryGetValueExternal()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        napi_status status = runtime.GetValueExternal(env, handle, out nint result);

        // The invalid arg error code is returned if there was no external value.
        if (status == napi_status.napi_invalid_arg)
        {
            return null;
        }

        status.ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    /// <summary>
    /// Gets the .NET external value or primitive object value (string, boolean, or double)
    /// for a JS value, or null if the JS value is not convertible to one of those types.
    /// </summary>
    /// <remarks>
    /// This is useful when marshalling where a JS value must be converted to some .NET type,
    /// but the target type is unknown (object).
    /// </remarks>
    public object? GetValueExternalOrPrimitive()
    {
        return TypeOf() switch
        {
            JSValueType.String => GetValueStringUtf16(),
            JSValueType.Boolean => GetValueBool(),
            JSValueType.Number => GetValueDouble(),
            JSValueType.External => GetValueExternal(),
            _ => null,
        };
    }

    public bool IsError() => GetRuntime(out napi_env env, out napi_value handle)
        .IsError(env, handle, out bool result).ThrowIfFailed(result);

    public bool IsArrayBuffer() => GetRuntime(out napi_env env, out napi_value handle)
        .IsArrayBuffer(env, handle, out bool result).ThrowIfFailed(result);

    public unsafe Span<byte> GetArrayBufferInfo()
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetArrayBufferInfo(env, handle, out nint data, out nuint length).ThrowIfFailed();
        return new Span<byte>((void*)data, (int)length);
    }

    public bool IsTypedArray() => GetRuntime(out napi_env env, out napi_value handle)
        .IsTypedArray(env, handle, out bool result).ThrowIfFailed(result);

    public unsafe int GetTypedArrayLength(out JSTypedArrayType type)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetTypedArrayInfo(
            env,
            handle,
            out napi_typedarray_type arrayType,
            out nuint length,
            out nint _,
            out napi_value _,
            out nuint _).ThrowIfFailed();
        type = (JSTypedArrayType)(int)arrayType;
        return (int)length;
    }

    public unsafe Span<T> GetTypedArrayData<T>() where T : struct
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetTypedArrayInfo(
            env,
            handle,
            out napi_typedarray_type arrayType,
            out nuint length,
            out nint data,
            out napi_value _,
            out nuint _).ThrowIfFailed();
        var type = (JSTypedArrayType)(int)arrayType;
        if (!(default(T) switch
        {
            sbyte => type == JSTypedArrayType.Int8,
            byte => type == JSTypedArrayType.UInt8 || type == JSTypedArrayType.UInt8Clamped,
            short => type == JSTypedArrayType.Int16,
            ushort => type == JSTypedArrayType.Int16,
            int => type == JSTypedArrayType.Int32,
            uint => type == JSTypedArrayType.UInt32,
            long => type == JSTypedArrayType.BigInt64,
            ulong => type == JSTypedArrayType.BigUInt64,
            float => type == JSTypedArrayType.Float32,
            double => type == JSTypedArrayType.Float64,
            _ => throw new InvalidCastException("Invalid typed-array type: " + typeof(T)),
        }))
        {
            throw new InvalidCastException(
                $"Incorrect typed-array type {typeof(T)} for {type}Array.");
        }

        return new Span<T>((void*)data, (int)length);
    }

    public unsafe void GetTypedArrayBuffer(
        out JSTypedArrayType type,
        out int length,
        out JSValue arrayBuffer,
        out int byteOffset)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetTypedArrayInfo(
            env,
            handle,
            out napi_typedarray_type type_,
            out nuint length_,
            out nint _,
            out napi_value arrayBuffer_,
            out nuint byteOffset_).ThrowIfFailed();
        type = (JSTypedArrayType)(int)type_;
        length = (int)length_;
        arrayBuffer = arrayBuffer_;
        byteOffset = (int)byteOffset_;
    }

    public bool IsDataView() => GetRuntime(out napi_env env, out napi_value handle)
        .IsDataView(env, handle, out bool result).ThrowIfFailed(result);

    public unsafe void GetDataViewInfo(
        out ReadOnlySpan<byte> viewSpan,
        out JSValue arrayBuffer,
        out int byteOffset)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetDataViewInfo(
          env,
          handle,
          out nuint byteLength,
          out nint data,
          out napi_value arrayBuffer_,
          out nuint byteOffset_).ThrowIfFailed();
        viewSpan = new ReadOnlySpan<byte>((void*)data, (int)byteLength);
        arrayBuffer = arrayBuffer_;
        byteOffset = (int)byteOffset_;
    }

    public static uint GetVersion() => GetCurrentRuntime(out napi_env env)
        .GetVersion(env, out uint result).ThrowIfFailed(result);

    public bool IsPromise() => GetRuntime(out napi_env env, out napi_value handle)
        .IsPromise(env, handle, out bool result).ThrowIfFailed(result);

    public static JSValue RunScript(JSValue script)
        => script.GetRuntime(out napi_env env, out napi_value handle)
            .RunScript(env, handle, out napi_value result).ThrowIfFailed(result);

    public bool IsDate() => GetRuntime(out napi_env env, out napi_value handle)
        .IsDate(env, handle, out bool result).ThrowIfFailed(result);

    public double GetDateValue() => GetRuntime(out napi_env env, out napi_value handle)
        .GetValueDate(env, handle, out double result).ThrowIfFailed(result);

    public unsafe void AddFinalizer(Action finalize)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        GCHandle finalizeHandle = _scope!.RuntimeContext.AllocGCHandle(finalize);
        runtime.AddFinalizer(
            env,
            handle,
            (nint)finalizeHandle,
            new napi_finalize(s_callFinalizeAction),
            _scope!.RuntimeContextHandle).ThrowIfFailed();
    }

    public unsafe void AddFinalizer(Action finalize, out JSReference finalizerRef)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        GCHandle finalizeHandle = _scope!.RuntimeContext.AllocGCHandle(finalize);
        runtime.AddFinalizer(
            env,
            handle,
            (nint)finalizeHandle,
            new napi_finalize(s_callFinalizeAction),
            _scope!.RuntimeContextHandle,
            out napi_ref reference).ThrowIfFailed();
        finalizerRef = new JSReference(reference, isWeak: true);
    }

    public long ToInt64BigInt(out bool isLossless)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetValueBigInt64(env, handle, out long result, out isLossless).ThrowIfFailed(result);

    public ulong ToUInt64BigInt(out bool isLossless)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetValueBigInt64(env, handle, out ulong result, out isLossless).ThrowIfFailed(result);

    public int GetBigIntWordCount()
        => (int)GetRuntime(out napi_env env, out napi_value handle)
            .GetBigIntWordCount(env, handle, out nuint result).ThrowIfFailed(result);

    public void GetBigIntWords(Span<ulong> destination, out int sign, out int wordCount)
    {
        GetRuntime(out napi_env env, out napi_value handle)
            .GetBigIntWords(env, handle, out sign, destination, out nuint wordCountResult)
            .ThrowIfFailed();
        wordCount = (int)wordCountResult;
    }

    public ulong[] GetBigIntWords(out int sign)
    {
        JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
        runtime.GetBigIntWordCount(env, handle, out nuint wordCount).ThrowIfFailed();
        if (wordCount > 0)
        {
            ulong[] words = new ulong[(int)wordCount];
            runtime.GetBigIntWords(env, handle, out sign, words.AsSpan(), out _).ThrowIfFailed();
            return words;
        }
        else
        {
            sign = 0;
            return [];
        }
    }

    public JSValue GetAllPropertyNames(
        JSKeyCollectionMode mode,
        JSKeyFilter filter,
        JSKeyConversion conversion)
        => GetRuntime(out napi_env env, out napi_value handle)
            .GetAllPropertyNames(
                env,
                handle,
                (napi_key_collection_mode)mode,
                (napi_key_filter)filter,
                (napi_key_conversion)conversion,
                out napi_value result).ThrowIfFailed(result);

    //TODO: (vmoroz) What env parameter does here?
    //TODO: (vmoroz) Move instance data to somewhere else. It must be not in the public API
    internal static unsafe void SetInstanceData(napi_env env, object? data)
    {
        JSRuntime runtime = CurrentRuntime;
        runtime.GetInstanceData(env, out nint handlePtr).ThrowIfFailed();
        if (handlePtr != default)
        {
            // Current napi_set_instance_data implementation does not call finalizer when we replace existing instance data.
            // It means that we only remove the GC root, but do not call Dispose.
            GCHandle.FromIntPtr(handlePtr).Free();
        }

        if (data != null)
        {
            GCHandle handle = GCHandle.Alloc(data);
            runtime.SetInstanceData(
              env,
              (nint)handle,
              new napi_finalize(s_finalizeGCHandleToDisposable),
              finalizeHint: default).ThrowIfFailed();
        }
    }

    internal static object? GetInstanceData(napi_env env)
    {
        CurrentRuntime.GetInstanceData(env, out nint data).ThrowIfFailed();
        return (data != default) ? GCHandle.FromIntPtr(data).Target : null;
    }

    public void DetachArrayBuffer() => GetRuntime(out napi_env env, out napi_value handle)
        .DetachArrayBuffer(env, handle).ThrowIfFailed();

    public bool IsDetachedArrayBuffer() => GetRuntime(out napi_env env, out napi_value handle)
        .IsDetachedArrayBuffer(env, handle, out bool result).ThrowIfFailed(result);

    public void SetObjectTypeTag(Guid typeTag)
        => GetRuntime(out napi_env env, out napi_value handle)
            .SetObjectTypeTag(env, handle, typeTag).ThrowIfFailed();

    public bool CheckObjectTypeTag(Guid typeTag)
        => GetRuntime(out napi_env env, out napi_value handle)
            .CheckObjectTypeTag(env, handle, typeTag, out bool result).ThrowIfFailed(result);

    public void Freeze() => GetRuntime(out napi_env env, out napi_value handle)
        .Freeze(env, handle).ThrowIfFailed();

    public void Seal() => GetRuntime(out napi_env env, out napi_value handle)
        .Seal(env, handle).ThrowIfFailed();

#if NETFRAMEWORK
    internal static readonly napi_callback.Delegate s_invokeJSCallback = InvokeJSCallback;
    internal static readonly napi_callback.Delegate s_invokeJSMethod = InvokeJSMethod;
    internal static readonly napi_callback.Delegate s_invokeJSGetter = InvokeJSGetter;
    internal static readonly napi_callback.Delegate s_invokeJSSetter = InvokeJSSetter;
    internal static readonly napi_callback.Delegate s_invokeJSCallbackNC = InvokeJSCallbackNoContext;
    internal static readonly napi_callback.Delegate s_invokeJSMethodNC = InvokeJSMethodNoContext;
    internal static readonly napi_callback.Delegate s_invokeJSGetterNC = InvokeJSGetterNoContext;
    internal static readonly napi_callback.Delegate s_invokeJSSetterNC = InvokeJSSetterNoContext;

    internal static readonly napi_finalize.Delegate s_finalizeGCHandle = FinalizeGCHandle;
    internal static readonly napi_finalize.Delegate s_finalizeGCHandleToDisposable = FinalizeGCHandleToDisposable;
    internal static readonly napi_finalize.Delegate s_finalizeGCHandleToPinnedMemory = FinalizeGCHandleToPinnedMemory;
    internal static readonly napi_finalize.Delegate s_callFinalizeAction = CallFinalizeAction;
#else
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSCallback = &InvokeJSCallback;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSMethod = &InvokeJSMethod;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSGetter = &InvokeJSGetter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSSetter = &InvokeJSSetter;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSCallbackNC = &InvokeJSCallbackNoContext;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSMethodNC = &InvokeJSMethodNoContext;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSGetterNC = &InvokeJSGetterNoContext;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_invokeJSSetterNC = &InvokeJSSetterNoContext;

    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_finalizeGCHandle = &FinalizeGCHandle;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_finalizeGCHandleToDisposable = &FinalizeGCHandleToDisposable;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_finalizeGCHandleToPinnedMemory = &FinalizeGCHandleToPinnedMemory;
    internal static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_callFinalizeAction = &CallFinalizeAction;
#endif

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe napi_value InvokeJSCallback(
        napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSCallbackDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (descriptor) => descriptor);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSMethod(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Method!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSGetter(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Getter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static napi_value InvokeJSSetter(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Setter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe napi_value InvokeJSCallbackNoContext(
        napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSCallbackDescriptor>(
            env, callbackInfo, JSValueScopeType.NoContext, (descriptor) => descriptor);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSMethodNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.NoContext, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Method!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSGetterNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.NoContext, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Getter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static napi_value InvokeJSSetterNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.NoContext, (propertyDescriptor) => new(
                propertyDescriptor.Name,
                propertyDescriptor.Setter!,
                propertyDescriptor.Data,
                propertyDescriptor.ModuleContext));
    }

    private static unsafe napi_value InvokeCallback<TDescriptor>(
        napi_env env,
        napi_callback_info callbackInfo,
        JSValueScopeType scopeType,
        Func<TDescriptor, JSCallbackDescriptor> getCallbackDescriptor)
    {
        using var scope = new JSValueScope(scopeType, env, runtime: default);
        try
        {
            JSCallbackArgs.GetDataAndLength(scope, callbackInfo, out object? data, out int length);
            Span<napi_value> args = stackalloc napi_value[length];
            JSCallbackDescriptor descriptor = getCallbackDescriptor((TDescriptor)data!);
            scope.ModuleContext = descriptor.ModuleContext;
            return (napi_value)descriptor.Callback(
                new JSCallbackArgs(scope, callbackInfo, args, descriptor.Data));
        }
        catch (Exception ex)
        {
            JSError.ThrowError(ex);
            return napi_value.Null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe void FinalizeGCHandle(napi_env env, nint data, nint hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        if (hint != default)
        {
            GCHandle contextHandle = GCHandle.FromIntPtr(hint);
            JSRuntimeContext context = (JSRuntimeContext)contextHandle.Target!;
            context.FreeGCHandle(handle);
        }
        else
        {
            handle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe void FinalizeGCHandleToDisposable(napi_env env, nint data, nint hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        try
        {
            (handle.Target as IDisposable)?.Dispose();
        }
        finally
        {
            if (hint != default)
            {
                GCHandle contextHandle = GCHandle.FromIntPtr(hint);
                JSRuntimeContext context = (JSRuntimeContext)contextHandle.Target!;
                context.FreeGCHandle(handle);
            }
            else
            {
                handle.Free();
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe void FinalizeGCHandleToPinnedMemory(napi_env env, nint data, nint hint)
    {
        // The GC handle is passed via the hint parameter.
        // (The data parameter is the pointer to raw memory.)
        GCHandle handle = GCHandle.FromIntPtr(hint);
        PinnedMemory pinnedMemory = (PinnedMemory)handle.Target!;
        try
        {
            pinnedMemory.Dispose();
        }
        finally
        {
            pinnedMemory.RuntimeContext.FreeGCHandle(handle);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void CallFinalizeAction(napi_env env, nint data, nint hint)
    {
        GCHandle gcHandle = GCHandle.FromIntPtr(data);
        GCHandle contextHandle = GCHandle.FromIntPtr(hint);
        JSRuntimeContext context = (JSRuntimeContext)contextHandle.Target!;
        try
        {
            // TODO: [vmoroz] In future we will be not allowed to run JS in finalizers.
            // We must remove creation of the scope.
            using var scope = new JSValueScope(JSValueScopeType.Callback);
            ((Action)gcHandle.Target!)();
        }
        finally
        {
            context.FreeGCHandle(gcHandle);
        }
    }

    internal abstract class PinnedMemory : IDisposable
    {
        private bool _disposed = false;
        private MemoryHandle _memoryHandle;

        protected PinnedMemory(MemoryHandle memoryHandle, object? owner)
        {
            _memoryHandle = memoryHandle;
            Owner = owner;
            RuntimeContext = JSRuntimeContext.Current;
        }

        public abstract int Length { get; }

        public object? Owner { get; private set; }

        public unsafe void* Pointer => _memoryHandle.Pointer;

        public JSRuntimeContext RuntimeContext { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _memoryHandle.Dispose();
                Owner = null;
            }
        }

    }

    internal sealed class PinnedMemory<T> : PinnedMemory where T : struct
    {
        private readonly Memory<T> _memory;

        public PinnedMemory(Memory<T> memory, object? owner) : base(memory.Pin(), owner)
        {
            _memory = memory;
        }

        public override int Length => _memory.Length * Unsafe.SizeOf<T>();
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
    public bool Equals(JSValue other) => StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }

    public unsafe void AddGCHandleFinalizer(nint finalizeData)
    {
        if (finalizeData != default)
        {
            JSRuntime runtime = GetRuntime(out napi_env env, out napi_value handle);
            runtime.AddFinalizer(
                env,
                handle,
                finalizeData,
                new napi_finalize(s_finalizeGCHandle),
                Scope.RuntimeContextHandle).ThrowIfFailed();
        }
    }

    private JSRuntime GetRuntime(out napi_env env)
    {
        JSValueScope scope = _scope ?? throw new JSInvalidThreadAccessException(null);
        scope.ThrowIfDisposed();
        scope.ThrowIfInvalidThreadAccess();
        env = scope.UncheckedEnvironmentHandle;
        return scope.Runtime;
    }

    private JSRuntime GetRuntime(out napi_env env, out napi_value handle)
        => GetRuntime(out env, out handle, out _);

    private JSRuntime GetRuntime(out napi_env env, out napi_value handle, out JSRuntime runtime)
    {
        if (_scope is JSValueScope scope)
        {
            scope.ThrowIfDisposed();
            scope.ThrowIfInvalidThreadAccess();
            env = scope.UncheckedEnvironmentHandle;
            handle = _handle;
            runtime = scope.Runtime;
        }
        else
        {
            scope = Current;
            env = scope.UncheckedEnvironmentHandle;
            runtime = scope.Runtime;
            runtime.GetUndefined(env, out handle).ThrowIfFailed();
        }

        return runtime;
    }

    internal static JSRuntime GetCurrentRuntime(out napi_env env)
    {
        JSValueScope scope = Current;
        env = scope.UncheckedEnvironmentHandle;
        return scope.Runtime;
    }

    private static unsafe nint[] ToUnmanagedPropertyDescriptors(
        string name,
        IReadOnlyCollection<JSPropertyDescriptor> descriptors,
        UseUnmanagedDescriptors action)
    {
        napi_callback methodCallback;
        napi_callback getterCallback;
        napi_callback setterCallback;
        if (JSValueScope.Current?.ScopeType == JSValueScopeType.NoContext)
        {
            // The NativeHost and ManagedHost set up callbacks without a current module context.
            methodCallback = new napi_callback(s_invokeJSMethodNC);
            getterCallback = new napi_callback(s_invokeJSGetterNC);
            setterCallback = new napi_callback(s_invokeJSSetterNC);
        }
        else
        {
            methodCallback = new napi_callback(s_invokeJSMethod);
            getterCallback = new napi_callback(s_invokeJSGetter);
            setterCallback = new napi_callback(s_invokeJSSetter);
        }

        nint[] handlesToFinalize = new nint[descriptors.Count];
        int count = descriptors.Count;
        Span<napi_property_descriptor> descriptorsPtr = stackalloc napi_property_descriptor[count];
        int i = 0;
        foreach (JSPropertyDescriptor descriptor in descriptors)
        {
            ref napi_property_descriptor descriptorPtr = ref descriptorsPtr[i];
            descriptorPtr.name = (napi_value)(descriptor.NameValue ?? (JSValue)descriptor.Name!);
            descriptorPtr.utf8name = default;
            descriptorPtr.method = descriptor.Method == null ? default : methodCallback;
            descriptorPtr.getter = descriptor.Getter == null ? default : getterCallback;
            descriptorPtr.setter = descriptor.Setter == null ? default : setterCallback;
            descriptorPtr.value = (napi_value)descriptor.Value;
            descriptorPtr.attributes = (napi_property_attributes)descriptor.Attributes;
            if (descriptor.Data != null ||
                descriptor.Method != null ||
                descriptor.Getter != null ||
                descriptor.Setter != null)
            {
                handlesToFinalize[i] = descriptorPtr.data = (nint)(
                    JSRuntimeContext.Current?.AllocGCHandle(descriptor) ?? GCHandle.Alloc(descriptor));
            }
            else
            {
                handlesToFinalize[i] = descriptorPtr.data = default;
            }
            i++;
        }
        action(name, descriptorsPtr);
        return handlesToFinalize;
    }

    private unsafe delegate void UseUnmanagedDescriptors(
        string name, ReadOnlySpan<napi_property_descriptor> descriptors);

    private static napi_value GetUndefined(JSRuntime runtime, napi_env env)
        => runtime.GetUndefined(env, out napi_value result).ThrowIfFailed(result);
}
