// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Hermes.Example.HermesApi.Interop;

namespace Hermes.Example;

public sealed class HermesConfig : IDisposable
{
    private hermes_config _config;
    private bool _isDisposed;

    public HermesConfig()
    {
        hermes_create_config(out _config).ThrowIfFailed();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        hermes_delete_config(_config).ThrowIfFailed();
    }

    public static explicit operator hermes_config(HermesConfig value) => value._config;
}
