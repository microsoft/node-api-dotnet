// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

// Node API managed wrappers
public static partial class JSNativeApi
{
    /// <summary>
    /// Hint to a finalizer callback that indicates the object referenced by the handle should be
    /// disposed when finalizing.
    /// </summary>
    private const nint DisposeHint = (nint)1;

    public static unsafe void AddGCHandleFinalizer(this JSValue thisValue, nint handle)
    {
        if (handle != default)
        {
            thisValue.Runtime.AddFinalizer(
                Env,
                (napi_value)thisValue,
                handle,
                new napi_finalize(s_finalizeGCHandle),
                default,
                out _).ThrowIfFailed();
        }
    }

    public static unsafe JSValueType TypeOf(this JSValue value)
        => value.Runtime.GetValueType(Env, (napi_value)value, out napi_valuetype result)
            .ThrowIfFailed((JSValueType)result);

    public static unsafe bool IsUndefined(this JSValue value)
        => value.TypeOf() == JSValueType.Undefined;

    public static unsafe bool IsNull(this JSValue value)
        => value.TypeOf() == JSValueType.Null;

    public static unsafe bool IsNullOrUndefined(this JSValue value) => value.TypeOf() switch
    {
        JSValueType.Null => true,
        JSValueType.Undefined => true,
        _ => false,
    };

    public static unsafe bool IsBoolean(this JSValue value)
        => value.TypeOf() == JSValueType.Boolean;

    public static unsafe bool IsNumber(this JSValue value)
        => value.TypeOf() == JSValueType.Number;

    public static unsafe bool IsString(this JSValue value)
        => value.TypeOf() == JSValueType.String;

    public static unsafe bool IsSymbol(this JSValue value)
        => value.TypeOf() == JSValueType.Symbol;

    public static unsafe bool IsObject(this JSValue value)
    {
        JSValueType valueType = value.TypeOf();
        return (valueType == JSValueType.Object) || (valueType == JSValueType.Function);
    }

    public static unsafe bool IsFunction(this JSValue value)
        => value.TypeOf() == JSValueType.Function;

    public static unsafe bool IsExternal(this JSValue value)
        => value.TypeOf() == JSValueType.External;

    public static double GetValueDouble(this JSValue value)
        => value.Runtime.GetValueDouble(Env, (napi_value)value, out double result)
            .ThrowIfFailed(result);

    public static int GetValueInt32(this JSValue value)
        => value.Runtime.GetValueInt32(Env, (napi_value)value, out int result)
            .ThrowIfFailed(result);

    public static uint GetValueUInt32(this JSValue value)
        => value.Runtime.GetValueUInt32(Env, (napi_value)value, out uint result)
        .ThrowIfFailed(result);

    public static long GetValueInt64(this JSValue value)
        => value.Runtime.GetValueInt64(Env, (napi_value)value, out long result)
        .ThrowIfFailed(result);

    public static bool GetValueBool(this JSValue value)
        => value.Runtime.GetValueBool(Env, (napi_value)value, out bool result)
            .ThrowIfFailed(result);

    public static unsafe int GetValueStringUtf8(this JSValue thisValue, Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return thisValue.Runtime.GetValueStringUtf8(
                Env, (napi_value)thisValue, Span<byte>.Empty, out int result)
                .ThrowIfFailed(result);
        }

        return thisValue.Runtime.GetValueStringUtf8(
            Env, (napi_value)thisValue, buffer, out int result2)
            .ThrowIfFailed(result2);
    }

    public static byte[] GetValueStringUtf8(this JSValue value)
    {
        int length = GetValueStringUtf8(value, Span<byte>.Empty);
        byte[] result = new byte[length + 1];
        GetValueStringUtf8(value, new Span<byte>(result));
        // Remove the zero terminating character
        Array.Resize(ref result, length);
        return result;
    }

    public static unsafe int GetValueStringUtf16(this JSValue thisValue, Span<char> buffer)
    {
        if (buffer.IsEmpty)
        {
            return thisValue.Runtime.GetValueStringUtf16(
                Env, (napi_value)thisValue, Span<char>.Empty, out int result)
                .ThrowIfFailed(result);
        }

        return thisValue.Runtime.GetValueStringUtf16(
            Env, (napi_value)thisValue, buffer, out int result2)
            .ThrowIfFailed(result2);
    }

    public static char[] GetValueStringUtf16AsCharArray(this JSValue value)
    {
        int length = GetValueStringUtf16(value, Span<char>.Empty);
        char[] result = new char[length + 1];
        GetValueStringUtf16(value, new Span<char>(result));
        // Remove the zero terminating character
        Array.Resize(ref result, length);
        return result;
    }

    public static string GetValueStringUtf16(this JSValue value)
        => new(GetValueStringUtf16AsCharArray(value));

    public static JSValue CoerceToBoolean(this JSValue value)
        => value.Runtime.CoerceToBool(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CoerceToNumber(this JSValue value)
        => value.Runtime.CoerceToNumber(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CoerceToObject(this JSValue value)
        => value.Runtime.CoerceToObject(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue CoerceToString(this JSValue value)
        => value.Runtime.CoerceToString(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue GetPrototype(this JSValue value)
        => value.Runtime.GetPrototype(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static JSValue GetPropertyNames(this JSValue value)
        => value.Runtime.GetPropertyNames(Env, (napi_value)value, out napi_value result)
            .ThrowIfFailed(result);

    public static void SetProperty(this JSValue thisValue, JSValue key, JSValue value)
    {
        thisValue.Runtime.SetProperty(Env, (napi_value)thisValue, (napi_value)key, (napi_value)value)
            .ThrowIfFailed();
    }

    public static bool HasProperty(this JSValue thisValue, JSValue key)
        => thisValue.Runtime.HasProperty(Env, (napi_value)thisValue, (napi_value)key, out bool result)
            .ThrowIfFailed(result);

    public static JSValue GetProperty(this JSValue thisValue, JSValue key)
        => thisValue.Runtime.GetProperty(Env, (napi_value)thisValue, (napi_value)key, out napi_value result)
            .ThrowIfFailed(result);

    public static bool DeleteProperty(this JSValue thisValue, JSValue key)
        => thisValue.Runtime.DeleteProperty(Env, (napi_value)thisValue, (napi_value)key, out bool result)
            .ThrowIfFailed(result);

    public static bool HasOwnProperty(this JSValue thisValue, JSValue key)
        => thisValue.Runtime.HasOwnProperty(Env, (napi_value)thisValue, (napi_value)key, out bool result)
            .ThrowIfFailed(result);

    public static void SetElement(this JSValue thisValue, int index, JSValue value)
    {
        thisValue.Runtime.SetElement(Env, (napi_value)thisValue, (uint)index, (napi_value)value)
            .ThrowIfFailed();
    }

    public static bool HasElement(this JSValue thisValue, int index)
        => thisValue.Runtime.HasElement(Env, (napi_value)thisValue, (uint)index, out bool result)
            .ThrowIfFailed(result);

    public static JSValue GetElement(this JSValue thisValue, int index)
        => thisValue.Runtime.GetElement(Env, (napi_value)thisValue, (uint)index, out napi_value result)
            .ThrowIfFailed(result);

    public static bool DeleteElement(this JSValue thisValue, int index)
        => thisValue.Runtime.DeleteElement(Env, (napi_value)thisValue, (uint)index, out bool result)
            .ThrowIfFailed(result);

    public static unsafe void DefineProperties(this JSValue thisValue, IReadOnlyCollection<JSPropertyDescriptor> descriptors)
    {
        nint[] handles = ToUnmanagedPropertyDescriptors(string.Empty, descriptors, (_, descriptorsPtr) =>
            thisValue.Runtime.DefineProperties(Env, (napi_value)thisValue, descriptorsPtr)
                .ThrowIfFailed());
        Array.ForEach(handles, handle => thisValue.AddGCHandleFinalizer(handle));
    }

    public static unsafe void DefineProperties(this JSValue thisValue, params JSPropertyDescriptor[] descriptors)
    {
        nint[] handles = ToUnmanagedPropertyDescriptors(string.Empty, descriptors, (_, descriptorsPtr) =>
            thisValue.Runtime.DefineProperties(Env, (napi_value)thisValue, descriptorsPtr)
                .ThrowIfFailed());
        Array.ForEach(handles, handle => thisValue.AddGCHandleFinalizer(handle));
    }

    public static bool IsArray(this JSValue thisValue)
        => thisValue.Runtime.IsArray(Env, (napi_value)thisValue, out bool result)
            .ThrowIfFailed(result);

    public static int GetArrayLength(this JSValue thisValue)
        => thisValue.Runtime.GetArrayLength(Env, (napi_value)thisValue, out int result)
            .ThrowIfFailed(result);

    // Internal because JSValue structs all implement IEquatable<JSValue>, which calls this method.
    internal static bool StrictEquals(this JSValue thisValue, JSValue other)
        => thisValue.Runtime.StrictEquals(Env, (napi_value)thisValue, (napi_value)other, out bool result)
            .ThrowIfFailed(result);

    public static unsafe JSValue Call(this JSValue thisValue)
        => thisValue.Runtime.CallFunction(
            Env, (napi_value)JSValue.Undefined, (napi_value)thisValue, Array.Empty<napi_value>(), out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue Call(this JSValue thisValue, JSValue thisArg)
        => thisValue.Runtime.CallFunction(Env, (napi_value)thisArg, (napi_value)thisValue, Array.Empty<napi_value>(), out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue Call(this JSValue thisValue, JSValue thisArg, JSValue arg0)
    {
        Span<napi_value> args = stackalloc napi_value[] { (napi_value)arg0 };
        return thisValue.Runtime.CallFunction(
            Env, (napi_value)thisArg, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, JSValue arg0, JSValue arg1)
    {
        Span<napi_value> args = stackalloc napi_value[] { (napi_value)arg0, (napi_value)arg1 };
        return thisValue.Runtime.CallFunction(
            Env, (napi_value)thisArg, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        Span<napi_value> args = stackalloc napi_value[]
        {
            (napi_value)arg0,
            (napi_value)arg1,
            (napi_value)arg2
        };
        return thisValue.Runtime.CallFunction(
            Env, (napi_value)thisArg, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, params JSValue[] args)
        => Call(thisValue, thisArg, new ReadOnlySpan<JSValue>(args));

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        Span<napi_value> argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = (napi_value)args[i];
        }

        return thisValue.Runtime.CallFunction(
            Env,
            (napi_value)thisArg,
            (napi_value)thisValue,
            argv,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, napi_value thisArg, ReadOnlySpan<napi_value> args)
    {
        return thisValue.Runtime.CallFunction(
            Env,
            thisArg,
            (napi_value)thisValue,
            args,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue)
        => thisValue.Runtime.NewInstance(
            Env, (napi_value)thisValue, Span<napi_value>.Empty, out napi_value result)
                .ThrowIfFailed(result);

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue, JSValue arg0)
    {
        napi_value argValue0 = (napi_value)arg0;
        Span<napi_value> args = stackalloc napi_value[1] { argValue0 };
        return thisValue.Runtime.NewInstance(Env, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, JSValue arg0, JSValue arg1)
    {
        Span<napi_value> args = stackalloc napi_value[2] { (napi_value)arg0, (napi_value)arg1 };
        return thisValue.Runtime.NewInstance(Env, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        Span<napi_value> args = stackalloc napi_value[3] {
            (napi_value)arg0,
            (napi_value)arg1,
            (napi_value)arg2
        };
        return thisValue.Runtime.NewInstance(Env, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue, params JSValue[] args)
        => CallAsConstructor(thisValue, new ReadOnlySpan<JSValue>(args));

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        Span<napi_value> argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = (napi_value)args[i];
        }

        return thisValue.Runtime.NewInstance(
            Env, (napi_value)thisValue, argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, ReadOnlySpan<napi_value> args)
    {
        return thisValue.Runtime.NewInstance(
            Env, (napi_value)thisValue, args, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static JSValue CallMethod(this JSValue thisValue, JSValue methodName)
        => thisValue.GetProperty(methodName).Call(thisValue);

    public static JSValue CallMethod(this JSValue thisValue, JSValue methodName, JSValue arg0)
        => thisValue.GetProperty(methodName).Call(thisValue, arg0);

    public static JSValue CallMethod(
        this JSValue thisValue, JSValue methodName, JSValue arg0, JSValue arg1)
        => thisValue.GetProperty(methodName).Call(thisValue, arg0, arg1);

    public static JSValue CallMethod(
        this JSValue thisValue, JSValue methodName, JSValue arg0, JSValue arg1, JSValue arg2)
        => thisValue.GetProperty(methodName).Call(thisValue, arg0, arg1, arg2);

    public static JSValue CallMethod(
        this JSValue thisValue, JSValue methodName, params JSValue[] args)
        => thisValue.GetProperty(methodName).Call(thisValue, args);

    public static JSValue CallMethod(
        this JSValue thisValue, JSValue methodName, ReadOnlySpan<JSValue> args)
        => thisValue.GetProperty(methodName).Call(thisValue, args);

    public static JSValue CallMethod(
        this JSValue thisValue, JSValue methodName, ReadOnlySpan<napi_value> args)
        => thisValue.GetProperty(methodName).Call((napi_value)thisValue, args);

    public static bool InstanceOf(this JSValue thisValue, JSValue constructor)
        => thisValue.Runtime.InstanceOf(Env, (napi_value)thisValue, (napi_value)constructor, out bool result)
            .ThrowIfFailed(result);

    public static unsafe JSValue DefineClass(
        string name,
        napi_callback callback,
        nint data,
        ReadOnlySpan<napi_property_descriptor> descriptors)
    {
        return JSValueScope.CurrentRuntime.DefineClass(
            Env,
            name,
            callback,
            data,
            descriptors,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue DefineClass(
        string name,
        JSCallbackDescriptor constructorDescriptor,
        params JSPropertyDescriptor[] propertyDescriptors)
    {
        GCHandle descriptorHandle = JSRuntimeContext.Current.AllocGCHandle(constructorDescriptor);
        JSValue? func = null;
        napi_callback callback = new(
            JSValueScope.Current?.ScopeType == JSValueScopeType.NoContext
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
    /// Attaches an object to a JS wrapper.
    /// </summary>
    /// <param name="wrapper">The JS wrapper value, typically the 'this' argument to a class
    /// constructor callback.</param>
    /// <param name="value">The object to be wrapped.</param>
    /// <returns>The JS wrapper.</returns>
    public static unsafe JSValue Wrap(this JSValue wrapper, object value)
    {
        GCHandle valueHandle = JSRuntimeContext.Current.AllocGCHandle(value);
        wrapper.Runtime.Wrap(
            Env,
            (napi_value)wrapper,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            default,
            out _).ThrowIfFailed();
        return wrapper;
    }

    /// <summary>
    /// Attaches an object to a JS wrapper.
    /// </summary>
    /// <param name="wrapper">The JS wrapper value, typically the 'this' argument to a class
    /// constructor callback.</param>
    /// <param name="value">The object to be wrapped.</param>
    /// <param name="wrapperWeakRef">Returns a weak reference to the JS wrapper.</param>
    /// <returns>The JS wrapper.</returns>
    public static unsafe JSValue Wrap(
        this JSValue wrapper, object value, out JSReference wrapperWeakRef)
    {
        GCHandle valueHandle = JSRuntimeContext.Current.AllocGCHandle(value);
        wrapper.Runtime.Wrap(
            Env,
            (napi_value)wrapper,
            (nint)valueHandle,
            new napi_finalize(s_finalizeGCHandle),
            default,
            out napi_ref weakRef).ThrowIfFailed();
        wrapperWeakRef = new JSReference(weakRef, isWeak: true);
        return wrapper;
    }

    /// <summary>
    /// Attempts to get the object that was previously attached to a JS wrapper.
    /// </summary>
    /// <param name="thisValue">The JS wrapper value.</param>
    /// <param name="value">Returns the wrapped object, or null if nothing was wrapped.</param>
    /// <returns>True if a wrapped object was found and returned, else false.</returns>
    public static bool TryUnwrap(this JSValue thisValue, out object? value)
    {
        napi_status status = thisValue.Runtime.Unwrap(Env, (napi_value)thisValue, out nint result);

        // The invalid arg error code is returned if there was nothing to unwrap. It doesn't
        // distinguish from an invalid handle, but either way the unwrap failed.
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
    /// Attempts to get the object that was previously attached to a JS wrapper.
    /// </summary>
    /// <param name="thisValue">The JS wrapper value.</param>
    /// <returns>The unwrapped object, or null if nothing was wrapped.</returns>
    public static object? TryUnwrap(this JSValue thisValue)
    {
        napi_status status = thisValue.Runtime.Unwrap(Env, (napi_value)thisValue, out nint result);

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
    /// Gets the object that was previously attached to a JS wrapper.
    /// (Throws an exception if unwrapping failed.)
    /// </summary>
    public static object Unwrap(this JSValue thisValue, string? unwrapType = null)
    {
        napi_status status = thisValue.Runtime.Unwrap(Env, (napi_value)thisValue, out nint result);

        if (status == napi_status.napi_invalid_arg && unwrapType != null)
        {
            throw new JSException(new JSError($"Failed to unwrap object of type '{unwrapType}'"));
        }

        status.ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    /// <summary>
    /// Detaches an object from a JS wrapper.
    /// </summary>
    /// <param name="thisValue">The JS wrapper value.</param>
    /// <param name="value">Returns the wrapped object, or null if nothing was wrapped.</param>
    /// <returns>True if a wrapped object was found and removed, else false.</returns>
    public static bool RemoveWrap(this JSValue thisValue, out object? value)
    {
        napi_status status = thisValue.Runtime.RemoveWrap(Env, (napi_value)thisValue, out nint result);

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
    public static unsafe object GetValueExternal(this JSValue thisValue)
    {
        thisValue.Runtime.GetValueExternal(Env, (napi_value)thisValue, out nint result)
            .ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    /// <summary>
    /// Gets the object that is represented as an external value, or null if the JS value
    /// is not an external value.
    /// </summary>
    public static unsafe object? TryGetValueExternal(this JSValue thisValue)
    {
        napi_status status = thisValue.Runtime.GetValueExternal(
            Env, (napi_value)thisValue, out nint result);

        // The invalid arg error code is returned if there was no external value.
        if (status == napi_status.napi_invalid_arg)
        {
            return null;
        }

        status.ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    public static JSReference CreateReference(this JSValue thisValue)
        => new(thisValue);

    public static JSReference CreateWeakReference(this JSValue thisValue)
        => new(thisValue, isWeak: true);

    public static bool IsError(this JSValue thisValue)
        => thisValue.Runtime.IsError(
            Env, (napi_value)thisValue, out bool result).ThrowIfFailed(result);

    public static bool IsExceptionPending()
        => JSValueScope.CurrentRuntime.IsExceptionPending(Env, out bool result).ThrowIfFailed(result);

    public static JSValue GetAndClearLastException()
        => JSValueScope.CurrentRuntime.GetAndClearLastException(Env, out napi_value result).ThrowIfFailed(result);

    public static bool IsArrayBuffer(this JSValue thisValue)
        => thisValue.Runtime.IsArrayBuffer(
            Env, (napi_value)thisValue, out bool result).ThrowIfFailed(result);

    public static unsafe Span<byte> GetArrayBufferInfo(this JSValue thisValue)
    {
        thisValue.Runtime.GetArrayBufferInfo(Env, (napi_value)thisValue, out nint data, out nuint length)
            .ThrowIfFailed();
        return new Span<byte>((void*)data, (int)length);
    }

    public static bool IsTypedArray(this JSValue thisValue)
        => thisValue.Runtime.IsTypedArray(
            Env, (napi_value)thisValue, out bool result).ThrowIfFailed(result);

    public static unsafe int GetTypedArrayLength(
        this JSValue thisValue,
        out JSTypedArrayType type)
    {
        thisValue.Runtime.GetTypedArrayInfo(
            Env,
            (napi_value)thisValue,
            out napi_typedarray_type arrayType,
            out nuint length,
            out nint _,
            out napi_value _,
            out nuint _).ThrowIfFailed();
        type = (JSTypedArrayType)(int)arrayType;
        return (int)length;
    }

    public static unsafe Span<T> GetTypedArrayData<T>(
        this JSValue thisValue) where T : struct
    {
        thisValue.Runtime.GetTypedArrayInfo(
            Env,
            (napi_value)thisValue,
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

    public static unsafe void GetTypedArrayBuffer(
        this JSValue thisValue,
        out JSTypedArrayType type,
        out int length,
        out JSValue arrayBuffer,
        out int byteOffset)
    {
        thisValue.Runtime.GetTypedArrayInfo(
            Env,
            (napi_value)thisValue,
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

    public static bool IsDataView(this JSValue thisValue)
        => thisValue.Runtime.IsDataView(Env, (napi_value)thisValue, out bool result)
            .ThrowIfFailed(result);

    public static unsafe void GetDataViewInfo(
        this JSValue thisValue,
        out ReadOnlySpan<byte> viewSpan,
        out JSValue arrayBuffer,
        out int byteOffset)
    {
        thisValue.Runtime.GetDataViewInfo(
          Env,
          (napi_value)thisValue,
          out nuint byteLength,
          out nint data,
          out napi_value arrayBuffer_,
          out nuint byteOffset_).ThrowIfFailed();
        viewSpan = new ReadOnlySpan<byte>((void*)data, (int)byteLength);
        arrayBuffer = arrayBuffer_;
        byteOffset = (int)byteOffset_;
    }

    public static uint GetVersion()
        => JSValueScope.CurrentRuntime.GetVersion(Env, out uint result).ThrowIfFailed(result);

    public static bool IsPromise(this JSValue thisValue)
        => thisValue.Runtime.IsPromise(Env, (napi_value)thisValue, out bool result)
            .ThrowIfFailed(result);

    public static JSValue RunScript(this JSValue thisValue)
        => thisValue.Runtime.RunScript(Env, (napi_value)thisValue, out napi_value result)
            .ThrowIfFailed(result);

    public static bool IsDate(this JSValue thisValue)
        => thisValue.Runtime.IsDate(Env, (napi_value)thisValue, out bool result)
            .ThrowIfFailed(result);

    public static double GetDateValue(this JSValue thisValue)
        => thisValue.Runtime.GetValueDate(Env, (napi_value)thisValue, out double result)
            .ThrowIfFailed(result);

    public static unsafe void AddFinalizer(this JSValue thisValue, Action finalize)
    {
        GCHandle finalizeHandle = JSRuntimeContext.Current.AllocGCHandle(finalize);
        thisValue.Runtime.AddFinalizer(
            Env,
            (napi_value)thisValue,
            (nint)finalizeHandle,
            new napi_finalize(s_callFinalizeAction),
            default,
            out _).ThrowIfFailed();
    }

    public static unsafe void AddFinalizer(
        this JSValue thisValue, Action finalize, out JSReference finalizerRef)
    {
        GCHandle finalizeHandle = JSRuntimeContext.Current.AllocGCHandle(finalize);
        napi_ref reference;
        thisValue.Runtime.AddFinalizer(
            Env,
            (napi_value)thisValue,
            (nint)finalizeHandle,
            new napi_finalize(s_callFinalizeAction),
            default,
            out reference).ThrowIfFailed();
        finalizerRef = new JSReference(reference, isWeak: true);
    }

    public static long GetValueBigIntInt64(this JSValue thisValue, out bool isLossless)
    {
        thisValue.Runtime.GetValueBigInt64(
            Env, (napi_value)thisValue, out long result, out bool lossless).ThrowIfFailed();
        isLossless = lossless;
        return result;
    }

    public static ulong GetValueBigIntUInt64(this JSValue thisValue, out bool isLossless)
    {
        thisValue.Runtime.GetValueBigInt64(
            Env, (napi_value)thisValue, out ulong result, out bool lossless).ThrowIfFailed();
        isLossless = lossless;
        return result;
    }

    public static unsafe ulong[] GetValueBigIntWords(this JSValue thisValue, out int signBit)
    {
        thisValue.Runtime.GetValueBigInt(
            Env, (napi_value)thisValue, out signBit, Span<ulong>.Empty, out nuint wordCount)
                .ThrowIfFailed();
        ulong[] words = new ulong[wordCount];
        thisValue.Runtime.GetValueBigInt(
            Env, (napi_value)thisValue, out signBit, words.AsSpan(), out _)
                .ThrowIfFailed();
        return words;
    }

    public static JSValue GetAllPropertyNames(
        this JSValue thisValue,
        JSKeyCollectionMode mode,
        JSKeyFilter filter,
        JSKeyConversion conversion)
    {
        return thisValue.Runtime.GetAllPropertyNames(
          Env,
          (napi_value)thisValue,
          (napi_key_collection_mode)mode,
          (napi_key_filter)filter,
          (napi_key_conversion)conversion,
          out napi_value result).ThrowIfFailed(result);
    }

    internal static unsafe void SetInstanceData(napi_env env, object? data)
    {
        JSValueScope.CurrentRuntime.GetInstanceData(env, out nint handlePtr).ThrowIfFailed();
        if (handlePtr != default)
        {
            // Current napi_set_instance_data implementation does not call finalizer when we replace existing instance data.
            // It means that we only remove the GC root, but do not call Dispose.
            GCHandle.FromIntPtr(handlePtr).Free();
        }

        if (data != null)
        {
            GCHandle handle = GCHandle.Alloc(data);
            JSValueScope.CurrentRuntime.SetInstanceData(
              env,
              (nint)handle,
              new napi_finalize(s_finalizeGCHandle),
              DisposeHint).ThrowIfFailed();
        }
    }

    internal static object? GetInstanceData(napi_env env)
    {
        JSValueScope.CurrentRuntime.GetInstanceData(env, out nint data).ThrowIfFailed();
        return (data != default) ? GCHandle.FromIntPtr(data).Target : null;
    }

    public static void DetachArrayBuffer(this JSValue thisValue)
        => thisValue.Runtime.DetachArrayBuffer(Env, (napi_value)thisValue).ThrowIfFailed();

    public static bool IsDetachedArrayBuffer(this JSValue thisValue)
        => thisValue.Runtime.IsDetachedArrayBuffer(Env, (napi_value)thisValue, out bool result)
            .ThrowIfFailed(result);

    public static void SetObjectTypeTag(this JSValue thisValue, Guid typeTag)
        => thisValue.Runtime.SetObjectTypeTag(Env, (napi_value)thisValue, typeTag)
            .ThrowIfFailed();

    public static bool CheckObjectTypeTag(this JSValue thisValue, Guid typeTag)
        => thisValue.Runtime.CheckObjectTypeTag(Env, (napi_value)thisValue, typeTag, out bool result)
            .ThrowIfFailed(result);

    public static void Freeze(this JSValue thisValue)
        => thisValue.Runtime.Freeze(Env, (napi_value)thisValue).ThrowIfFailed();

    public static void Seal(this JSValue thisValue)
        => thisValue.Runtime.Seal(Env, (napi_value)thisValue).ThrowIfFailed();

    private static napi_env Env => (napi_env)JSValueScope.Current;

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
    internal static readonly napi_finalize.Delegate s_finalizeHintHandle = FinalizeHintHandle;
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
        <napi_env, nint, nint, void> s_finalizeHintHandle = &FinalizeHintHandle;
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
        using var scope = new JSValueScope(scopeType);
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

        if (hint == DisposeHint)
        {
            (handle.Target as IDisposable)?.Dispose();
        }

        JSRuntimeContext.FreeGCHandle(handle, env);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe void FinalizeHintHandle(napi_env env, nint _2, nint hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(hint);
        (handle.Target as IDisposable)?.Dispose();
        JSRuntimeContext.FreeGCHandle(handle, env);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void CallFinalizeAction(napi_env env, nint data, nint hint)
    {
        GCHandle gcHandle = GCHandle.FromIntPtr(data);
        try
        {
            // TODO: [vmoroz] In future we will be not allowed to run JS in finalizers.
            // We must remove creation of the scope.
            using var scope = new JSValueScope(JSValueScopeType.Callback);
            ((Action)gcHandle.Target!)();
        }
        finally
        {
            JSRuntimeContext.FreeGCHandle(gcHandle, env);
        }
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

    internal sealed class PinnedMemory<T> : IDisposable where T : struct
    {
        private bool _disposed = false;
        private readonly Memory<T> _memory;
        private MemoryHandle _memoryHandle;

        public object? Owner { get; private set; }

        public PinnedMemory(Memory<T> memory, object? owner)
        {
            Owner = owner;
            _memory = memory;
            _memoryHandle = _memory.Pin();
        }

        public unsafe void* Pointer => _memoryHandle.Pointer;

        public int Length => _memory.Length * Unsafe.SizeOf<T>();

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
}
