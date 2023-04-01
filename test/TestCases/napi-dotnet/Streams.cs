using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.TestCases;

/// <summary>
/// Tests streaming data between .NET Streams and Node.js Duplex streams.
/// </summary>
[JSExport]
public static class Streams
{
    /// <summary>
    /// Gets .NET stream that will be read from by Node.js.
    /// </summary>
    public static Stream ReadFile(string filePath)
    {
        return File.OpenRead(filePath);
    }

    /// <summary>
    /// Gets .NET stream that will be written to by Node.js.
    /// </summary>
    public static Stream WriteFile(string filePath)
    {
        return File.Create(filePath);
    }

    /// <summary>
    /// Reads data from a file and pipes it to a Node.js Writable stream.
    /// </summary>
    public static async Task PipeFromFileAsync(string filePath, Stream toStream)
    {
        using Stream fileStream = File.OpenRead(filePath);

#if NETFRAMEWORK
        byte[] buffer = new byte[4096];
        int count;
        while ((count = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await toStream.WriteAsync(buffer, 0, count);
        }
#else
        Memory<byte> buffer = new byte[4096].AsMemory();
        int count;
        while ((count = await fileStream.ReadAsync(buffer)) > 0)
        {
            await toStream.WriteAsync(buffer.Slice(0, count));
        }
#endif
    }

    /// <summary>
    /// Creates a file and pipes data from a Node.js Readable stream to the file.
    /// </summary>
    public static async Task PipeToFileAsync(string filePath, Stream fromStream)
    {
        using Stream fileStream = File.Create(filePath);

#if NETFRAMEWORK
        byte[] buffer = new byte[4096];
        int count;
        while ((count = await fromStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, count);
        }
#else
        Memory<byte> buffer = new byte[4096].AsMemory();
        int count;
        while ((count = await fromStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.Slice(0, count));
        }
#endif
    }
}

[JSExport]
public class TestStream : Stream
{
    private readonly MemoryStream _memory;

    public TestStream(string data)
    {
        _memory = new MemoryStream(Encoding.UTF8.GetBytes(data));
    }

    public TestStream(int capacity)
    {
        _memory = new MemoryStream(capacity);
    }

    public static string GetData(TestStream stream)
    {
        return Encoding.UTF8.GetString(stream._memory.ToArray());
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;

    public override long Length => _memory.Length;

    public override long Position
    {
        get => _memory.Position;
        set => _memory.Position = value;
    }

    public override void SetLength(long value) => _memory.SetLength(value);

    public override int Read(byte[] buffer, int offset, int count)
        => _memory.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count)
        => _memory.Write(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
        => _memory.Seek(offset, origin);

    public override void Flush() => _memory.Flush();
}
