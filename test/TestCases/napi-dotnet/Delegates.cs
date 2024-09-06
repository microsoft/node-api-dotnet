// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.TestCases;

[JSExport]
public delegate string TestDelegate(string value);


[JSExport]
public static class Delegates
{
    public delegate string NestedDelegate(string value);

    public static void CallAction(Action<int> actionDelegate, int value) => actionDelegate(value);

    public static int CallFunc(Func<int, int> funcDelegate, int value) => funcDelegate(value);

    public static string CallDelegate(TestDelegate testDelegate, string value) => testDelegate(value);

    public static string CallDotnetDelegate(Func<TestDelegate, string> callDelegate)
        => callDelegate((string value) => "#" + value);

    public static async Task WaitUntilCancelled(CancellationToken cancellation)
    {
        JSSynchronizationContext syncContext = JSSynchronizationContext.Current!;
        TaskCompletionSource<bool> completion = new();
        cancellation.Register(() => syncContext.Post(() => completion.SetResult(true)));
        await completion.Task;
    }

    public static async Task CallDelegateAndCancel(
        Func<CancellationToken, Task> cancellableDelegate)
    {
        CancellationTokenSource cancellationSource = new();
        Task delegateTask = cancellableDelegate(cancellationSource.Token);
        await Task.Delay(100);
        cancellationSource.Cancel();
        await delegateTask;
    }
}
