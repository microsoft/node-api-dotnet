using System;

namespace NodeApi.TestCases;

public class ModuleInitializer
{
    [JSModule]
    public static JSValue Initialize(JSObject exports)
    {
        Console.WriteLine("Module.Initialize()");

        string? exportValue = Environment.GetEnvironmentVariable("TEST_DOTNET_MODULE_INIT_EXPORT");
        if (!string.IsNullOrEmpty(exportValue))
        {
            // Export a single string value instead of the `exports` object.
            return exportValue;
        }

        // Export a module with a JS property that doesn't map to any C# property.
        JSModuleBuilder<ModuleInitializer> moduleBuilder = new();
        moduleBuilder.AddProperty("test", JSValue.GetBoolean(true));
        return moduleBuilder.ExportModule(exports, new ModuleInitializer());
    }
}
