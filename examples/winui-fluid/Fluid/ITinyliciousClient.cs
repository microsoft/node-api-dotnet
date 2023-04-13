// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

public interface ITinyliciousClient
{
    public Task<TinyliciousContainerInfo> CreateContainer(JSValue containerSchema);

    public Task<TinyliciousContainerInfo> GetContainer(string id, JSValue containerSchema);
}

public struct TinyliciousContainerInfo
{
    public IFluidContainer Container { get; set; }

    public ITinyliciousContainerServices Services { get; set; }
}

public interface ITinyliciousContainerServices
{
    public ITinyliciousAudience Audience { get; set; }

}

public interface ITinyliciousAudience
{
    public IDictionary<string, TinyliciousMember> GetMembers();

    public TinyliciousMember? GetMyself();
}

public struct TinyliciousMember
{
    public string UserId { get; set; }

    public Connection[] Connections { get; set; }

    public string UserName { get; set; }
}
