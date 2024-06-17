// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Extension methods for converting between .NET tasks and JS promises.
/// </summary>
public static class TaskExtensions
{
    public static Task<JSValue> AsTask(this JSPromise promise)
    {
        TaskCompletionSource<JSValue> completion = new();
        promise.Then(
            (JSValue value) =>
            {
                completion.SetResult(value);
                return default;
            },
            (JSError error) =>
            {
                completion.SetException(new JSException(error));
                return default;
            });
        return completion.Task;
    }

    public static Task<JSValue> AsTask(this JSPromise promise, CancellationToken cancellation)
    {
        TaskCompletionSource<JSValue> completion = new();
        cancellation.Register(() => completion.TrySetCanceled(cancellation));
        promise.Then(
            (JSValue value) =>
            {
                completion.TrySetResult(value);
                return default;
            },
            (JSError error) =>
            {
                completion.TrySetException(new JSException(error));
                return default;
            });
        return completion.Task;
    }

    public static async Task<T> AsTask<T>(
        this JSPromise promise,
        JSValue.To<T> fromJS)
    {
        Task<JSValue> jsTask = promise.AsTask();
        return fromJS(await jsTask);
    }

    public static async Task<T> AsTask<T>(
        this JSPromise promise,
        JSValue.To<T> fromJS,
        CancellationToken cancellation)
    {
        Task<JSValue> jsTask = promise.AsTask(cancellation);
        return fromJS(await jsTask);
    }

    public static JSPromise AsPromise(this Task task)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            return JSPromise.Resolve();
        }

        return new JSPromise(async (resolve) =>
        {
            await task;
            resolve(JSValue.Undefined);
        });
    }

    public static JSPromise AsPromise(this Task<JSValue> task)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            return JSPromise.Resolve(task.Result);
        }

        return new JSPromise(async (resolve) =>
        {
            JSValue jsValue = await task;
            resolve(jsValue);
        });
    }

    public static JSPromise AsPromise<T>(this Task<T> task, JSValue.From<T> toJS)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            return JSPromise.Resolve(toJS(task.Result));
        }

        return new JSPromise(async (resolve) =>
        {
            T value = await task;
            resolve(toJS(value));
        });
    }

    public static JSPromise AsPromise(this ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return JSPromise.Resolve();
        }

        return new JSPromise(async (resolve) =>
        {
            await task;
            resolve(JSValue.Undefined);
        });
    }

    public static JSPromise AsPromise(this ValueTask<JSValue> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return JSPromise.Resolve(task.Result);
        }

        return new JSPromise(async (resolve) =>
        {
            JSValue jsValue = await task;
            resolve(jsValue);
        });
    }

    public static JSPromise AsPromise<T>(this ValueTask<T> task, JSValue.From<T> toJS)
    {
        if (task.IsCompletedSuccessfully)
        {
            return JSPromise.Resolve(toJS(task.Result));
        }

        return new JSPromise(async (resolve) =>
        {
            T value = await task;
            resolve(toJS(value));
        });
    }
}
