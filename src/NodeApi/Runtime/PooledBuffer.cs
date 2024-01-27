using System;
using System.Buffers;
using System.Text;

namespace Microsoft.JavaScript.NodeApi.Runtime;

internal struct PooledBuffer : IDisposable
{
    public static readonly PooledBuffer Empty = new();

    public PooledBuffer()
    {
        Buffer = [];
        Length = 0;
    }

#if NETFRAMEWORK

    // Avoid a dependency on System.Buffers with .NET Framwork.
    // It is available as a nuget package, but might not be installed in the application.
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

    public readonly ref byte Pin() => ref Span.GetPinnableReference();

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
}
