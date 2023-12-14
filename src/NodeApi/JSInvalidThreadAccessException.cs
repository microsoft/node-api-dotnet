// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// An exception that was caused by an attempt to access a JavaScript value without any
/// <see cref="JSValueScope" /> established on the current thread, or from a thread associated
/// with a different environment / root scope.
/// </summary>
/// <remarks>
/// All JavaScript values are created within a scope that is bound to the thread that runs the
/// JS environment. They can only be accessed from the same thread and only as long as the scope
/// is still valid (not disposed).
/// </remarks>
/// <seealso cref="JSSynchronizationContext"/>
public class JSInvalidThreadAccessException : InvalidOperationException
{
    /// <summary>
    /// Creates a new instance of <see cref="JSInvalidThreadAccessException" /> with a
    /// current scope and message.
    /// </summary>
    public JSInvalidThreadAccessException(
        JSValueScope? currentScope,
        string? message = null)
        : this(currentScope, targetScope: null, message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSInvalidThreadAccessException" /> with current
    /// and target scopes and a message.
    /// </summary>
    public JSInvalidThreadAccessException(
        JSValueScope? currentScope,
        JSValueScope? targetScope,
        string? message = null)
        : base(message ?? GetMessage(currentScope, targetScope))
    {
        CurrentScope = currentScope;
        TargetScope = targetScope;
    }

    /// <summary>
    /// Gets the scope associated with the current thread (<see cref="JSValueScope.Current" />)
    /// when the exception was thrown, or null if there was no scope for the thread.
    /// </summary>
    public JSValueScope? CurrentScope { get; }

    /// <summary>
    /// Gets the scope of the value (<see cref="JSValue.Scope" />) that was being accessed when
    /// the exception was thrown, or null if a static operation was attempted.
    /// </summary>
    public JSValueScope? TargetScope { get; }

    private static string GetMessage(JSValueScope? currentScope, JSValueScope? targetScope)
    {
        int threadId = Environment.CurrentManagedThreadId;
        string? threadName = Thread.CurrentThread.Name;
        string threadDescription = string.IsNullOrEmpty(threadName) ?
            $"#{threadId}" : $"#{threadId} \"{threadName}\"";

        if (targetScope == null)
        {
            // If the target scope is null, then this was an attempt to access either a static
            // operation or a JS reference (which has an environment but no scope).
            if (currentScope != null)
            {
                // In that case if the current scope is NOT null this exception
                // shouldn't be thrown.
                throw new ArgumentException("Current scope must be null if target scope is null.");
            }

            return $"There is no active JS value scope.\nCurrent thread: {threadDescription}. " +
                $"Consider using the synchronization context to switch to the JS thread.";
        }

        return "The JS value scope cannot be accessed from the current thread.\n" +
            $"The scope of type {targetScope.ScopeType} was created on thread" +
            $"#{targetScope.ThreadId} and is being accessed from {threadDescription}. " +
            $"Consider using the synchronization context to switch to the JS thread.";
    }
}
