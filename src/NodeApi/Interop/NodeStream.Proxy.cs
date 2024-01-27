using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi.Interop;

public partial class NodeStream
{
    private const int ReadChunkSize = 4096;
    private static JSReference? s_duplexStreamAdapterReference;
    private static JSReference? s_readableStreamAdapterReference;
    private static JSReference? s_writableStreamAdapterReference;

    /// <summary>
    /// Defines a Node.js class that extends the Duplex stream class. The class has a
    /// constructor callback and optional static properties, along with the the instance
    /// properties and methods inherited from the Duplex class.
    /// </summary>
    /// <param name="className">Name of the stream class.</param>
    /// <param name="constructorDescriptor">Callback that constructs an instance of the .NET
    /// <see cref="Stream"/> subclass.
    /// <param name="staticProperties">Additional static properties on the stream subclass.</param>
    /// <returns>The class object.</returns>
    internal static JSValue DefineStreamClass(
        string className,
        JSCallbackDescriptor constructorDescriptor,
        IEnumerable<JSPropertyDescriptor> staticProperties)
    {
        // For now, instance properties of a stream class are limited to the ones inherited from the
        // Duplex base class, plus the stream implementation methods below. It could be possible
        // to expose additional properties/methods on the stream subclass, but those must not
        // include members inherited from the .NET Stream class: Read, Write, Flush, Seek, etc.
        JSPropertyDescriptor[] properties = staticProperties.Concat(new[]
        {
            JSPropertyDescriptor.Function("_read", Read, JSPropertyAttributes.DefaultMethod),
            JSPropertyDescriptor.Function("_write", Write, JSPropertyAttributes.DefaultMethod),
            JSPropertyDescriptor.Function("_final", Final, JSPropertyAttributes.DefaultMethod),
            JSPropertyDescriptor.Function("_destroy", Destroy, JSPropertyAttributes.DefaultMethod),
            // TODO: Consider implementing writev() ?
        }).ToArray();

        return JSValue.DefineClass(
            className,
            new JSCallbackDescriptor(
                className,
                (args) =>
                {
                    JSValue instance;
                    if (args.Length == 1 && args[0].IsExternal())
                    {
                        // Constructing a JS instance to wrap a pre-existing C# instance.
                        instance = args[0];
                    }
                    else
                    {
                        instance = constructorDescriptor.Callback(args);
                    }

                    var stream = (Stream)instance.GetValueExternal();

                    // Call the base class constructor.
                    JSValue options = JSValue.CreateObject();
                    options["readable"] = stream.CanRead;
                    options["writable"] = stream.CanWrite;
                    JSValue duplexClass = JSRuntimeContext.Current.Import("node:stream", "Duplex");
                    duplexClass.Call(args.ThisArg, options);

                    return JSRuntimeContext.Current.InitializeObjectWrapper(args.ThisArg, instance);
                },
                constructorDescriptor.Data),
            properties);
    }

    /// <summary>
    /// Creates a Node.js Duplex instance that is a proxy to a .NET stream.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DefineStreamClass" />, this just invokes the Node.js Duplex constructor
    /// directly. This is used when the subclass of .NET Stream is not known or not important.
    /// </remarks>
    internal static JSValue CreateProxy(Stream stream)
    {
        // https://nodejs.org/api/stream.html#api-for-stream-implementers

        JSValue duplexConstructor = JSRuntimeContext.Current.Import("node:stream", "Duplex");
        JSValue streamAdapter = GetOrCreateAdapter(stream.CanRead, stream.CanWrite);
        JSValue streamValue = duplexConstructor.CallAsConstructor(streamAdapter);
        streamValue.Wrap(stream);
        return streamValue;
    }

    private static JSValue GetOrCreateAdapter(bool readable, bool writable)
    {
        if (readable && writable)
        {
            if (s_duplexStreamAdapterReference != null)
            {
                return s_duplexStreamAdapterReference.GetValue()!.Value;
            }

            var streamAdapter = new JSObject
            {
                ["read"] = JSValue.CreateFunction("read", Read),
                ["write"] = JSValue.CreateFunction("write", Write),
                ["final"] = JSValue.CreateFunction("final", Final),
                ["destroy"] = JSValue.CreateFunction("destroy", Destroy)
                // TODO: Consider implementing writev() ?
            };

            s_duplexStreamAdapterReference = new JSReference(streamAdapter);
            return streamAdapter;
        }
        else if (readable)
        {
            if (s_readableStreamAdapterReference != null)
            {
                return s_readableStreamAdapterReference.GetValue()!.Value;
            }

            var streamAdapter = new JSObject
            {
                ["read"] = JSValue.CreateFunction("read", Read),
                ["destroy"] = JSValue.CreateFunction("destroy", Destroy)
            };

            s_readableStreamAdapterReference = new JSReference(streamAdapter);
            return streamAdapter;
        }
        else if (writable)
        {
            if (s_writableStreamAdapterReference != null)
            {
                return s_writableStreamAdapterReference.GetValue()!.Value;
            }

            var streamAdapter = new JSObject
            {
                ["write"] = JSValue.CreateFunction("write", Write),
                ["final"] = JSValue.CreateFunction("final", Final),
                ["destroy"] = JSValue.CreateFunction("destroy", Destroy)
                // TODO: Consider implementing writev() ?
            };

            s_writableStreamAdapterReference = new JSReference(streamAdapter);
            return streamAdapter;
        }
        else
        {
            throw new ArgumentException("Stream must be readable or writable.");
        }
    }

    private static JSValue Read(JSCallbackArgs args)
    {
        // The count (argument 0) is intentionally ignored.
        JSValue nodeStream = args.ThisArg;
        var stream = (Stream)nodeStream.Unwrap(typeof(Stream).Name);

        ReadAsync(stream, nodeStream);

        return JSValue.Undefined;
    }

    private static async void ReadAsync(
        Stream stream,
        JSValue nodeStream)
    {
        // https://nodejs.org/api/stream.html#readable_readsize

        using var asyncScope = new JSAsyncScope();
        using JSReference nodeStreamReference = new(nodeStream);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ReadChunkSize);
        try
        {
#if NETFRAMEWORK
            int count = await stream.ReadAsync(buffer, 0, ReadChunkSize);
#else
            int count = await stream.ReadAsync(buffer.AsMemory(0, ReadChunkSize));
#endif

            nodeStream = nodeStreamReference.GetValue()!.Value;
            nodeStream.CallMethod(
                "push", count == 0 ? JSValue.Null : new JSTypedArray<byte>(buffer, 0, count));
        }
        catch (Exception ex)
        {
            try
            {
                nodeStream = nodeStreamReference.GetValue()!.Value;
                nodeStream.CallMethod("destroy", new JSError(ex).Value);
            }
            catch (Exception)
            {
                // Ignore errors from destroy().
            }
        }
        finally
        {
            // Return the rented buffer on the next tick, after data has been processed by push().
            try
            {
                JSSynchronizationContext.Current!.Post(
                    (_) => ArrayPool<byte>.Shared.Return(buffer), null);
            }
            catch (Exception)
            {
                // Sync context is broken? Go ahead and return the rented buffer immediately.
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static JSValue Write(JSCallbackArgs args)
    {
        // The encoding (argument 1) is currently ignored.
        JSValue nodeStream = args.ThisArg;
        var stream = (Stream)nodeStream.Unwrap(typeof(Stream).Name);
        JSValue chunk = args[0];
        JSValue callback = args[2];

        Memory<byte> memory;
        try
        {
            memory = ((JSTypedArray<byte>)chunk).Memory;
        }
        catch (Exception ex)
        {
            throw new JSException(new JSError(ex.Message, JSErrorType.TypeError));
        }

        WriteAsync(stream, memory, callback);

        return JSValue.Undefined;
    }

    private static async void WriteAsync(
        Stream stream,
        Memory<byte> chunk,
        JSValue callback)
    {
        // https://nodejs.org/api/stream.html#writable_writechunk-encoding-callback

        using var asyncScope = new JSAsyncScope();
        using JSReference callbackReference = new(callback);
        try
        {
#if NETFRAMEWORK
            await stream.WriteAsync(chunk.ToArray(), 0, chunk.Length);
#else
            await stream.WriteAsync(chunk);
#endif

            callback = callbackReference.GetValue()!.Value;
            callback.Call();
        }
        catch (Exception ex)
        {
            bool isExceptionPending5 = JSError.IsExceptionPending();
            try
            {
                callback = callbackReference.GetValue()!.Value;
                callback.Call(thisArg: JSValue.Undefined, new JSError(ex).Value);
            }
            catch (Exception)
            {
                // Ignore errors from error callback.
            }
        }
    }

    private static JSValue Final(JSCallbackArgs args)
    {
        JSValue nodeStream = args.ThisArg;
        var stream = (Stream)nodeStream.Unwrap(typeof(Stream).Name);
        JSValue callback = args[0];

        FinalAsync(stream, callback);

        return JSValue.Undefined;
    }

    private static async void FinalAsync(
        Stream stream,
        JSValue callback)
    {
        using var asyncScope = new JSAsyncScope();
        using JSReference callbackReference = new(callback);
        try
        {
            await stream.FlushAsync();

            callback = callbackReference.GetValue()!.Value;
            callback.Call();
        }
        catch (Exception ex)
        {
            try
            {
                callback = callbackReference.GetValue()!.Value;
                callback.Call(thisArg: JSValue.Undefined, new JSError(ex).Value);
            }
            catch (Exception)
            {
                // Ignore errors from error callback.
            }
        }
    }

    private static JSValue Destroy(JSCallbackArgs args)
    {
        // The error (argument 0) is currently ignored.
        JSValue nodeStream = args.ThisArg;
        var stream = (Stream)nodeStream.Unwrap(typeof(Stream).Name);
        JSValue callback = args[1];

        stream.Close();
        callback.Call();

        return JSValue.Undefined;
    }
}
