// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Indicates the type of <see cref="JSValueScope" /> within the hiearchy of scopes.
/// </summary>
public enum JSValueScopeType
{
    /// <summary>
    /// A limited scope without any <see cref="JSContext" /> or <see cref="JSModuleContext" />.
    /// Used by the Node API .NET native host to set up callbacks before the managed host is
    /// initialized.
    /// </summary>
    NoContext,

    /// <summary>
    /// A parent scope shared by all (non-AOT) .NET modules loaded in the same process. It has
    /// a <see cref="JSContext" /> but no <see cref="JSModuleContext" />.
    /// </summary>
    /// <remarks>
    /// AOT modules do not have any root scope, so each module scope has a separate
    /// <see cref="JSContext"/>.
    /// </remarks>
    Root,

    /// <summary>
    /// A scope specific to each module. It inherits the <see cref="JSContext" /> from the root
    /// scope, and has a unique <see cref="JSModuleContext" />.
    /// </summary>
    /// <remarks>
    /// AOT modules do not have any root scope, so each module also has a separate
    /// <see cref="JSContext"/>.
    /// </remarks>
    Module,

    /// <summary>
    /// Callback scope within a module; inherits context from the module.
    /// </summary>
    Callback,

    /// <summary>
    /// Handle scope within a callback; inherits context from the module.
    /// </summary>
    Handle,

    /// <summary>
    /// Escapable handle scope within a callback; inherits context from the module.
    /// </summary>
    Escapable,
}

public sealed class JSValueScope : IDisposable
{
    private JSValueScope? _parentScope;
    private napi_env _env;
    private SynchronizationContext? _previousSyncContext;
    private nint _scopeHandle;

    [ThreadStatic] private static JSValueScope? s_currentScope;

    public JSValueScopeType ScopeType { get; private set; }

    public static JSValueScope? Current => s_currentScope;

    public bool IsDisposed { get; private set; }

    public JSContext Context { get; private set; } = null!;

    public JSModuleContext? ModuleContext { get; internal set; }

    /// <summary>
    /// A previously-disposed scope that is available for resurrection.
    /// </summary>
    private static JSValueScope? _freeScope;

    public JSValueScope(
        JSValueScopeType scopeType = JSValueScopeType.Handle,
        napi_env env = default)
    {
        Initialize(scopeType, env);
    }

    private JSValueScope() { }

    internal static JSValueScope Create(
        JSValueScopeType scopeType = JSValueScopeType.Handle,
        napi_env env = default)
    {
        JSValueScope? scope = Interlocked.CompareExchange(ref _freeScope, null, _freeScope);
        if (scope is not null)
        {
            // Resurrecting a previously-disposed scope.
            scope.IsDisposed = false;
        }
        else
        {
            scope = new();
        }

        scope.Initialize(scopeType, env);
        return scope;
    }

    private void Initialize(
        JSValueScopeType scopeType = JSValueScopeType.Handle,
        napi_env env = default)
    {
        ScopeType = scopeType;

        _parentScope = s_currentScope;
        s_currentScope = this;

        _env = !env.IsNull
               ? env
               : _parentScope?._env ?? throw new ArgumentException("env is null", nameof(env));

        Context = scopeType switch
        {
            JSValueScopeType.NoContext => null!,
            JSValueScopeType.Root => _parentScope?.Context ?? new JSContext(_env),
            JSValueScopeType.Module => _parentScope?.Context ?? new JSContext(_env),
            JSValueScopeType.Callback => (JSContext)_env,
            _ => _parentScope?.Context
                 ?? throw new InvalidOperationException("Parent scope not found."),
        };

        ModuleContext = _parentScope?.ModuleContext;
        if (scopeType == JSValueScopeType.Module)
        {
            if (ModuleContext != null)
            {
                throw new InvalidOperationException("Module scope cannot be nested.");
            }

            ModuleContext = new JSModuleContext();
        }

        if (scopeType == JSValueScopeType.Root || scopeType == JSValueScopeType.Callback)
        {
            _previousSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(Context.SynchronizationContext);
        }

        _scopeHandle = ScopeType switch
        {
            JSValueScopeType.Handle
                => napi_open_handle_scope(_env, out napi_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            JSValueScopeType.Escapable
                => napi_open_escapable_handle_scope(
                    _env, out napi_escapable_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            _ => default,
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (ScopeType != JSValueScopeType.NoContext)
        {
            napi_env env = (napi_env)Context;

            switch (ScopeType)
            {
                case JSValueScopeType.Handle:
                    napi_close_handle_scope(
                        env, new napi_handle_scope(_scopeHandle)).ThrowIfFailed();
                    break;
                case JSValueScopeType.Escapable:
                    napi_close_escapable_handle_scope(
                        env, new napi_escapable_handle_scope(_scopeHandle)).ThrowIfFailed();
                    break;
                default:
                    SynchronizationContext.SetSynchronizationContext(_previousSyncContext);
                    break;
            }

            s_currentScope = _parentScope;
        }

        if (Interlocked.CompareExchange(ref _freeScope, this, null) is null)
        {
            // This object is being saved for possible future resurrection.
            _parentScope = null;
            _previousSyncContext = null;
        }
        else
        {
            // Another instance is already saved. So this one is discarded.
            GC.SuppressFinalize(this);
        }
    }

    public JSValue Escape(JSValue value)
    {
        if (_parentScope == null)
            throw new InvalidOperationException("Parent scope must not be null.");

        if (ScopeType != JSValueScopeType.Escapable)
            throw new InvalidOperationException(
                "It can be called only for Escapable value scopes.");

        napi_escape_handle(
            (napi_env)this,
            new napi_escapable_handle_scope(_scopeHandle),
            (napi_value)value,
            out napi_value result);
        return new JSValue(result, _parentScope);
    }

    public static explicit operator napi_env(JSValueScope? scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        return scope!._env;
    }
}
