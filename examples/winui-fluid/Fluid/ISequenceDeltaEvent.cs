// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

[JSImport]
public struct SequenceDeltaEvent
{
    public bool IsLocal { get; set; }

    public string ClientId { get; set; }

    public MergeTreeDeltaOpArgs OpArgs { get; set; }
}

[JSImport]
public struct MergeTreeDeltaOpArgs
{
    public MergeTreeOp Op { get; set; }
}

[JSImport]
public struct MergeTreeOp
{
    public MergeTreeDeltaType Type { get; set; }

    public int? Pos1 { get; set; }

    public int? Pos2 { get; set; }

    public string? Seg { get; set; }
}

public enum MergeTreeDeltaType
{
    Insert = 0,
    Remove = 1,
    Annotate = 2,
    Group = 3,
}
