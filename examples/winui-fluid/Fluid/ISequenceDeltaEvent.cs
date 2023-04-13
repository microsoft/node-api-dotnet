// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

public interface ISequenceDeltaEvent
{
    public bool IsLocal { get; }

    public string ClientId { get; }

    public IMergeTreeDeltaOpArgs OpArgs { get; }
}

public interface IMergeTreeDeltaOpArgs
{
    public IMergeTreeOp Op { get; }
}

public interface IMergeTreeOp
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
