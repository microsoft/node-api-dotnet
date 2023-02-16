using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NodeApi;

public readonly struct JSTypedArray<T> : IEquatable<JSValue> where T : struct
{
    private readonly JSValue _value;

    public static explicit operator JSTypedArray<T>(JSValue value) => new(value);
    public static implicit operator JSValue(JSTypedArray<T> arr) => arr._value;

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
        // Check if this Memory is already owned by a JS TypedArray value.
        // This assumes the owner object of a Memory struct is stored as a reference in the
        // first (private) field of the struct. If the Memory internal structure ever changes
        // (in a future major version of the .NET Runtime), this unsafe code could crash.
        // Unfortunately there's no public API to get the Memory owner object.
        void* memoryPointer = Unsafe.AsPointer(ref data);
        object? memoryOwner = Unsafe.Read<object?>(memoryPointer);
        if (memoryOwner is MemoryManager manager)
        {
            // The Memory was created from a JS TypedArray.

            void* memoryIndexPointer = (byte*)memoryPointer + Unsafe.SizeOf<object?>();
            int index = Unsafe.Read<int>(memoryIndexPointer);
            void* memoryLengthPointer = (byte*)memoryIndexPointer + Unsafe.SizeOf<int>();
            int length = Unsafe.Read<int>(memoryLengthPointer);

            _value = manager.JSValue;
            int valueLength = _value.GetTypedArrayLength(out _);

            if (index != 0 || length != valueLength)
            {
                // The Memory was sliced, so get an equivalent slice of the JS TypedArray.
                _value = _value.CallMethod("slice", index, index + length);
            }
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

    public Span<T> Span => _value.GetTypedArrayData<T>();

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
    /// Gets the typed-array values as memory, without copying.
    /// </summary>
    public Memory<T> AsMemory()
    {
        JSReference typedArrayReference = new(_value);
        return new MemoryManager(typedArrayReference).Memory;
    }

    /// <summary>
    /// Holds a reference to a typed-array value until the memory is disposed.
    /// </summary>
    private class MemoryManager : MemoryManager<T>
    {
        private readonly JSReference _typedArrayReference;

        public MemoryManager(JSReference typedArrayReference)
        {
            _typedArrayReference = typedArrayReference;
        }

        public JSValue JSValue => _typedArrayReference.GetValue() ??
            throw new ObjectDisposedException(nameof(JSTypedArray<T>));

        public override Span<T> GetSpan()
        {
            return ((JSTypedArray<T>)JSValue).Span;
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
