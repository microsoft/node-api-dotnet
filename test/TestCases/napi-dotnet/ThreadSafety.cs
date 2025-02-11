// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
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
        await RunInThread(() =>
        {
            ValidateNotOnJSThread();

            action();
        });
    }

    public static async Task<string> CallInterfaceMethodFromOtherThread(
        ISmpleInterface interfaceObj,
        string value)
    {
        return await RunInThread(() =>
        {
            ValidateNotOnJSThread();

            return interfaceObj.Echo(value);
        });
    }

    public static async Task<int> EnumerateCollectionFromOtherThread(
        IReadOnlyCollection<int> collection)
    {
        return await RunInThread(() =>
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
        return await RunInThread(() =>
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
        return await RunInThread(() =>
        {
            ValidateNotOnJSThread();

            return dictionary.Remove(keyToRemove);
        });
    }

    private static Task RunInThread(Action action)
    {
        TaskCompletionSource<bool> threadCompletion = new TaskCompletionSource<bool>();

        Thread thread = new Thread(() =>
        {
            try
            {
                action();
                threadCompletion.TrySetResult(true);
            }
            catch (Exception e)
            {
                threadCompletion.TrySetException(e);
            }
        });
        thread.Start();

        return threadCompletion.Task;
    }

    private static Task<T> RunInThread<T>(Func<T> func)
    {
        TaskCompletionSource<T> threadCompletion = new TaskCompletionSource<T>();

        Thread thread = new Thread(() =>
        {
            try
            {
                threadCompletion.TrySetResult(func());
            }
            catch (Exception e)
            {
                threadCompletion.TrySetException(e);
            }
        });
        thread.Start();

        return threadCompletion.Task;
    }
}
