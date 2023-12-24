// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Numerics;
using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

public class TestBigInt : TestHelper, ITestObject
{
    private static JSValue IsLossless(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        bool isSigned = (bool)args[1];

        if (isSigned)
        {
            big.ToInt64(out bool isLossless);
            return isLossless;
        }
        else
        {
            big.ToUInt64(out bool isLossless);
            return isLossless;
        }
    }

    private static JSValue IsBigInt(JSCallbackArgs args)
    {
        JSBigInt? big = args[0].AsJSBigInt();
        return big.HasValue;
    }

    private static JSValue TestInt64(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        long input = big.ToInt64(out _);
        return new JSBigInt(input);
    }

    private static JSValue TestUInt64(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        ulong input = big.ToUInt64(out _);
        return new JSBigInt(input);
    }

    private static JSValue TestWords(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        ulong[] words = big.ToUInt64Array(out int sign);
        return new JSBigInt(sign, words);
    }

    private static JSValue TestWordSpan(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        int expectedWordCount = big.GetWordCount();
        Span<ulong> words = stackalloc ulong[10];
        big.CopyTo(words, out int sign, out int wordCount);

        if (wordCount != expectedWordCount)
        {
            JSError.ThrowError("word count did not match");
            return default;
        }

        return new JSBigInt(sign, words.Slice(0, wordCount));
    }

    private static JSValue TestBigInteger(JSCallbackArgs args)
    {
        JSBigInt big = (JSBigInt)args[0];
        string bigStr = (string)args[1];
        BigInteger expected = BigInteger.Parse(bigStr);
        BigInteger actual = big.ToBigInteger();
        if (expected != actual)
        {
            JSError.ThrowError("BigInteger is not parsed correctly");
            return default;
        }

        return new JSBigInt(actual);
    }

    public JSObject Init() => [
        Method(IsLossless),
        Method(IsBigInt),
        Method(TestInt64),
        Method(TestUInt64),
        Method(TestWords),
        Method(TestWordSpan),
        Method(TestBigInteger),
    ];
}
