using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

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
        if (handle != nint.Zero)
        {
            napi_add_finalizer(Env, (napi_value)thisValue, handle, new napi_finalize(&FinalizeGCHandle), nint.Zero, null).ThrowIfFailed();
        }
    }

    public static unsafe JSValueType TypeOf(this JSValue value)
        => napi_typeof(Env, (napi_value)value, out napi_valuetype result).ThrowIfFailed((JSValueType)result);

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
        => napi_get_value_double(Env, (napi_value)value, out double result).ThrowIfFailed(result);

    public static int GetValueInt32(this JSValue value)
        => napi_get_value_int32(Env, (napi_value)value, out int result).ThrowIfFailed(result);

    public static uint GetValueUInt32(this JSValue value)
        => napi_get_value_uint32(Env, (napi_value)value, out uint result).ThrowIfFailed(result);

    public static long GetValueInt64(this JSValue value)
        => napi_get_value_int64(Env, (napi_value)value, out long result).ThrowIfFailed(result);

    public static bool GetValueBool(this JSValue value)
        => napi_get_value_bool(Env, (napi_value)value, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe int GetValueStringLatin1(this JSValue thisValue, Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return napi_get_value_string_latin1(
                Env, (napi_value)thisValue, nint.Zero, 0, out nuint result)
                .ThrowIfFailed((int)result);
        }

        fixed (byte* ptr = &buffer[0])
        {
            return napi_get_value_string_latin1(
                Env, (napi_value)thisValue, (nint)ptr, (nuint)buffer.Length, out nuint result)
                .ThrowIfFailed((int)result);
        }
    }

    public static byte[] GetValueStringLatin1(this JSValue value)
    {
        int length = GetValueStringLatin1(value, Span<byte>.Empty);
        byte[] result = new byte[length + 1];
        GetValueStringLatin1(value, new Span<byte>(result));
        // Remove the zero terminating character
        Array.Resize(ref result, length);
        return result;
    }

    public static unsafe int GetValueStringUtf8(this JSValue thisValue, Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return napi_get_value_string_utf8(
                Env, (napi_value)thisValue, nint.Zero, 0, out nuint result)
                .ThrowIfFailed((int)result);
        }

        fixed (byte* ptr = &buffer[0])
        {
            return napi_get_value_string_utf8(
                Env, (napi_value)thisValue, (nint)ptr, (nuint)buffer.Length, out nuint result)
                .ThrowIfFailed((int)result);
        }
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
            return napi_get_value_string_utf16(
                Env, (napi_value)thisValue, nint.Zero, 0, out nuint result)
                .ThrowIfFailed((int)result);
        }

        fixed (char* ptr = &buffer[0])
        {
            return napi_get_value_string_utf16(
                Env, (napi_value)thisValue, (nint)ptr, (nuint)buffer.Length, out nuint result)
                .ThrowIfFailed((int)result);
        }
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
        => napi_coerce_to_bool(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CoerceToNumber(this JSValue value)
        => napi_coerce_to_number(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CoerceToObject(this JSValue value)
        => napi_coerce_to_object(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static JSValue CoerceToString(this JSValue value)
        => napi_coerce_to_string(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static JSValue GetPrototype(this JSValue value)
        => napi_get_prototype(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static JSValue GetPropertyNames(this JSValue value)
        => napi_get_property_names(Env, (napi_value)value, out napi_value result).ThrowIfFailed(result);

    public static void SetProperty(this JSValue thisValue, JSValue key, JSValue value)
    {
        napi_set_property(Env, (napi_value)thisValue, (napi_value)key, (napi_value)value).ThrowIfFailed();
    }

    public static bool HasProperty(this JSValue thisValue, JSValue key)
        => napi_has_property(Env, (napi_value)thisValue, (napi_value)key, out c_bool result).ThrowIfFailed((bool)result);

    public static JSValue GetProperty(this JSValue thisValue, JSValue key)
        => napi_get_property(Env, (napi_value)thisValue, (napi_value)key, out napi_value result).ThrowIfFailed(result);

    public static bool DeleteProperty(this JSValue thisValue, JSValue key)
        => napi_delete_property(Env, (napi_value)thisValue, (napi_value)key, out c_bool result).ThrowIfFailed((bool)result);

    public static bool HasOwnProperty(this JSValue thisValue, JSValue key)
        => napi_has_own_property(Env, (napi_value)thisValue, (napi_value)key, out c_bool result).ThrowIfFailed((bool)result);

    public static void SetElement(this JSValue thisValue, int index, JSValue value)
    {
        napi_set_element(Env, (napi_value)thisValue, (uint)index, (napi_value)value).ThrowIfFailed();
    }

    public static bool HasElement(this JSValue thisValue, int index)
        => napi_has_element(Env, (napi_value)thisValue, (uint)index, out c_bool result).ThrowIfFailed((bool)result);

    public static JSValue GetElement(this JSValue thisValue, int index)
        => napi_get_element(Env, (napi_value)thisValue, (uint)index, out napi_value result).ThrowIfFailed(result);

    public static bool DeleteElement(this JSValue thisValue, int index)
        => napi_delete_element(Env, (napi_value)thisValue, (uint)index, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe void DefineProperties(this JSValue thisValue, IReadOnlyCollection<JSPropertyDescriptor> descriptors)
    {
        nint[] handles = ToUnmanagedPropertyDescriptors(ReadOnlySpan<byte>.Empty, descriptors, (_, count, descriptorsPtr) =>
          napi_define_properties(Env, (napi_value)thisValue, count, (nint)descriptorsPtr).ThrowIfFailed());
        Array.ForEach(handles, handle => thisValue.AddGCHandleFinalizer(handle));
    }

    public static unsafe void DefineProperties(this JSValue thisValue, params JSPropertyDescriptor[] descriptors)
    {
        nint[] handles = ToUnmanagedPropertyDescriptors(ReadOnlySpan<byte>.Empty, descriptors, (_, count, descriptorsPtr) =>
          napi_define_properties(Env, (napi_value)thisValue, count, (nint)descriptorsPtr).ThrowIfFailed());
        Array.ForEach(handles, handle => thisValue.AddGCHandleFinalizer(handle));
    }

    public static bool IsArray(this JSValue thisValue)
        => napi_is_array(Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static int GetArrayLength(this JSValue thisValue)
        => napi_get_array_length(Env, (napi_value)thisValue, out uint result).ThrowIfFailed((int)result);

    // Internal because JSValue structs all implement IEquatable<JSValue>, which calls this method.
    internal static bool StrictEquals(this JSValue thisValue, JSValue other)
        => napi_strict_equals(Env, (napi_value)thisValue, (napi_value)other, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe JSValue Call(this JSValue thisValue)
        => napi_call_function(Env, (napi_value)JSValue.Undefined, (napi_value)thisValue, 0, nint.Zero, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue Call(this JSValue thisValue, JSValue thisArg)
        => napi_call_function(Env, (napi_value)thisArg, (napi_value)thisValue, 0, nint.Zero, out napi_value result).ThrowIfFailed(result);

    public static unsafe JSValue Call(this JSValue thisValue, JSValue thisArg, JSValue arg0)
    {
        napi_value argValue0 = (napi_value)arg0;
        napi_value* argv = &argValue0;
        return napi_call_function(
            Env, (napi_value)thisArg, (napi_value)thisValue, 1, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, JSValue arg0, JSValue arg1)
    {
        napi_value* argv = stackalloc napi_value[2] { (napi_value)arg0, (napi_value)arg1 };
        return napi_call_function(
            Env, (napi_value)thisArg, (napi_value)thisValue, 2, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        napi_value* argv = stackalloc napi_value[3] {
            (napi_value)arg0,
            (napi_value)arg1,
            (napi_value)arg2
        };
        return napi_call_function(
            Env, (napi_value)thisArg, (napi_value)thisValue, 3, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, params JSValue[] args)
        => Call(thisValue, thisArg, new ReadOnlySpan<JSValue>(args));

    public static unsafe JSValue Call(
        this JSValue thisValue, JSValue thisArg, ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        napi_value* argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = (napi_value)args[i];
        }

        return napi_call_function(
            Env,
            (napi_value)thisArg,
            (napi_value)thisValue,
            (nuint)argc,
            (nint)argv,
            out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue Call(
        this JSValue thisValue, napi_value thisArg, ReadOnlySpan<napi_value> args)
    {
        fixed (napi_value* argv = args)
        {
            return napi_call_function(
                Env,
                thisArg,
                (napi_value)thisValue,
                (nuint)args.Length,
                (nint)argv,
                out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue)
        => napi_new_instance(Env, (napi_value)thisValue, 0, nint.Zero, out napi_value result)
            .ThrowIfFailed(result);

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue, JSValue arg0)
    {
        napi_value argValue0 = (napi_value)arg0;
        napi_value* argv = &argValue0;
        return napi_new_instance(Env, (napi_value)thisValue, 1, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, JSValue arg0, JSValue arg1)
    {
        napi_value* argv = stackalloc napi_value[2] { (napi_value)arg0, (napi_value)arg1 };
        return napi_new_instance(Env, (napi_value)thisValue, 2, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        napi_value* argv = stackalloc napi_value[3] {
            (napi_value)arg0,
            (napi_value)arg1,
            (napi_value)arg2
        };
        return napi_new_instance(Env, (napi_value)thisValue, 3, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(this JSValue thisValue, params JSValue[] args)
        => CallAsConstructor(thisValue, new ReadOnlySpan<JSValue>(args));

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, ReadOnlySpan<JSValue> args)
    {
        int argc = args.Length;
        napi_value* argv = stackalloc napi_value[argc];
        for (int i = 0; i < argc; ++i)
        {
            argv[i] = (napi_value)args[i];
        }

        return napi_new_instance(
            Env, (napi_value)thisValue, (nuint)argc, (nint)argv, out napi_value result)
            .ThrowIfFailed(result);
    }

    public static unsafe JSValue CallAsConstructor(
        this JSValue thisValue, ReadOnlySpan<napi_value> args)
    {
        fixed (napi_value* argv = args)
        {
            return napi_new_instance(
                Env, (napi_value)thisValue, (nuint)args.Length, (nint)argv, out napi_value result)
                .ThrowIfFailed(result);
        }
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
        => napi_instanceof(Env, (napi_value)thisValue, (napi_value)constructor, out c_bool result)
        .ThrowIfFailed((bool)result);

    public static unsafe JSValue DefineClass(
        ReadOnlySpan<byte> utf8Name,
        napi_callback callback,
        nint data,
        nuint count,
        napi_property_descriptor* descriptors)
    {
        fixed (byte* namePtr = utf8Name)
        {
            return napi_define_class(
                Env,
                (nint)namePtr,
                (nuint)utf8Name.Length,
                callback,
                data,
                count,
                (nint)descriptors,
                out napi_value result)
                .ThrowIfFailed(result);
        }
    }

    public static unsafe JSValue DefineClass(
        ReadOnlySpan<byte> utf8Name,
        JSCallbackDescriptor constructorDescriptor,
        params JSPropertyDescriptor[] propertyDescriptors)
    {
        GCHandle descriptorHandle = GCHandle.Alloc(constructorDescriptor);
        JSValue? func = null;
        napi_callback callback = new(
            JSValueScope.Current?.ScopeType == JSValueScopeType.RootNoContext
            ? &InvokeJSCallbackNoContext
            : &InvokeJSCallback);

        nint[] handles = ToUnmanagedPropertyDescriptors(
            utf8Name, propertyDescriptors, (name, count, descriptorsPtr) =>
        {
            func = DefineClass(name, callback, (nint)descriptorHandle, count, descriptorsPtr);
        });
        func!.Value.AddGCHandleFinalizer((nint)descriptorHandle);
        Array.ForEach(handles, handle => func!.Value.AddGCHandleFinalizer(handle));
        return func!.Value;
    }

    public static unsafe JSValue DefineClass(
        string name,
        JSCallbackDescriptor constructorDescriptor,
        params JSPropertyDescriptor[] propertyDescriptors)
    {
        return DefineClass(
            Encoding.UTF8.GetBytes(name), constructorDescriptor, propertyDescriptors);
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
        GCHandle valueHandle = GCHandle.Alloc(value);
        napi_wrap(
            Env,
            (napi_value)wrapper,
            (nint)valueHandle,
            new napi_finalize(&FinalizeGCHandle),
            nint.Zero,
            null).ThrowIfFailed();
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
        GCHandle valueHandle = GCHandle.Alloc(value);
        napi_ref weakRef;
        napi_wrap(
            Env,
            (napi_value)wrapper,
            (nint)valueHandle,
            new napi_finalize(&FinalizeGCHandle),
            nint.Zero,
            &weakRef).ThrowIfFailed();
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
        napi_status status = napi_unwrap(Env, (napi_value)thisValue, out nint result);

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
        napi_status status = napi_unwrap(Env, (napi_value)thisValue, out nint result);

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
    public static object Unwrap(this JSValue thisValue)
    {
        napi_unwrap(Env, (napi_value)thisValue, out nint result).ThrowIfFailed();
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
        napi_status status = napi_remove_wrap(Env, (napi_value)thisValue, out nint result);

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

    public static unsafe object GetValueExternal(this JSValue thisValue)
    {
        napi_get_value_external(Env, (napi_value)thisValue, out nint result).ThrowIfFailed();
        return GCHandle.FromIntPtr(result).Target!;
    }

    public static JSReference CreateReference(this JSValue thisValue)
        => new(thisValue);

    public static JSReference CreateWeakReference(this JSValue thisValue)
        => new(thisValue, isWeak: true);

    public static bool IsError(this JSValue thisValue)
        => napi_is_error(
            Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static bool IsExceptionPending()
        => napi_is_exception_pending(Env, out c_bool result).ThrowIfFailed((bool)result);

    public static JSValue GetAndClearLastException()
        => napi_get_and_clear_last_exception(Env, out napi_value result).ThrowIfFailed(result);

    public static bool IsArrayBuffer(this JSValue thisValue)
        => napi_is_arraybuffer(
            Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe Span<byte> GetArrayBufferInfo(this JSValue thisValue)
    {
        napi_get_arraybuffer_info(Env, (napi_value)thisValue, out nint data, out nuint length)
            .ThrowIfFailed();
        return new Span<byte>((void*)data, (int)length);
    }

    public static bool IsTypedArray(this JSValue thisValue)
        => napi_is_typedarray(
            Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe int GetTypedArrayLength(
        this JSValue thisValue,
        out JSTypedArrayType type)
    {
        napi_get_typedarray_info(
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
        napi_get_typedarray_info(
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
        napi_get_typedarray_info(
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
        => napi_is_dataview(Env, (napi_value)thisValue, out c_bool result)
            .ThrowIfFailed((bool)result);

    public static unsafe void GetDataViewInfo(
        this JSValue thisValue,
        out ReadOnlySpan<byte> viewSpan,
        out JSValue arrayBuffer,
        out int byteOffset)
    {
        napi_get_dataview_info(
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
        => napi_get_version(Env, out uint result).ThrowIfFailed(result);

    public static bool IsPromise(this JSValue thisValue)
        => napi_is_promise(Env, (napi_value)thisValue, out c_bool result)
            .ThrowIfFailed((bool)result);

    public static JSValue RunScript(this JSValue thisValue)
        => napi_run_script(Env, (napi_value)thisValue, out napi_value result)
            .ThrowIfFailed(result);

    public static bool IsDate(this JSValue thisValue)
        => napi_is_date(Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static double GetDateValue(this JSValue thisValue)
        => napi_get_date_value(Env, (napi_value)thisValue, out double result)
            .ThrowIfFailed(result);

    public static unsafe void AddFinalizer(this JSValue thisValue, Action finalize)
    {
        GCHandle finalizeHandle = GCHandle.Alloc(finalize);
        napi_add_finalizer(
            Env,
            (napi_value)thisValue,
            (nint)finalizeHandle,
            new napi_finalize(&CallFinalizeAction),
            nint.Zero,
            null).ThrowIfFailed();
    }

    public static unsafe void AddFinalizer(
        this JSValue thisValue, Action finalize, out JSReference finalizerRef)
    {
        GCHandle finalizeHandle = GCHandle.Alloc(finalize);
        napi_ref reference;
        napi_add_finalizer(
            Env,
            (napi_value)thisValue,
            (nint)finalizeHandle,
            new napi_finalize(&CallFinalizeAction),
            nint.Zero,
            &reference).ThrowIfFailed();
        finalizerRef = new JSReference(reference, isWeak: true);
    }

    public static long GetValueBigIntInt64(this JSValue thisValue, out bool isLossless)
    {
        napi_get_value_bigint_int64(
            Env, (napi_value)thisValue, out long result, out c_bool lossless).ThrowIfFailed();
        isLossless = (bool)lossless;
        return result;
    }

    public static ulong GetValueBigIntUInt64(this JSValue thisValue, out bool isLossless)
    {
        napi_get_value_bigint_uint64(
            Env, (napi_value)thisValue, out ulong result, out c_bool lossless).ThrowIfFailed();
        isLossless = (bool)lossless;
        return result;
    }

    public static unsafe ulong[] GetValueBigIntWords(this JSValue thisValue, out int signBit)
    {
        napi_get_value_bigint_words(Env, (napi_value)thisValue, out signBit, out nuint wordCount, null).ThrowIfFailed();
        ulong[] words = new ulong[wordCount];
        fixed (ulong* wordsPtr = &words[0])
        {
            napi_get_value_bigint_words(Env, (napi_value)thisValue, out signBit, out wordCount, wordsPtr).ThrowIfFailed();
        }
        return words;
    }

    public static JSValue GetAllPropertyNames(this JSValue thisValue, JSKeyCollectionMode mode, JSKeyFilter filter, JSKeyConversion conversion)
    {
        return napi_get_all_property_names(
          Env,
          (napi_value)thisValue,
          (napi_key_collection_mode)mode,
          (napi_key_filter)filter,
          (napi_key_conversion)conversion,
          out napi_value result).ThrowIfFailed(result);
    }

    internal static unsafe void SetInstanceData(napi_env env, object? data)
    {
        napi_get_instance_data(env, out nint handlePtr).ThrowIfFailed();
        if (handlePtr != nint.Zero)
        {
            // Current napi_set_instance_data implementation does not call finalizer when we replace existing instance data.
            // It means that we only remove the GC root, but do not call Dispose.
            GCHandle.FromIntPtr(handlePtr).Free();
        }

        if (data != null)
        {
            GCHandle handle = GCHandle.Alloc(data);
            napi_set_instance_data(
              env,
              (nint)handle,
              new napi_finalize(&FinalizeGCHandle),
              DisposeHint).ThrowIfFailed();
        }
    }

    internal static object? GetInstanceData(napi_env env)
    {
        napi_get_instance_data(env, out nint data).ThrowIfFailed();
        return (data != nint.Zero) ? GCHandle.FromIntPtr(data).Target : null;
    }

    public static void DetachArrayBuffer(this JSValue thisValue)
        => napi_detach_arraybuffer(Env, (napi_value)thisValue).ThrowIfFailed();

    public static bool IsDetachedArrayBuffer(this JSValue thisValue)
        => napi_is_detached_arraybuffer(Env, (napi_value)thisValue, out c_bool result).ThrowIfFailed((bool)result);

    public static void SetObjectTypeTag(this JSValue thisValue, in napi_type_tag typeTag)
        => napi_type_tag_object(Env, (napi_value)thisValue, in typeTag);

    public static unsafe void SetObjectTypeTag(this JSValue thisValue, ref Guid typeGuid)
    {
        ReadOnlySpan<Guid> guidSpan = MemoryMarshal.CreateReadOnlySpan(ref typeGuid, 1);
        ReadOnlySpan<byte> guidBytes = MemoryMarshal.AsBytes(guidSpan);
        thisValue.SetObjectTypeTag(MemoryMarshal.AsRef<napi_type_tag>(guidBytes));
    }

    public static bool CheckObjectTypeTag(this JSValue thisValue, in napi_type_tag typeTag)
        => napi_check_object_type_tag(Env, (napi_value)thisValue, typeTag, out c_bool result).ThrowIfFailed((bool)result);

    public static unsafe bool CheckObjectTypeTag(this JSValue thisValue, ref Guid typeGuid)
    {
        ReadOnlySpan<Guid> guidSpan = MemoryMarshal.CreateReadOnlySpan(ref typeGuid, 1);
        ReadOnlySpan<byte> guidBytes = MemoryMarshal.AsBytes(guidSpan);
        return thisValue.CheckObjectTypeTag(MemoryMarshal.AsRef<napi_type_tag>(guidBytes));
    }

    public static void Freeze(this JSValue thisValue)
        => napi_object_freeze(Env, (napi_value)thisValue).ThrowIfFailed();

    public static void Seal(this JSValue thisValue)
        => napi_object_seal(Env, (napi_value)thisValue).ThrowIfFailed();


    private static napi_env Env => (napi_env)JSValueScope.Current;

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
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) =>
                new(propertyDescriptor.Method!, propertyDescriptor.Data));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSGetter(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) =>
                new(propertyDescriptor.Getter!, propertyDescriptor.Data));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static napi_value InvokeJSSetter(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.Callback, (propertyDescriptor) =>
                new(propertyDescriptor.Setter!, propertyDescriptor.Data));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe napi_value InvokeJSCallbackNoContext(
        napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSCallbackDescriptor>(
            env, callbackInfo, JSValueScopeType.RootNoContext, (descriptor) => descriptor);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSMethodNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.RootNoContext, (propertyDescriptor) =>
                new(propertyDescriptor.Method!, propertyDescriptor.Data));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value InvokeJSGetterNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.RootNoContext, (propertyDescriptor) =>
                new(propertyDescriptor.Getter!, propertyDescriptor.Data));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static napi_value InvokeJSSetterNoContext(napi_env env, napi_callback_info callbackInfo)
    {
        return InvokeCallback<JSPropertyDescriptor>(
            env, callbackInfo, JSValueScopeType.RootNoContext, (propertyDescriptor) =>
                new(propertyDescriptor.Setter!, propertyDescriptor.Data));
    }

    private static unsafe napi_value InvokeCallback<TDescriptor>(
        napi_env env,
        napi_callback_info callbackInfo,
        JSValueScopeType scopeType,
        Func<TDescriptor, JSCallbackDescriptor> getCallbackDescriptor)
    {
        using var scope = new JSValueScope(scopeType, env);
        try
        {
            JSCallbackArgs.GetDataAndLength(env, callbackInfo, out object? data, out int length);
            Span<napi_value> args = stackalloc napi_value[length];
            JSCallbackDescriptor descriptor = getCallbackDescriptor((TDescriptor)data!);
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

        handle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe void FinalizeHintHandle(napi_env _1, nint _2, nint hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(hint);
        (handle.Target as IDisposable)?.Dispose();
        handle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void CallFinalizeAction(napi_env env, nint data, nint hint)
    {
        GCHandle gcHandle = GCHandle.FromIntPtr(data);
        try
        {
            // TODO: [vmoroz] In future we will be not allowed to run JS in finalizers.
            // We must remove creation of the scope.
            using var scope = new JSValueScope(JSValueScopeType.Callback, env);
            ((Action)gcHandle.Target!)();
        }
        finally
        {
            gcHandle.Free();
        }
    }

    private static unsafe nint[] ToUnmanagedPropertyDescriptors(
        ReadOnlySpan<byte> name,
        IReadOnlyCollection<JSPropertyDescriptor> descriptors,
        UseUnmanagedDescriptors action)
    {
        napi_callback methodCallback;
        napi_callback getterCallback;
        napi_callback setterCallback;
        if (JSValueScope.Current?.ScopeType == JSValueScopeType.RootNoContext)
        {
            // The NativeHost and ManagedHost set up callbacks without a current module context.
            methodCallback = new napi_callback(&InvokeJSMethodNoContext);
            getterCallback = new napi_callback(&InvokeJSGetterNoContext);
            setterCallback = new napi_callback(&InvokeJSSetterNoContext);
        }
        else
        {
            methodCallback = new napi_callback(&InvokeJSMethod);
            getterCallback = new napi_callback(&InvokeJSGetter);
            setterCallback = new napi_callback(&InvokeJSSetter);
        }

        nint[] handlesToFinalize = new nint[descriptors.Count];
        int count = descriptors.Count;
        napi_property_descriptor* descriptorsPtr = stackalloc napi_property_descriptor[count];
        int i = 0;
        foreach (JSPropertyDescriptor descriptor in descriptors)
        {
            napi_property_descriptor* descriptorPtr = &descriptorsPtr[i];
            descriptorPtr->name = (napi_value)descriptor.Name;
            descriptorPtr->utf8name = nint.Zero;
            descriptorPtr->method = descriptor.Method == null ? default : methodCallback;
            descriptorPtr->getter = descriptor.Getter == null ? default : getterCallback;
            descriptorPtr->setter = descriptor.Setter == null ? default : setterCallback;
            descriptorPtr->value = (napi_value)descriptor.Value;
            descriptorPtr->attributes = (napi_property_attributes)descriptor.Attributes;
            if (descriptor.Data != null || descriptor.Method != null || descriptor.Getter != null || descriptor.Setter != null)
            {
                handlesToFinalize[i] = descriptorPtr->data = (nint)GCHandle.Alloc(descriptor);
            }
            else
            {
                handlesToFinalize[i] = descriptorPtr->data = nint.Zero;
            }
            i++;
        }
        action(name, (nuint)count, descriptorsPtr);
        return handlesToFinalize;
    }

    private unsafe delegate void UseUnmanagedDescriptors(ReadOnlySpan<byte> name, nuint count, napi_property_descriptor* descriptors);

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

            GC.SuppressFinalize(this);
        }
    }
}
