// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.TestCases;

[JSExport]
public static class OptionalParameters
{
    public static string DefaultNull(string a, string? b = null)
    {
        b ??= "(null)";
        return $"{a},{b}";
    }

    public static string DefaultFalse(bool a, bool b = false)
    {
        return $"{a},{b}";
    }

    public static string DefaultZero(int a, int b = 0)
    {
        return $"{a},{b}";
    }

    public static string DefaultEmptyString(string a, string b = "")
    {
        return $"{a},{b}";
    }

    public static string Multiple(string a, string? b = null, int c = 0)
    {
        return $"{a},{b},{c}";
    }
}
