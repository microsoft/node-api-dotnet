// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static JSRuntime;
using static NodejsEmbedding;

public class NodejsEmbeddingPlatformSettings
{
    public node_embedding_platform_flags? PlatformFlags { get; set; }
    public string[]? Args { get; set; }
    public HandleErrorCallback? OnError { get; set; }
    public ConfigurePlatformCallback? ConfigurePlatform { get; set; }

    public static JSRuntime JSRuntime => NodejsEmbedding.JSRuntime;

    public static unsafe implicit operator node_embedding_configure_platform_functor_ref(
        NodejsEmbeddingPlatformSettings? settings)
    {
        var confgurePlatform = new ConfigurePlatformCallback((config) =>
        {
            if (settings?.PlatformFlags != null)
            {
                JSRuntime.EmbeddingPlatformSetFlags(config, settings.PlatformFlags.Value)
                    .ThrowIfFailed();
            }
            settings?.ConfigurePlatform?.Invoke(config);
        });

        return new node_embedding_configure_platform_functor_ref(
            confgurePlatform,
            new node_embedding_configure_platform_callback(s_configurePlatformCallback));
    }
}
