// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
#if !(NETFRAMEWORK || NETSTANDARD)
using System.Buffers;
#endif
using System.ComponentModel;
using System.Text;

internal struct PooledBuffer : IDisposable
{
    public static readonly PooledBuffer Empty = new();

    public PooledBuffer()
    {
        Buffer = [];
        Length = 0;
    }

#if NETFRAMEWORK || NETSTANDARD

    // Avoid a dependency on System.Buffers with .NET Framework.
    // It is available as a NuGet package, but might not be installed in the application.
    // In this case the buffer is not actually pooled.

    public PooledBuffer(int length) : this(length, length) { }

    public PooledBuffer(int length, int bufferMinimumLength)
    {
        Buffer = new byte[bufferMinimumLength];
        Length = length;
    }

    public readonly void Dispose() { }

#else

    private ArrayPool<byte>? _pool;

    private PooledBuffer(int length, int bufferMinimumLength)
        : this(ArrayPool<byte>.Shared, length, bufferMinimumLength) { }

    private PooledBuffer(ArrayPool<byte> pool, int length, int bufferMinimumLength)
    {
        _pool = pool;
        Buffer = pool.Rent(bufferMinimumLength);
        Length = length;
    }

    public void Dispose()
    {
        if (_pool != null)
        {
            _pool.Return(Buffer!);
            _pool = null;
        }
    }

#endif

    public int Length { get; private set; }

    public readonly byte[] Buffer { get; }

    public readonly Span<byte> Span => Buffer;

    // To support PooledBuffer usage within a fixed statement.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly ref byte GetPinnableReference() => ref Span.GetPinnableReference();

    public static unsafe PooledBuffer FromStringUtf8(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Empty;
        }

        int byteLength = Encoding.UTF8.GetByteCount(value);
        PooledBuffer buffer = new(byteLength, byteLength + 1);
        Encoding.UTF8.GetBytes(value, 0, value!.Length, buffer.Buffer, 0);

        return buffer;
    }

    public static unsafe PooledBuffer FromSpanUtf8(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return Empty;
        }

        fixed (char* valuePtr = value)
        {
            int byteLength = Encoding.UTF8.GetByteCount(valuePtr, value.Length);
            PooledBuffer buffer = new(byteLength, byteLength + 1);
            fixed (byte* bufferPtr = buffer.Span)
                Encoding.UTF8.GetBytes(valuePtr, value.Length, bufferPtr, byteLength + 1);
            return buffer;
        }
    }
}
