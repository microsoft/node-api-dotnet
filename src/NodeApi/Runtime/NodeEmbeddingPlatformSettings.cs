// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static NodeEmbedding;
using static NodejsRuntime;

public class NodeEmbeddingPlatformSettings
{
    public NodeEmbeddingPlatformFlags? PlatformFlags { get; set; }
    public string[]? Args { get; set; }
    public ConfigurePlatformCallback? ConfigurePlatform { get; set; }

    public static JSRuntime JSRuntime => NodeEmbedding.JSRuntime;

    public unsafe ConfigurePlatformCallback CreateConfigurePlatformCallback()
        => new((config) =>
        {
            if (PlatformFlags != null)
            {
                JSRuntime.EmbeddingPlatformConfigSetFlags(config, PlatformFlags.Value)
                    .ThrowIfFailed();
            }
            ConfigurePlatform?.Invoke(config);
        });
}
