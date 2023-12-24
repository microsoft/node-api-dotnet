// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.TestCases;

public static class ModuleExports
{
    private static string s_value = "test";

    [JSExport]
    public static JSValue MergedProperty
    {
        get => s_value;
        set => s_value = (string)value;
    }

    [JSExport]
    public static JSValue MergedMethod(JSCallbackArgs args)
    {
        string stringValue = (string)args[0];
        return $"Hello {stringValue}!";
    }
}
