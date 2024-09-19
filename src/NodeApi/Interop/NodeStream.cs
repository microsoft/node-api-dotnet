using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Represents a Node.js Duplex, Readable, or Writable stream.
/// </summary>
public partial class NodeStream : Stream
{
    private readonly JSReference _valueReference;
    private readonly SemaphoreSlim? _readableSemaphore;
    private readonly SemaphoreSlim? _drainSemaphore;
    private JSError? _error;

    public static explicit operator NodeStream(JSValue value) => new(value);
    public static implicit operator JSValue(NodeStream stream)
        => stream._valueReference.GetValue();

    private NodeStream(JSValue value)
    {
        bool isObject = value.IsObject();
        bool canRead = isObject && value.HasProperty("read");
        bool canWrite = isObject && value.HasProperty("write");
        if (!canRead && !canWrite)
        {
            throw new ArgumentException("Stream is neither readable nor writeable.", nameof(value));
        }

        _valueReference = new JSReference(value);

        if (canRead)
        {
            _readableSemaphore = new SemaphoreSlim(0);
            value.CallMethod("on", "readable", JSValue.CreateFunction("onreadable", (args) =>
            {
                _readableSemaphore.Release();
                return JSValue.Undefined;
            }));
            value.CallMethod("on", "end", JSValue.CreateFunction("onend", (args) =>
            {
                _readableSemaphore.Release();
                return JSValue.Undefined;
            }));
        }

        if (canWrite)
        {
            _drainSemaphore = new SemaphoreSlim(0);
            value.CallMethod("on", "drain", JSValue.CreateFunction("ondrain", (args) =>
            {
                _drainSemaphore.Release();
                return JSValue.Undefined;
            }));
        }

        value.CallMethod("on", "error", JSValue.CreateFunction("onerror", (args) =>
        {
            _error = new JSError(args[0]);
            _readableSemaphore?.Release();
            _drainSemaphore?.Release();
            return JSValue.Undefined;
        }));
    }

    private JSValue Value => _valueReference.GetValue();

    public override bool CanRead => Value.HasProperty("read");

    public override bool CanWrite => Value.HasProperty("write");

    /// <summary>
    /// Node.js Readable / Writable streams do not directly support seeking. The position must be
    /// established using a different API when creating the stream.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Node.js Readable / Writable streams do not directly support seeking. The position must be
    /// established using a different API when creating the stream.
    /// </summary>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Node.js Readable / Writable streams do not directly support seeking. The position must be
    /// established using a different API when creating the stream.
    /// </summary>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// Node.js Readable / Writable streams do not support getting the stream length.
    /// </summary>
    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Node.js Readable / Writable streams do not support setting the stream length.
    /// </summary>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
#pragma warning disable IDE0060 // Unused parameter 'buffer'
#if STREAM_MEMORY
    public override int Read(Span<byte> buffer)
#else
    public int Read(Span<byte> buffer)
#endif
#pragma warning restore IDE0060
    {
        // Synchronous reading could block the Node.js event loop while waiting for more data
        // which will never come because data events are dispatched by the event loop.
        throw new NotSupportedException(
            "Synchronous reading from Node.js streams is not supported.");
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellation)
        => ReadAsync(buffer.AsMemory(offset, count), cancellation).AsTask();

    /// <inheritdoc/>
#if STREAM_MEMORY
    public override async ValueTask<int> ReadAsync(
#else
    public async ValueTask<int> ReadAsync(
#endif
        Memory<byte> buffer,
        CancellationToken cancellation = default)
    {
        ThrowIfError();

        int count = buffer.Length;
        JSValue value = Value;
        JSValue result = value.CallMethod("read", count);
        while (result.IsNull())
        {
            if ((bool)value.GetProperty("readableEnded"))
            {
                // End of stream.
                return 0;
            }
            else
            {
                int readableLength = (int)value.GetProperty("readableLength");
                if (readableLength > 0)
                {
                    // There are some bytes available, but fewer than the requested count.
                    count = Math.Min(readableLength, buffer.Length);
                    buffer = buffer.Slice(0, count);
                }
                else
                {
                    // No data is currently available. Wait for the next "readable" event, which will be
                    // raised either when data becomes available or the end of the stream is reached.
                    await _readableSemaphore!.WaitAsync(cancellation);
                    ThrowIfError();
                    value = Value;
                }

                result = value.CallMethod("read", count);
            }
        }

        if (!result.IsTypedArray())
        {
            if (result.IsNull())
            {
                return 0;
            }

            // The readable stream may be in "object mode", which isn't supported.
            throw new NotSupportedException(
                "Unsupported stream read result type: " + result.TypeOf());
        }

        Memory<byte> bytes = ((JSTypedArray<byte>)result).Memory;
        bytes.CopyTo(buffer);
        return bytes.Length;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        var bytes = new JSTypedArray<byte>(buffer.AsMemory(offset, count));
        bool drained = (bool)Value.CallMethod("write", bytes);

        if (!drained)
        {
            // The stream's internal buffer is full. Wait for the 'drain' event, which will be
            // raised when the buffer is no longer full.
            _drainSemaphore!.Wait();
        }

        ThrowIfError();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellation)
    {
        var bytes = new JSTypedArray<byte>(buffer.AsMemory(offset, count));
        bool drained = (bool)Value.CallMethod("write", bytes);

        if (!drained)
        {
            // The stream's internal buffer is full. Wait for the 'drain' event, which will be
            // raised when the buffer is no longer full.
            await _drainSemaphore!.WaitAsync(cancellation);
        }

        ThrowIfError();
    }

    /// <inheritdoc/>
#if STREAM_MEMORY
    public override async ValueTask WriteAsync(
#else
    public async ValueTask WriteAsync(
#endif
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellation = default)
    {
        var bytes = new JSTypedArray<byte>(MemoryMarshal.AsMemory(buffer));
        bool drained = (bool)Value.CallMethod("write", bytes);

        if (!drained)
        {
            // The stream's internal buffer is full. Wait for the 'drain' event, which will be
            // raised when the buffer is no longer full.
            await _drainSemaphore!.WaitAsync(cancellation);
        }

        ThrowIfError();
    }

    /// <summary>
    /// Does nothing because Node.js Writable streams do not support flushing.
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// Does nothing because Node.js Writable streams do not support flushing.
    /// </summary>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private void ThrowIfError()
    {
        if (_error.HasValue)
        {
            throw new JSException(_error.Value);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            JSValue? value = _valueReference.GetValue();
            value?.CallMethod("destroy");
            _valueReference.Dispose();
        }
    }
}
