// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace System.Collections.Generic;

// This is polyfill for the .Net Framework, in .Net Core 5+ this API already exists.
#if !NET5_0_OR_GREATER
internal class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new ();

    private ReferenceEqualityComparer()
    {

    }

    public bool Equals(object? x, object? y)
    {
        return object.ReferenceEquals(x, y);
    }

    public int GetHashCode(object? obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
