// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using static JSRuntime;
using static NodejsRuntime;

public sealed class NodeEmbeddingNodeApiScope : IDisposable
{
    readonly NodeEmbeddingRuntime _runtime;
    private node_embedding_node_api_scope _nodeApiScope;
    private readonly JSValueScope _valueScope;

    public NodeEmbeddingNodeApiScope(NodeEmbeddingRuntime runtime)
    {
        _runtime = runtime;
        NodeEmbedding.JSRuntime.EmbeddingRuntimeOpenNodeApiScope(
            runtime.Handle, out _nodeApiScope, out napi_env env)
            .ThrowIfFailed();
        _valueScope = new JSValueScope(
            JSValueScopeType.Root, env, NodeEmbedding.JSRuntime);
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
        NodeEmbedding.JSRuntime.EmbeddingRuntimeCloseNodeApiScope(
            _runtime.Handle, _nodeApiScope)
            .ThrowIfFailed();
    }
}
