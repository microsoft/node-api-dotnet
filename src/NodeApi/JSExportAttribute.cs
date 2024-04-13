// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Exports an item to JavaScript, optionally specifying the name of the export and whether
/// it is the default export of the module.
/// </summary>
/// <remarks>
/// When applied to an assembly, all public types in the assembly are exported, unless excluded
/// by another <see cref="JSExportAttribute"/>. When applied to a public type, all public members
/// of the type are exported, unless excluded by another <see cref="JSExportAttribute"/> or
/// unsupported for JS export.
/// <para />
/// A static class is exported as a JavaScript object, with public members of the static class
/// automatically exported as properties on the object.
/// <para/>
/// A non-static class or struct is exported as a JavaScript class, with static members
/// automatically exported as properties of the class constructor object, and instance members
/// automatically exported as properties of the class. .NET classes are passed by reference, such
/// that a JavaScript instance of the class is always backed by a .NET instance; structs are passed
/// by value.
/// <para/>
/// A static property exported without its containing class is exported as a JavaScript property
/// on the module object. (Note module-level properties are constant when using ES modules: their
/// value is only read once at module initialization time.)
/// <para/>
/// A static method exported without its containing class is exported as a JavaScript
/// function property on the module object.
/// <para/>
/// An enum, interface, or delegate is exported as part of the type definitions of the module but
/// does not have any runtime representation in the JavaScript exports.
/// </remarks>
/// <example>
/// <code>
/// // Export all public types in the current assembly (unless otherwise specified).
/// [assembly: JSExport]
/// 
/// // This type is not exported. The type-level attribute overrides the assembly-level attribute.
/// [JSExport(false)]
/// public static class NonExportedClass
/// {
///     // This method is exported as a static method on the module (not on the unexported class).
///     // The member-level attribute overrides any class or assembly-level attributes.
///     [JSExport]
///     public static void ModuleMethod();
/// }
/// 
/// // Without a type-level attribute, public types are exported by the assembly-level attribute.
/// public class ExportedClass
/// {
///     // The member-level attribute overrides any class or assembly-level attributes.
///     [JSExport(false)]
///     public void NonExportedMethod() {}
/// 
///     // Without a member-level attribute, public members are exported along with the type.
///     public void ExportedMethod() {}
/// }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Assembly |
    AttributeTargets.Interface |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Enum |
    AttributeTargets.Delegate |
    AttributeTargets.Constructor |
    AttributeTargets.Property |
    AttributeTargets.Method |
    AttributeTargets.Event
)]
public class JSExportAttribute : Attribute
{
    /// <summary>
    /// Exports an item to JavaScript, with an auto-generated JavaScript name.
    /// </summary>
    /// <remarks>
    /// By default, type names are unchanged while member names are camel-cased for JavaScript.
    /// </remarks>
    public JSExportAttribute() : this(export: true)
    {
    }

    /// <summary>
    /// Exports an item to JavaScript, with an explicit JavaScript name.
    /// </summary>
    /// <param name="name">Name of the item as exported to JavaScript.</param>
    /// <remarks>
    /// Names must be unique among a module's exports. Duplicates will result in a build error.
    /// <para/>
    /// Use the name "default" to create a default export.
    /// </remarks>
    public JSExportAttribute(string name) : this(export: true)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Excludes or includes an item for export to JavaScript.
    /// </summary>
    /// <param name="export">True to export the item (default), or false to exclude it from
    /// exports.</param>
    public JSExportAttribute(bool export)
    {
        Export = export;
    }

    /// <summary>
    /// Gets a value indicating whether the item is exported to JavaScript.
    /// </summary>
    public bool Export { get; }

    /// <summary>
    /// Gets the name of item as exported to JavaScript, or null if the JavaScript name is
    /// auto-generated. By default, type names are unchanged while member names are camel-cased
    /// for JavaScript.
    /// </summary>
    /// <remarks>
    /// Names must be unique among a module's exports. Duplicates will result in a build error.
    /// <para/>
    /// Use the name "default" to create a default export.
    /// </remarks>
    public string? Name { get; }
}
