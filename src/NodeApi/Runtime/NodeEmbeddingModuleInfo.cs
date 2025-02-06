// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static NodeEmbedding;

public class NodeEmbeddingModuleInfo
{
    public required string Name { get; set; }
    public required InitializeModuleCallback OnInitialize { get; set; }
    public int? NodeApiVersion { get; set; }
}
