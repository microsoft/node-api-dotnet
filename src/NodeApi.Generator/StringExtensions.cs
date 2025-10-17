// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NETFRAMEWORK || NETSTANDARD

using System;

/// <summary>
/// Fills in extension methods for the <see cref="string" /> class that are not present
/// in .NET Framework.
/// </summary>
internal static class StringExtensions
{
    public static bool Contains(this string s, char c) => s.Contains(c.ToString());

    public static bool StartsWith(this string s, char c) => s.StartsWith(c.ToString(), StringComparison.Ordinal);

    public static bool EndsWith(this string s, char c) => s.EndsWith(c.ToString(), StringComparison.Ordinal);
}

#endif
