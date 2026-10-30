// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable CA1822 // Mark members as static

using System;
using System.Text;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class PooledBufferTests
{
    public static TheoryData<string> Strings { get; } = new()
    {
        "",
        "x",
        "module-name",
        "\u00e4\u00f6\u00fc \u4e2d\u6587 \U0001F600",
    };

    [Theory]
    [MemberData(nameof(Strings))]
    public void FromStringUtf8IsNullTerminated(string value)
    {
        FillArrayPoolWithGarbage();
        using PooledBuffer buffer = PooledBuffer.FromStringUtf8(value);
        AssertNullTerminatedUtf8(value, buffer);
    }

    [Theory]
    [MemberData(nameof(Strings))]
    public void FromSpanUtf8IsNullTerminated(string value)
    {
        FillArrayPoolWithGarbage();
        using PooledBuffer buffer = PooledBuffer.FromSpanUtf8(value.AsSpan());
        AssertNullTerminatedUtf8(value, buffer);
    }

    [Fact]
    public void FromStringUtf8NullIsEmpty()
    {
        // A null string must still produce a null pointer when pinned.
        using PooledBuffer buffer = PooledBuffer.FromStringUtf8(null);
        Assert.Equal(0, buffer.Length);
        Assert.True(buffer.Span.IsEmpty);
    }

    private static void AssertNullTerminatedUtf8(string expected, PooledBuffer buffer)
    {
        int byteLength = Encoding.UTF8.GetByteCount(expected);
        Assert.Equal(byteLength, buffer.Length);

        // The buffer must hold the terminator so that pinning it never yields a null pointer.
        Assert.True(buffer.Span.Length > byteLength);
        Assert.Equal(0, buffer.Span[byteLength]);
        Assert.Equal(expected, Encoding.UTF8.GetString(buffer.Buffer, 0, byteLength));
    }
}
