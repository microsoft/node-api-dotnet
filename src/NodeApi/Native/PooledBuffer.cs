using System;
using System.Buffers;
using System.Text;

namespace Microsoft.JavaScript.NodeApi;

public struct PooledBuffer : IDisposable
{
    private ArrayPool<byte>? _pool;
    private readonly byte[] _buffer;

    public static readonly PooledBuffer Empty = new PooledBuffer(null, Array.Empty<byte>(), 0);

    public PooledBuffer(ArrayPool<byte> pool, int length)
        : this(pool, pool.Rent(length), length) {}

    public PooledBuffer(ArrayPool<byte> pool, int length, int bufferMinimumLength)
        : this(pool, pool.Rent(bufferMinimumLength), length) {}

    private PooledBuffer(ArrayPool<byte>? pool, byte[] buffer, int length)
    {
        _pool = pool;
        _buffer = buffer;
        Length = length;
    }

    public int Length { get; private set;}

    public byte[]? Buffer => _buffer;

    public Span<byte> Span => _buffer;

    public ref byte Pin() => ref Span.GetPinnableReference();

    public void Dispose()
    {
        if (_pool != null)
        {
            _pool.Return(_buffer!);
            _pool = null;
        }
    }

    public static unsafe PooledBuffer FromStringUtf8(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Empty;
        }

        int byteLength = Encoding.UTF8.GetByteCount(value);
        PooledBuffer buffer = new PooledBuffer(ArrayPool<byte>.Shared, byteLength, byteLength + 1);

        fixed (char* pChars = value)
        fixed (byte* pBuffer = buffer.Buffer)
        {
            // The Span<byte> overload of GetBytes() would be nicer, but is not available on .NET 4.
            Encoding.UTF8.GetBytes(pChars, value!.Length, pBuffer, byteLength);
            pBuffer[byteLength] = 0;
        }

        return buffer;
    }
}
