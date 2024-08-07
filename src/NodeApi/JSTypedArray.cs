// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi;

//TODO: Add support for Uint8ClampedArray
public readonly struct JSTypedArray<T> : IJSValue<JSTypedArray<T>>
    where T : struct
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSTypedArray&lt;T&gt;" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSTypedArray&lt;T&gt;" /> to convert.</param>
    public static implicit operator JSValue(JSTypedArray<T> array) => array._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a
    /// nullable <see cref="JSTypedArray&lt;T&gt;" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSTypedArray&lt;T&gt;" /> if it was successfully created or
    /// `null` if it was failed.
    /// </returns>
    public static explicit operator JSTypedArray<T>?(JSValue value) => value.As<JSTypedArray<T>>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSTypedArray&lt;T&gt;" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSTypedArray&lt;T&gt;" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSTypedArray<T>(JSValue value)
        => value.CastTo<JSTypedArray<T>>();

    private static int ElementSize { get; } = default(T) switch
    {
        sbyte => sizeof(sbyte),
        byte => sizeof(byte),
        short => sizeof(short),
        ushort => sizeof(ushort),
        int => sizeof(int),
        uint => sizeof(uint),
        long => sizeof(long),
        ulong => sizeof(ulong),
        float => sizeof(float),
        double => sizeof(double),
        _ => throw new InvalidCastException("Invalid typed-array type: " + typeof(T)),
    };

    private static JSTypedArrayType ArrayType { get; } = default(T) switch
    {
        sbyte => JSTypedArrayType.Int8,
        byte => JSTypedArrayType.UInt8,
        short => JSTypedArrayType.Int16,
        ushort => JSTypedArrayType.UInt16,
        int => JSTypedArrayType.Int32,
        uint => JSTypedArrayType.UInt32,
        long => JSTypedArrayType.BigInt64,
        ulong => JSTypedArrayType.BigUInt64,
        float => JSTypedArrayType.Float32,
        double => JSTypedArrayType.Float64,
        _ => throw new InvalidCastException("Invalid typed-array type: " + typeof(T)),
    };

    private static string JSTypeName { get; } = default(T) switch
    {
        sbyte => "Int8Array",
        byte => "Uint8Array",
        short => "Int16Array",
        ushort => "Uint16Array",
        int => "Int32Array",
        uint => "Uint32Array",
        long => "BigInt64Array",
        ulong => "BigUint64Array",
        float => "Float32Array",
        double => "Float64Array",
        _ => throw new InvalidCastException("Invalid typed-array type: " + typeof(T)),
    };

    private JSTypedArray(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new typed array of specified length, with newly allocated memory.
    /// </summary>
    public JSTypedArray(int length)
    {
        JSValue arrayBuffer = JSValue.CreateArrayBuffer(length * ElementSize);
        _value = JSValue.CreateTypedArray(ArrayType, length, arrayBuffer, 0);
    }

    /// <summary>
    /// Creates a typed-array over memory, without copying.
    /// </summary>
    public unsafe JSTypedArray(Memory<T> data)
    {
        JSValue? value = GetJSValueForMemory(data);
        if (value is not null)
        {
            _value = value.Value;
        }
        else
        {
            // The Memory was NOT created from a JS TypedArray. Most likely it was allocated
            // directly or via a .NET array or string.

            JSValue arrayBuffer = data.Length > 0 ?
                JSValue.CreateExternalArrayBuffer(data) : JSValue.CreateArrayBuffer(0);
            _value = JSValue.CreateTypedArray(ArrayType, data.Length, arrayBuffer, 0);
        }
    }

    /// <summary>
    /// Creates a typed-array over read-memory, without copying. Only valid for memory
    /// which was previously marshalled from a JS typed-array to .NET.
    /// </summary>
    /// <exception cref="NotSupportedException">The memory is external to JS.</exception>
    public unsafe JSTypedArray(ReadOnlyMemory<T> data)
    {
        JSValue? value = GetJSValueForMemory(data);
        if (value is not null)
        {
            _value = value.Value;
        }
        else
        {
            // Consider copying the memory?
            throw new NotSupportedException(
                "Read-only memory cannot be transferred from .NET to JS.");
        }
    }

    #region IJSValue<JSTypedArray<T>> implementation

    /// <summary>
    /// Checks if the T struct can be created from this instance`.
    /// </summary>
    /// <typeparam name="TOther">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// `true` if the T struct can be created from this instance. Otherwise it returns `false`.
    /// </returns>
    public bool Is<TOther>() where TOther : struct, IJSValue<TOther>
        => _value.Is<TOther>();

    /// <summary>
    /// Tries to create a T struct from this instance.
    /// It returns `null` if the T struct cannot be created.
    /// </summary>
    /// <typeparam name="TOther">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// Nullable value that contains T struct if it was successfully created
    /// or `null` if it was failed.
    /// </returns>
    public TOther? As<TOther>() where TOther : struct, IJSValue<TOther>
        => _value.As<TOther>();

    /// <summary>
    /// Creates a T struct from this instance without checking the enclosed handle type.
    /// It must be used only when the handle type is known to be correct.
    /// </summary>
    /// <typeparam name="TOther">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    public TOther AsUnchecked<TOther>() where TOther : struct, IJSValue<TOther>
        => _value.AsUnchecked<TOther>();

    /// <summary>
    /// Creates a T struct from this instance.
    /// It throws `InvalidCastException` in case of failure.
    /// </summary>
    /// <typeparam name="TOther">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be crated based on this instance.
    /// </exception>
    public TOther CastTo<TOther>() where TOther : struct, IJSValue<TOther>
        => _value.CastTo<TOther>();

    /// <summary>
    /// Determines whether a <see cref="JSTypedArray&lt;T&gt;" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSTypedArray&lt;T&gt;" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSTypedArray<T>>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsObject() && value.InstanceOf(JSValue.Global[JSTypeName]);

    /// <summary>
    /// Creates a new instance of <see cref="JSTypedArray&lt;T&gt;" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSTypedArray&lt;T&gt;" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSTypedArray&lt;T&gt;" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSTypedArray<T> IJSValue<JSTypedArray<T>>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSTypedArray<T> CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    /// <summary>
    /// Checks if this Memory is already owned by a JS TypedArray value, and if so
    /// returns that JS value.
    /// </summary>
    /// <returns>The JS value, or null if the memory is external to JS.</returns>
    private static JSValue? GetJSValueForMemory(ReadOnlyMemory<T> data)
    {
        if (MemoryMarshal.TryGetMemoryManager(data, out MemoryManager? manager, out int index, out int length))
        {
            // The Memory was created from a JS TypedArray.

            JSValue value = manager!.JSValue;
            int valueLength = value.GetTypedArrayLength(out _);

            if (index != 0 || length != valueLength)
            {
                // The Memory was sliced, so get an equivalent slice of the JS TypedArray.
                value = value.CallMethod("slice", index, index + length);
            }

            return value;
        }

        return null;
    }

    /// <summary>
    /// Creates a typed-array over an array, without copying.
    /// </summary>
    public JSTypedArray(T[] data) : this(data.AsMemory())
    {
    }

    /// <summary>
    /// Creates a typed-array over an array, without copying.
    /// </summary>
    public JSTypedArray(T[] data, int start, int length) : this(data.AsMemory().Slice(start, length))
    {
    }

    public int Length => _value.GetTypedArrayLength(out _);

    public T this[int index]
    {
        get => Span[index];
        set => Span[index] = value;
    }

    /// <summary>
    /// Gets the typed-array values as a span, without copying.
    /// </summary>
    public Span<T> Span => _value.GetTypedArrayData<T>();

    /// <summary>
    /// Gets the typed-array values as memory, without copying.
    /// </summary>
    public Memory<T> Memory => new MemoryManager(this).Memory;

    /// <summary>
    /// Copies the typed-array data into a new array and returns the array.
    /// </summary>
    public T[] ToArray() => Span.ToArray();

    /// <summary>
    /// Copies the typed-array data into an array.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        Span.CopyTo(new Span<T>(array, arrayIndex, array.Length - arrayIndex));
    }

    public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSTypedArray<T> a, JSTypedArray<T> b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSTypedArray<T> a, JSTypedArray<T> b) => !a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => _value.StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }

    /// <summary>
    /// Holds a reference to a typed-array value until the memory is disposed.
    /// </summary>
    private unsafe class MemoryManager : MemoryManager<T>
    {
        private readonly void* _pointer;
        private readonly int _length;
        private readonly JSReference _typedArrayReference;

        public MemoryManager(JSTypedArray<T> typedArray)
        {
            Span<T> span = typedArray.Span;
            _pointer = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
            _length = span.Length;
            _typedArrayReference = new JSReference(typedArray);
        }

        public JSValue JSValue => _typedArrayReference.GetValue();

        public override Span<T> GetSpan()
        {
            return new Span<T>(_pointer, _length);
        }

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            // Do TypedArray or ArrayBuffer support pinning?
            // This code assumes the memory buffer is not moveable.
            Span<T> span = GetSpan().Slice(elementIndex);
            void* pointer = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
            return new MemoryHandle(pointer, handle: default, pinnable: this);
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _typedArrayReference.Dispose();
            }
        }
    }
}
