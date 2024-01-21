// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Default .Net to JS objects translation equality comparer.
/// </summary>
public class JSTranslationEqualityComparer : IEqualityComparer<object>
{
    /// <summary>
    /// Single instance of the comparer.
    /// </summary>
    public static JSTranslationEqualityComparer Instance { get; } = new ();

    private JSTranslationEqualityComparer()
    {
    }

    /// <inheritdoc />
    public new bool Equals(object? x, object? y)
    {
        if (x is null)
        {
            return y is null;
        }

        if (y is null)
        {
            return false;
        }

        // We should deliberately make different types not equal to avoid type definition mismatch.
        // Object of type B that matched through A.Equal(B) can be accessed in JS through description of type A, which is wrong.
        if (x.GetType() != y.GetType())
        {
            return false;
        }

        return x.Equals(y);
    }

    /// <inheritdoc />
    public int GetHashCode(object? obj)
    {
        if (obj is null)
        {
            return 0;
        }

        return obj.GetHashCode();
    }
}
