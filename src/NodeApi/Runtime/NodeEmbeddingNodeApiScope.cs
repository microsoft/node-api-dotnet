// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using Microsoft.JavaScript.NodeApi.Interop;
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
        try
        {
            JSRuntimeContext context = NodeEmbedding.GetOrCreateContext(env);
            _valueScope = JSValueScope.CreateRuntimeScope(env, context);
        }
        catch
        {
            // A throwing constructor cannot be disposed, so close the native scope opened above
            // before rethrowing, or it would leak for the lifetime of the embedding runtime.
            NodeEmbedding.JSRuntime.EmbeddingRuntimeCloseNodeApiScope(runtime.Handle, _nodeApiScope);
            throw;
        }
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

        // Mark disposal only after both closes succeed: the value scope's LIFO/thread check can throw
        // if a nested scope is still open, and marking first would leave that unretryable and leak the
        // native Node-API scope.
        _valueScope.Dispose();
        NodeEmbedding.JSRuntime.EmbeddingRuntimeCloseNodeApiScope(
            _runtime.Handle, _nodeApiScope)
            .ThrowIfFailed();
        IsDisposed = true;
    }
}
