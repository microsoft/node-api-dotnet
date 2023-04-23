// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Event emitter that can be backed by either a Node.js `EventEmitter` object or
/// a standalone (runtime-agnostic) listener tracker.
/// </summary>
public class JSEventEmitter : IDisposable
{
    private readonly JSReference? _nodeEmitter;
    private readonly Dictionary<string, JSReference>? _listeners;

    /// <summary>
    /// Creates a new instance of a standalone (runtime-agnostic) event emitter.
    /// </summary>
    public JSEventEmitter()
    {
        _listeners = new Dictionary<string, JSReference>();
    }

    /// <summary>
    /// Creates a new instance of an event emitter backed by a Node.js `EventEmitter` object.
    /// </summary>
    public JSEventEmitter(JSValue nodejsEventEmitter)
    {
        if (!nodejsEventEmitter.IsObject())
        {
            throw new ArgumentException("Event emitter must be an object.");
        }

        _nodeEmitter = new JSReference(nodejsEventEmitter);
    }

    public void AddListener(string eventName, JSValue listener)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod("addListener", eventName, listener);
            return;
        }

        if (!listener.IsFunction())
        {
            throw new ArgumentException("Listener must be a function.");
        }

        JSArray eventListeners;
        if (_listeners!.TryGetValue(eventName, out JSReference? eventListenersReference))
        {
            eventListeners = (JSArray)eventListenersReference.GetValue()!.Value;
        }
        else
        {
            eventListeners = new JSArray();
            _listeners.Add(eventName, new JSReference(eventListeners));
        }

        eventListeners.Add(listener);
    }

    public void RemoveListener(string eventName, JSValue listener)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod("removeListener", eventName, listener);
            return;
        }

        if (_listeners!.TryGetValue(eventName, out JSReference? eventListenersReference))
        {
            JSArray eventListeners = (JSArray)eventListenersReference.GetValue()!.Value;
            eventListeners.Remove(listener);
        }
    }

    public void Once(string eventName, JSCallback listener)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod(
                "once", eventName, JSValue.CreateFunction(eventName, listener));
            return;
        }

        JSValue onceListener = default;
        onceListener = JSValue.CreateFunction(eventName, (args) =>
        {
            listener(args);
            RemoveListener(eventName, onceListener);
            return default;
        });

        AddListener(eventName, onceListener);
    }

    public void Once(string eventName, JSValue listener)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod("once", eventName, listener);
            return;
        }

        JSValue onceListener = default;
        onceListener = JSValue.CreateFunction(eventName, (args) =>
        {
            if (args.Length == 0)
            {
                listener.Call(args.ThisArg);
            }
            else if (args.Length == 1)
            {
                listener.Call(args.ThisArg, args[0]);
            }
            else
            {
                JSValue[] argsArray = new JSValue[args.Length];
                for (int i = 0; i < argsArray.Length; i++)
                {
                    argsArray[i] = args[i];
                }
                listener.Call(args.ThisArg, argsArray);
            }

            RemoveListener(eventName, onceListener);
            return default;
        });

        AddListener(eventName, onceListener);
    }

    public void Emit(string eventName)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod("emit", eventName);
            return;
        }

        if (_listeners!.TryGetValue(eventName, out JSReference? eventListenersReference))
        {
            JSArray eventListeners = (JSArray)eventListenersReference.GetValue()!.Value;
            foreach (JSValue listener in eventListeners)
            {
                listener.Call(thisArg: default);
            }
        }
    }

    public void Emit(string eventName, JSValue arg)
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.GetValue()!.Value.CallMethod("emit", eventName, arg);
            return;
        }

        if (_listeners!.TryGetValue(eventName, out JSReference? eventListenersReference))
        {
            JSArray eventListeners = (JSArray)eventListenersReference.GetValue()!.Value;
            foreach (JSValue listener in eventListeners)
            {
                listener.Call(thisArg: default, arg);
            }
        }
    }

    public void Emit(string eventName, params JSValue[] args)
    {
        if (_nodeEmitter != null)
        {
            JSValue[] argsArray = new JSValue[args.Length + 1];
            argsArray[0] = eventName;
            args.CopyTo(argsArray, 1);
            _nodeEmitter.GetValue()!.Value.CallMethod("emit", argsArray);
            return;
        }

        if (_listeners!.TryGetValue(eventName, out JSReference? eventListenersReference))
        {
            JSArray eventListeners = (JSArray)eventListenersReference.GetValue()!.Value;
            foreach (JSValue listener in eventListeners)
            {
                listener.Call(thisArg: default, args);
            }
        }
    }

    public virtual void Dispose()
    {
        if (_nodeEmitter != null)
        {
            _nodeEmitter.Dispose();
        }
        else
        {
            _listeners!.Values.ToList().ForEach(l => l.Dispose());
            _listeners.Clear();
        }
    }
}
