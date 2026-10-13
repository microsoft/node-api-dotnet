// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.TestCases;

[JSExport]
public static class Errors
{
    public static void ThrowDotnetError(string message)
    {
        throw new Exception(message);
    }

    public static void ThrowJSError(string message, IJSErrors jsErrors)
    {
        jsErrors.ThrowJSError(message);
    }

    public static async Task ThrowAsyncDotnetError(string message)
    {
        await Task.Yield();
        throw new Exception(message);
    }

    public static void WaitAndThrow(
        int milliseconds, IWorkerTerminationCallbacks workerTerminationCallbacks)
    {
        workerTerminationCallbacks.Started();
        Thread.Sleep(milliseconds);
        throw new Exception("Worker termination test");
    }
}

[JSExport]
public interface IJSErrors
{
    void ThrowJSError(string message);
}

[JSExport]
public interface IWorkerTerminationCallbacks
{
    void Started();
}
