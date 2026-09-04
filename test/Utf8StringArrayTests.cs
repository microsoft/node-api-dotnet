// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// The bug only affects the .NET path, where the buffer is pool-rented (not zeroed); on .NET
// Framework it is a zeroed new byte[] and Marshal.PtrToStringUTF8 is unavailable.
#if !(NETFRAMEWORK || NETSTANDARD)

using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

public class Utf8StringArrayTests
{
    // Regression test for #481: a dirty (non-zeroed) pooled buffer must not leak bytes into the
    // marshaled strings, which requires writing the null terminator explicitly.
    [Fact]
    public void MarshalsStringsCorrectlyWithDirtyPooledBuffer()
    {
        string[] input =
        {
            "node",
            "--disable-wasm-trap-handler",
            "--max-old-space-size=4096",
        };

        PoisonSharedByteArrayPool();

        string[] roundTripped;
        using (var utf8Array = new Utf8StringArray(input))
        {
            nint[] pointers = utf8Array.Utf8Strings;
            roundTripped = new string[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                roundTripped[i] = Marshal.PtrToStringUTF8(pointers[i])!;
            }
        }

        Assert.Equal(input, roundTripped);
    }

    // Fill pooled buffers with non-zero bytes and return them, so the next rent starts dirty.
    private static void PoisonSharedByteArrayPool()
    {
        var rented = new List<byte[]>();
        for (int size = 8; size <= 1024; size *= 2)
        {
            for (int n = 0; n < 16; n++)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
                for (int k = 0; k < buffer.Length; k++)
                {
                    buffer[k] = 0xEF;
                }

                rented.Add(buffer);
            }
        }

        foreach (byte[] buffer in rented)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

#endif
