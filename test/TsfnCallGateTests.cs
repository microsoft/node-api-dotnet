// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

public class TsfnCallGateTests
{
    [Fact]
    public void EnterAndExitWhileOpen()
    {
        var gate = new TsfnCallGate();

        using TsfnCallGate.Guard? guard = gate.TryEnter();

        Assert.NotNull(guard);
        Assert.False(gate.IsClosed);
    }

    [Fact]
    public void CloseRejectsFurtherEntry()
    {
        var gate = new TsfnCallGate();

        gate.Close();

        Assert.True(gate.IsClosed);
        Assert.Null(gate.TryEnter());
    }

    [Fact]
    public void CloseIsIdempotent()
    {
        var gate = new TsfnCallGate();

        gate.Close();
        gate.Close();

        Assert.True(gate.IsClosed);
        Assert.Null(gate.TryEnter());
    }

    [Fact]
    public void CloseWaitsForInFlightCallToExit()
    {
        var gate = new TsfnCallGate();
        Task closeTask;
        {
            using TsfnCallGate.Guard? guard = gate.TryEnter();
            Assert.NotNull(guard);

            closeTask = Task.Run(gate.Close);
            Assert.True(SpinWait.SpinUntil(
                () => gate.IsClosed, TimeSpan.FromSeconds(5)));
            Assert.False(closeTask.IsCompleted);
        }

        Assert.True(closeTask.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ConcurrentCallersDrainBeforeCloseCompletes()
    {
        const int callerCount = 8;
        var gate = new TsfnCallGate();
        using var entered = new CountdownEvent(callerCount);
        using var release = new ManualResetEventSlim(false);

        Task[] callers = new Task[callerCount];
        for (int i = 0; i < callers.Length; i++)
        {
            callers[i] = Task.Run(() =>
            {
                {
                    using TsfnCallGate.Guard? guard = gate.TryEnter();
                    Assert.NotNull(guard);
                    entered.Signal();
                    release.Wait();
                }

                using TsfnCallGate.Guard? guardAfterClose = gate.TryEnter();
                Assert.Null(guardAfterClose);
            });
        }

        Task? closeTask = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            closeTask = Task.Run(gate.Close);
            Assert.True(SpinWait.SpinUntil(
                () => gate.IsClosed, TimeSpan.FromSeconds(5)));
            Assert.False(closeTask.IsCompleted);
        }
        finally
        {
            release.Set();
        }

        Assert.NotNull(closeTask);
        Assert.True(closeTask.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(Task.WaitAll(callers, TimeSpan.FromSeconds(30)));
    }
}
