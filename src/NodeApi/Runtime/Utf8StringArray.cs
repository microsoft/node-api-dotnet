using System;
using System.Runtime.InteropServices;
using System.Text;

internal struct Utf8StringArray : IDisposable
{
    // Use one contiguous buffer for all UTF-8 strings.
    private byte[] _stringBuffer;
    private GCHandle _pinnedStringBuffer;

    public unsafe Utf8StringArray(ReadOnlySpan<string> strings)
    {
        int byteLength = 0;
        for (int i = 0; i < strings.Length; i++)
        {
            byteLength += Encoding.UTF8.GetByteCount(strings[i]) + 1;
        }

#if NETFRAMEWORK || NETSTANDARD
        // Avoid a dependency on System.Buffers with .NET Framework.
        // It is available as a Nuget package, but might not be installed in the application.
        // In this case the buffer is not actually pooled.

        Utf8Strings = new nint[strings.Length];
        _stringBuffer = new byte[byteLength];
#else
        Utf8Strings = System.Buffers.ArrayPool<nint>.Shared.Rent(strings.Length);
        _stringBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(byteLength);
#endif

        // Pin the string buffer
        _pinnedStringBuffer = GCHandle.Alloc(_stringBuffer, GCHandleType.Pinned);
        nint stringBufferPtr = _pinnedStringBuffer.AddrOfPinnedObject();
        int offset = 0;
        for (int i = 0; i < strings.Length; i++)
        {
            fixed (char* src = strings[i])
            {
                Utf8Strings[i] = stringBufferPtr + offset;
                offset += Encoding.UTF8.GetBytes(
                    src, strings[i].Length, (byte*)(stringBufferPtr + offset), byteLength - offset)
                    + 1; // +1 for the string Null-terminator.
            }
        }
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            Disposed = true;
            _pinnedStringBuffer.Free();

#if !(NETFRAMEWORK || NETSTANDARD)
            System.Buffers.ArrayPool<nint>.Shared.Return(Utf8Strings);
            System.Buffers.ArrayPool<byte>.Shared.Return(_stringBuffer);
#endif
        }
    }


    public readonly nint[] Utf8Strings { get; }

    public bool Disposed { get; private set; }

    public readonly ref nint Pin()
    {
        if (Disposed) throw new ObjectDisposedException(nameof(Utf8StringArray));
        Span<nint> span = Utf8Strings;
        return ref span.GetPinnableReference();
    }

    public static unsafe string[] ToStringArray(nint utf8StringArray, int size)
    {
        var utf8Strings = new ReadOnlySpan<nint>((void*)utf8StringArray, size);
        string[] strings = new string[size];
        for (int i = 0; i < utf8Strings.Length; i++)
        {
            strings[i] = PtrToStringUTF8((byte*)utf8Strings[i]);
        }
        return strings;
    }

    public static unsafe string PtrToStringUTF8(byte* ptr)
    {
#if NETFRAMEWORK || NETSTANDARD
        if (ptr == null) throw new ArgumentNullException(nameof(ptr));
        int length = 0;
        while (ptr[length] != 0) length++;
        return Encoding.UTF8.GetString(ptr, length);
#else
        return Marshal.PtrToStringUTF8((nint)ptr) ?? throw new ArgumentNullException(nameof(ptr));
#endif
    }
}
