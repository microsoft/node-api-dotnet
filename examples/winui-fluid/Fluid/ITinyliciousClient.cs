// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

[JSImport]
public interface ITinyliciousClient
{
    public Task<TinyliciousContainerInfo> CreateContainer(JSValue containerSchema);

    public Task<TinyliciousContainerInfo> GetContainer(string id, JSValue containerSchema);
}

[JSImport]
public struct TinyliciousContainerInfo
{
    public IFluidContainer Container { get; set; }

    public ITinyliciousContainerServices Services { get; set; }
}

[JSImport]
public interface ITinyliciousContainerServices
{
    public ITinyliciousAudience Audience { get; set; }

}

[JSImport]
public interface ITinyliciousAudience
{
    public IDictionary<string, TinyliciousMember> GetMembers();

    public TinyliciousMember? GetMyself();
}

[JSImport]
public struct TinyliciousMember
{
    public string UserId { get; set; }

    public Connection[] Connections { get; set; }

    public string UserName { get; set; }
}

[JSImport]
public struct TinyliciousClientProps
{
    public TinyliciousConnectionConfig? Connection { get; set; }

    public TelemetryBaseLogger? Logger { get; set; }
}

[JSImport]
public struct TinyliciousConnectionConfig
{
    public int? Port { get; set; } 

    public string? Domain { get; set; }
}

[JSImport]
public struct TelemetryBaseLogger
{
    public bool SupportsTags { get; set; }

    // TODO: This should be a delegate type, when the marshaller supports it.
    public JSValue Send { get; set; }
}

[JSImport]
public struct TelemetryBaseEvent
{
    public string Category { get; set; }

    public string EventName { get; set; }
}
