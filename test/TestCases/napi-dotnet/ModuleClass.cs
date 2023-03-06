using System;

namespace NodeApi.TestCases;

#pragma warning disable CA1822 // Mark members as static

/// <summary>
/// An instance of this class is constructed when the module is loaded and disposed when the
/// module is unloaded. Public instance properties and methods on the module class are
/// automatically exported.
/// </summary>
[JSModule]
public class ModuleClass : IDisposable
{
    /// <summary>
    /// The module class must have a public constructor that takes either no parameters
    /// or a single JSContext parameter.
    /// </summary>
    public ModuleClass()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public string ModuleProperty { get; set; } = "test";

    public string ModuleMethod(string greeter)
    {
        string stringValue = greeter;
        return $"Hello {stringValue}!";
    }
}
