// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using static JSRuntime;

public sealed class NodejsEmbeddingNodeApiScope : IDisposable
{
    NodejsEmbeddingRuntime _runtime;
    private node_embedding_node_api_scope _nodeApiScope;
    private JSValueScope _valueScope;

    public NodejsEmbeddingNodeApiScope(NodejsEmbeddingRuntime runtime)
    {
        _runtime = runtime;
        NodejsEmbeddingRuntime.JSRuntime.EmbeddingOpenNodeApiScope(
            runtime, out _nodeApiScope, out napi_env env)
            .ThrowIfFailed();
        _valueScope = new JSValueScope(
            JSValueScopeType.Root, env, NodejsEmbeddingRuntime.JSRuntime);
    }

    /// <summary>
    /// Gets a value indicating whether the Node.js embedding Node-API scope is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Disposes the Node.js embedding Node-API scope.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        _valueScope.Dispose();
        NodejsEmbeddingRuntime.JSRuntime.EmbeddingCloseNodeApiScope(_runtime, _nodeApiScope)
            .ThrowIfFailed();
    }
}
