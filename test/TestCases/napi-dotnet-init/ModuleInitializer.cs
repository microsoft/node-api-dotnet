// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.TestCases;

public class ModuleInitializer
{
    [JSModule]
    public static JSValue Initialize(JSRuntimeContext context, JSObject exports)
    {
        Console.WriteLine("Module.Initialize()");

        string? exportValue = Environment.GetEnvironmentVariable("TEST_DOTNET_MODULE_INIT_EXPORT");
        if (!string.IsNullOrEmpty(exportValue))
        {
            // Export a single string value instead of the `exports` object.
            return exportValue;
        }

        // Export a module with a JS property that doesn't map to any C# property.
        JSModuleBuilder<JSRuntimeContext> moduleBuilder = new();
        moduleBuilder.AddProperty("test", JSValue.GetBoolean(true));
        return moduleBuilder.ExportModule(context, exports);
    }
}
