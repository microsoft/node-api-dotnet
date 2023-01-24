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
    private string _value = "test";

    /// <summary>
    /// The module class must have a public constructor that takes no parameters.
    /// </summary>
    public ModuleClass()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public JSValue ModuleProperty
    {
        get => _value;
        set => _value = (string)value;
    }

    public JSValue ModuleMethod(JSCallbackArgs args)
    {
        string stringValue = (string)args[0];
        return $"Hello {stringValue}!";
    }
}
