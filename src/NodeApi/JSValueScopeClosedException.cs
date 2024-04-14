// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// An exception that was caused by an attempt to access a <see cref="JSValue" /> (or a more
/// specific JS value type, such as <see cref="JSObject" /> or <see cref="JSArray" />)
/// after its <see cref="JSValueScope" /> was closed.
/// </summary>
public class JSValueScopeClosedException : ObjectDisposedException
{
    /// <summary>
    /// Creates a new instance of <see cref="JSValueScopeClosedException" /> with an optional
    /// object name and message.
    /// </summary>
    public JSValueScopeClosedException(JSValueScope scope, string? message = null)
        : base(scope.ScopeType.ToString(), message ?? GetMessage(scope))
    {
        Scope = scope;
    }

    public JSValueScope Scope { get; }

    private static string GetMessage(JSValueScope scope)
    {
        return $"The JS value scope of type {scope.ScopeType} was closed.\n" +
            "Values created within a scope are no longer available after their scope is " +
            "closed. Consider using an escapable scope to promote a value to the parent scope, " +
            "or a reference to make a value available to a future callback scope.";
    }
}
