// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.TestCases;

[JSExport]
public interface ISmpleInterface
{
    string Echo(string value);
}

/// <summary>
/// Marshalling adapters for delegates, interfaces, and collections are designed for compatibility
/// with code that might not be aware of JS threading concerns. So they should be thread-safe,
/// callable from any thread.
/// </summary>
[JSExport]
public static class ThreadSafety
{
    private static void ValidateNotOnJSThread()
    {
        try
        {
            _ = JSValueScope.Current;
            throw new InvalidOperationException("Expected not to be on a JS thread");
        }
        catch (JSInvalidThreadAccessException)
        {
            // Expected for these test cases.
        }
    }

    public static async Task CallDelegateFromOtherThread(Action action)
    {
        await Task.Run(() =>
        {
            ValidateNotOnJSThread();

            action();
        });
    }

    public static async Task<string> CallInterfaceMethodFromOtherThread(
        ISmpleInterface interfaceObj,
        string value)
    {
        return await Task.Run(() =>
        {
            ValidateNotOnJSThread();

            return interfaceObj.Echo(value);
        });
    }

    public static async Task<int> EnumerateCollectionFromOtherThread(
        IReadOnlyCollection<int> collection)
    {
        return await Task.Run(() =>
        {
            ValidateNotOnJSThread();

            int count = 0;
            foreach (var item in collection)
            {
                count++;
            }

            return count;
        });
    }

    public static async Task<int> EnumerateDictionaryFromOtherThread(
        IReadOnlyDictionary<string, string> dictionary)
    {
        return await Task.Run(() =>
        {
            ValidateNotOnJSThread();

            int count = 0;
            foreach (var item in dictionary)
            {
                count++;
            }

            return count;
        });
    }

    public static async Task<bool> ModifyDictionaryFromOtherThread(
        IDictionary<string, string> dictionary, string keyToRemove)
    {
        return await Task.Run(() =>
        {
            ValidateNotOnJSThread();

            return dictionary.Remove(keyToRemove);
        });
    }
}
