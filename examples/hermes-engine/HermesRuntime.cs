// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Hermes.Example.HermesApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Hermes.Example;

public sealed class HermesRuntime : IDisposable
{
    private hermes_runtime _runtime;
    private bool _isDisposed;

    public HermesRuntime(HermesConfig? config)
    {
        if (config != null)
        {
            hermes_create_runtime((hermes_config)config, out _runtime).ThrowIfFailed();
        }
        else
        {
            using HermesConfig tempConfig = new();
            hermes_create_runtime((hermes_config)tempConfig, out _runtime).ThrowIfFailed();
        }
    }

    public HermesRuntime() : this(null) { }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        hermes_delete_runtime(_runtime).ThrowIfFailed();
    }

    public static explicit operator hermes_runtime(HermesRuntime value) => value._runtime;

    public static explicit operator napi_env(HermesRuntime value)
        => hermes_get_node_api_env((hermes_runtime)value, out napi_env env).ThrowIfFailed(env);
}
