// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

public interface ISharedString
{
    public int GetLength();

    public string GetText(int? start = null, int? end = null);

    public void InsertText(int pos, string text, JSValue props = default);

    public void ReplaceText(int start, int end, string text, JSValue props = default);

    public void RemoveText(int start, int end);

    // SharedString has more methods, but they aren't currently used.
}
