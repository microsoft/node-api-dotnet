// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

using static Microsoft.JavaScript.NodeApi.DotNetHost.ManagedHost;

/// <summary>
/// Represents a projection of a .NET type as JavaScript object. Also keeps track of extension
/// methods on the type and manages propagation of extension methods to derived types.
/// </summary>
internal class TypeProxy
{
    private JSReference? _jsType;
    private List<MethodInfo>? _extensionMethods = null;
    private List<TypeProxy>? _derivedTypes = null;

    public TypeProxy(NamespaceProxy ns, Type type)
    {
        Namespace = ns;
        Type = type;
        Name = GetTypeProxyName(type);

        if (type.IsGenericTypeDefinition)
        {
            ConstructedGenerics = new List<TypeProxy>();
        }
    }

    public static string GetTypeProxyName(Type type)
    {
        if (type.IsArray)
        {
            // Arrays are not supported, but this result is useful for diagnostics.
            return GetTypeProxyName(type.GetElementType()!) + "[]";
        }

        string prefix = type.IsNested ?
            GetTypeProxyName(type.DeclaringType!) + "." : string.Empty;

        if (type.IsGenericType && type.Name.IndexOf('`') > 0)
        {
            return prefix +
#if NETFRAMEWORK
                type.Name.Substring(0, type.Name.IndexOf('`')) + '$';
#else
                string.Concat(type.Name.AsSpan(0, type.Name.IndexOf('`')), "$");
#endif
        }
        else
        {
            return prefix + type.Name;
        }
    }

    /// <summary>
    /// Gets the namespace proxy that contains this type.
    /// </summary>
    public NamespaceProxy Namespace { get; }

    /// <summary>
    /// Gets the simple name of the type, not including the namespace or any
    /// generic type parameters or parameter count.
    /// </summary>
    /// <remarks>
    /// This value is used as the dictionary key in <see cref="NamespaceProxy.Types"/>.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the .NET type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the collection of proxies for types constructed from the current generic type
    /// definition, or null if this type proxy is not for a generic type definition.
    /// </summary>
    public ICollection<TypeProxy>? ConstructedGenerics { get; }

    /// <summary>
    /// Gets the JS value that represents this .NET type. If
    /// <see cref="TypeExporter.IsDelayLoadEnabled"/> is true), the type is exported the first
    /// time the value is requested. Returns null if the type could not be exported.
    /// </summary>
    public JSObject? Value
    {
        get
        {
            Export();
            return (JSObject?)_jsType?.GetValue();
        }
    }

    internal void Export()
    {
        if (_jsType == null)
        {
            if (Type.IsGenericTypeDefinition)
            {
                _jsType = Namespace.TypeExporter.ExportGenericTypeDefinition(
                    Type,
                    ExportConstructedGenericType);
            }
            else
            {
                _jsType = Namespace.TypeExporter.TryExportType(Type);
            }

            if (_jsType != null)
            {
                // Note it would be more efficient to include known extension methods when defining
                // the class for the intial export of the type. But adding extension methods
                // separately is simpler because it doesn't complicate the class-definition code.
                foreach (IGrouping<string, MethodInfo> extensionMethodGroup in ExtensionMethods
                    .GroupBy((m) => m.Name))
                {
                    ExportExtensionMethod(
                        extensionMethodGroup.Key,
                        extensionMethodGroup);
                }
            }
        }
    }

    /// <summary>
    /// For a class, gets all subclasses of the current class; for an interface, gets all
    /// interfaces that extend the current interface as well as classes that implement the
    /// current interface. (Indirect relationships are not included.)
    /// </summary>
    public IEnumerable<TypeProxy> DerivedTypes =>
        _derivedTypes ?? Enumerable.Empty<TypeProxy>();

    /// <summary>
    /// For a class, adds a subclass of the current class; for an interface, adds an interface
    /// that extends the current interface or a class that implements the current interface.
    /// Any extension methods on the current type are propagated to the derived type.
    /// </summary>
    /// <param name="derivedTypeProxy"></param>
    public void AddDerivedType(TypeProxy derivedTypeProxy)
    {
        if (Type.IsGenericTypeDefinition && derivedTypeProxy.Type.IsConstructedGenericType)
        {
            // It is is derived from a generic type constructed from this generic type definition.
            // TODO: Find or create the constructed generic type proxy, then add the derived type to it.
            return;
        }

        _derivedTypes ??= new List<TypeProxy>();

        _derivedTypes.Add(derivedTypeProxy);

        // Propagate extension methods from this type to the new derived type.
        foreach (MethodInfo extensionMethod in ExtensionMethods)
        {
            derivedTypeProxy.AddExtensionMethod(extensionMethod);
        }
    }

    /// <summary>
    /// Gets all known extension methods on the current type, including extension methods
    /// propagated from base classes and implemented interfaces.
    /// </summary>
    /// <remarks>
    /// Extension methods become "known" after their assembly is loaded, so as more assemblies
    /// are loaded the collection may grow.
    /// </remarks>
    public IEnumerable<MethodInfo> ExtensionMethods =>
        _extensionMethods ?? Enumerable.Empty<MethodInfo>();

    /// <summary>
    /// Adds an extension method on the current type. The extension method is also propagated
    /// to any derived types.
    /// </summary>
    /// <param name="extensionMethod"></param>
    public void AddExtensionMethod(MethodInfo extensionMethod)
    {
        if (_extensionMethods == null)
        {
            _extensionMethods = new List<MethodInfo>();
        }
        else if (_extensionMethods.Contains(extensionMethod))
        {
            // Extension methods may come from multiple interfaces that inherit from
            // a common base interface. Avoid duplicating the method in that case.
            return;
        }

        if (Type.IsGenericTypeDefinition)
        {
            if (extensionMethod.IsGenericMethodDefinition)
            {
                _extensionMethods.Add(extensionMethod);

                // Apply the generic extension method definition to all constructed generic types.
                foreach (TypeProxy genericTypeProxy in ConstructedGenerics!)
                {
                    genericTypeProxy.AddExtensionMethod(extensionMethod);
                }
            }
            else
            {
                Type extensionTargetType = extensionMethod.GetParameters()[0].ParameterType;
                if (extensionTargetType.GenericTypeArguments.Length ==
                    Type.GetGenericArguments().Length)
                {
                    // Apply the method to the matching specific constructed generic type.
                    Type genericType = Type.MakeGenericType(
                        extensionTargetType.GenericTypeArguments);
                    TypeProxy genericTypeProxy = GetOrCreateConstructedGeneric(
                        genericType);
                    genericTypeProxy.AddExtensionMethod(extensionMethod);
                }
            }
        }
        else
        {
            if (extensionMethod.IsGenericMethodDefinition && Type.IsConstructedGenericType)
            {
                // Are the extension method type args always the same as the target type args?
                if (extensionMethod.GetGenericArguments().Length !=
                    Type.GenericTypeArguments.Length)
                {
                    // Not supported.
                    return;
                }

                extensionMethod = extensionMethod.MakeGenericMethod(Type.GenericTypeArguments);
            }

            _extensionMethods.Add(extensionMethod);

            if (_jsType != null)
            {
                // The target .NET type has already been exported as a JS class. (Re-)Export the
                // method, which will (re-)define the method and callback on the JS prototype.
                ExportExtensionMethod(
                    extensionMethod.Name,
                    _extensionMethods.Where((m) => m.Name == extensionMethod.Name));
            }
        }

        // Propagate the extension method to all derived types.
        foreach (TypeProxy derivedType in DerivedTypes)
        {
            derivedType.AddExtensionMethod(extensionMethod);
        }
    }

    /// <summary>
    /// Gets the full name of the type.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Type.FullName!;

    private JSValue ExportConstructedGenericType(Type constructedGenericType)
    {
        TypeProxy genericTypeProxy = GetOrCreateConstructedGeneric(constructedGenericType);
        return genericTypeProxy.Value ?? default;
    }

    internal TypeProxy GetOrCreateConstructedGeneric(Type constructedGenericType)
    {
        TypeProxy? genericTypeProxy = ConstructedGenerics!.FirstOrDefault(
            (t) => t.Type == constructedGenericType);

        if (genericTypeProxy == null)
        {
            genericTypeProxy = new TypeProxy(Namespace, constructedGenericType);
            ConstructedGenerics!.Add(genericTypeProxy);

            // Propagate extension methods to the constructed generic type.
            foreach (MethodInfo extensionMethod in ExtensionMethods)
            {
                genericTypeProxy.AddExtensionMethod(extensionMethod);
            }
        }

        return genericTypeProxy;
    }

    private void ExportExtensionMethod(string name, IEnumerable<MethodInfo> extensionMethods)
    {
        Namespace.TypeExporter.ExportMethod(
            Type,
            name,
            extensionMethods,
            Value!.Value);
    }
}
