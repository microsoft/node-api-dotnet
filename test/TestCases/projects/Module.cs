// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable CA1050 // Declare types in namespaces
#pragma warning disable CA1822 // Mark members as static

using Microsoft.JavaScript.NodeApi;

// Tests exporting top-level properties on the JS module.
public static class ModuleProperties
{
    [JSExport]
    public static string ReadOnlyProperty { get; } = "ROProperty";

    [JSExport]
    public static string ReadWriteProperty { get; set; } = "RWProperty";

    [JSExport]
    public static string Method(string arg) => arg;
}

[JSExport]
public class ModuleClass
{
    public ModuleClass(string value)
    {
        Property = value;
    }

    public string Property { get; }

    public string Method(string arg) => arg;
}

namespace TestNamespace
{
    [JSExport("default")]
    public static class DefaultClass
    {
        public static string Method(string arg) => arg;

        public static TestEnum EnumProperty { get; set; }
    }

    [JSExport("ModuleEnum")]
    public enum TestEnum
    {
        None = 0,
        One = 1,
    }
}