// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Coordinates in-flight native TSFN calls with TSFN release.
/// </summary>
internal sealed class TsfnCallGate
{
    /// <summary>
    /// Represents an admitted call that exits the gate when disposed.
    /// </summary>
    public readonly struct Guard : IDisposable
    {
        private readonly TsfnCallGate? _gate;

        internal Guard(TsfnCallGate gate) => _gate = gate;

        public void Dispose() => _gate?.Exit();
    }

    // The high bit marks the gate closed; the remaining bits count in-flight calls.
    private const int ClosedFlag = unchecked((int)0x80000000);
    private const int CountMask = 0x7FFFFFFF;

    private int _state;

    public bool IsClosed => (Volatile.Read(ref _state) & ClosedFlag) != 0;

    public Guard? TryEnter()
    {
        int state = Volatile.Read(ref _state);
        while ((state & ClosedFlag) == 0)
        {
            int updated = Interlocked.CompareExchange(ref _state, state + 1, state);
            if (updated == state)
            {
                return new Guard(this);
            }

            state = updated;
        }

        return null;
    }

    private void Exit() => Interlocked.Decrement(ref _state);

    public void Close()
    {
        // Reject new calls before waiting for admitted calls to finish.
        int state = Volatile.Read(ref _state);
        while ((state & ClosedFlag) == 0)
        {
            int updated = Interlocked.CompareExchange(ref _state, state | ClosedFlag, state);
            if (updated == state)
            {
                break;
            }

            state = updated;
        }

        SpinWait spin = default;
        while ((Volatile.Read(ref _state) & CountMask) != 0)
        {
            spin.SpinOnce();
        }
    }
}
