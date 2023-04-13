// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

[JSImport]
public struct SequenceDeltaEvent
{
    public bool IsLocal { get; }

    public string ClientId { get; }

    public MergeTreeDeltaOpArgs OpArgs { get; }
}

[JSImport]
public struct MergeTreeDeltaOpArgs
{
    public MergeTreeOp Op { get; }
}

[JSImport]
public struct MergeTreeOp
{
    public MergeTreeDeltaType Type { get; }

    public int? Pos1 { get; }

    public int? Pos2 { get; }

    public string? Seg { get; }
}

public enum MergeTreeDeltaType
{
    Insert = 0,
    Remove = 1,
    Annotate = 2,
    Group = 3,
}
