// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

public interface IFluidContainer : IDisposable
{
    public ConnectionState ConnectionState { get; }

    public bool IsDirty { get; }

    // TODO: Marshal as "disposed" property.
    public bool IsDisposed { get; }

    public JSValue InitialObjects { get; set; }

    // TODO: Marshal attribute to indicate the dictionary should be marshalled as object (not Map).
    ////[JSMarshalAs(JSMarshal.Object)]
    ////public IDictionary<string, JSValue> InitialObjects { get; set; }

    // TODO: Automatic marshalling of stringified enums.
    public string AttachState { get; }

    public Task<string> Attach();

    public void Connect();

    public void Disconnect();

    ////public Task<T> Create<T>();

    // TODO: Events
    /*
    public event EventHandler<EventArgs> Connected;
    public event EventHandler<EventArgs> Disconnected;
    public event EventHandler<EventArgs> Saved;
    public event EventHandler<EventArgs> Dirty;
    public event EventHandler<EventArgs> Disposed;
    */
}

public enum ConnectionState
{
    Disconnected = 0,
    EstablishingConnection = 3,
    CatchingUp = 1,
    Connected = 2,
}

public struct Connection
{
    public string Id { get; set; }

    public string Mode { get; set; }
}
