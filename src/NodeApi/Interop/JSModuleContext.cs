// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Manages JavaScript interop context for the lifetime of a .NET module.
/// </summary>
/// <remarks>
/// A <see cref="JSModuleContext"/> instance is constructed when the module is loaded and disposed
/// when the module is unloaded.
/// </remarks>
public sealed class JSModuleContext : IDisposable
{
    /// <summary>
    /// Gets the current module context.
    /// </summary>
    public static JSModuleContext Current => JSValueScope.Current.ModuleContext
        ?? throw new InvalidCastException("No current module context.");

    /// <summary>
    /// Gets an instance of the class that represents the module, or null if there is no module
    /// class.
    /// </summary>
    public object? Module { get; internal set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        if (Module is IDisposable module)
        {
            module.Dispose();
        }
    }
}
