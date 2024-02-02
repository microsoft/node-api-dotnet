// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSTypedArray<T> : IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSTypedArray<T>>
#endif
    where T : struct
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSTypedArray<T> value) => value.AsJSValue();
    public static explicit operator JSTypedArray<T>?(JSValue value) => value.As<JSTypedArray<T>>();
    public static explicit operator JSTypedArray<T>(JSValue value)
        => value.As<JSTypedArray<T>>()
        ?? throw new InvalidCastException("JSValue is not a TypedArray.");


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

    //TODO: (vmoroz) Implement correctly
    public static bool CanBeConvertedFrom(JSValue value) => value.IsObject();

    public static JSTypedArray<T> CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

    /// <summary>
    /// Checks if this Memory is already owned by a JS TypedArray value, and if so
    /// returns that JS value.
    /// </summary>
    /// <returns>The JS value, or null if the memory is external to JS.</returns>
    private static unsafe JSValue? GetJSValueForMemory(ReadOnlyMemory<T> data)
    {
        // This assumes the owner object of a Memory struct is stored as a reference in the
        // first (private) field of the struct. If the Memory internal structure ever changes
        // (in a future major version of the .NET Runtime), this unsafe code could crash.
        // Unfortunately there's no public API to get the Memory owner object.
        void* memoryPointer = Unsafe.AsPointer(ref data);
        object? memoryOwner = Unsafe.Read<object?>(memoryPointer);
        if (memoryOwner is MemoryManager manager)
        {
            // The Memory was created from a JS TypedArray.

            // Strip the high bit of the index - it has a special meaning.
            void* memoryIndexPointer = (byte*)memoryPointer + Unsafe.SizeOf<object?>();
            int index = Unsafe.Read<int>(memoryIndexPointer) & ~int.MinValue;

            void* memoryLengthPointer = (byte*)memoryIndexPointer + Unsafe.SizeOf<int>();
            int length = Unsafe.Read<int>(memoryLengthPointer);

            JSValue value = manager.JSValue;
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

        public JSValue JSValue => _typedArrayReference.GetValue() ??
            throw new ObjectDisposedException(nameof(JSTypedArray<T>));

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
