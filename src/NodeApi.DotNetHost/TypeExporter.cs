// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

using static Microsoft.JavaScript.NodeApi.DotNetHost.ManagedHost;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Dynamically exports .NET types to JS.
/// </summary>
internal class TypeExporter
{
    /// <summary>
    /// Mapping from top-level namespace names like `System` and `Microsoft` to
    /// namespace objects that track child namespaces and types in the namespace.
    /// </summary>
    /// <remarks>
    /// This is a tree structure because each namespace object has another dictionary
    /// of child namespaces.
    /// </remarks>
    private readonly Dictionary<string, NamespaceProxy> _exportedNamespaces = new();

    /// <summary>
    /// Mapping from .NET Type objects to the JS objects that represent each type in JS.
    /// </summary>
    private readonly Dictionary<Type, JSReference> _exportedTypes = new();

    /// <summary>
    /// Marshaller that dynamically generates expressions and compiles delegates for
    /// API calls between .NET and JS.
    /// </summary>
    private readonly JSMarshaller _marshaller;

    /// <summary>
    /// Creates a new instance of the <see cref="TypeExporter" /> class.
    /// </summary>
    public TypeExporter()
    {
        _marshaller = JSMarshaller.Current;
    }

    /// <summary>
    /// Gets or sets a value indicating whether exporting of type members and their dependencies
    /// is delayed until first use. The default is true.
    /// </summary>
    public bool IsDelayLoadEnabled { get; set; } = true;

    public void ExportAssemblyTypes(Assembly assembly, JSObject exports)
    {
        Trace($"> ManagedHost.LoadAssemblyTypes({assembly.GetName().Name})");
        int count = 0;

        List<TypeProxy> typeProxies = new();
        List<MethodInfo> extensionMethods = new();
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsPublic)
            {
                // This also skips nested types which are NestedPublic but not Public.
                // Nested types will be exported as members of their containing type.
                continue;
            }

            string[] namespaceParts = type.Namespace?.Split('.') ?? [];
            if (namespaceParts.Length == 0)
            {
                Trace($"    Skipping un-namespaced type: {type.Name}");
                continue;
            }

            if (!_exportedNamespaces.TryGetValue(
                namespaceParts[0], out NamespaceProxy? parentNamespace))
            {
                // Export a new top-level namespace.
                parentNamespace = new NamespaceProxy(namespaceParts[0], null, this);
                exports[namespaceParts[0]] = parentNamespace.Value;
                _exportedNamespaces.Add(namespaceParts[0], parentNamespace);
            }

            for (int i = 1; i < namespaceParts.Length; i++)
            {
                if (!parentNamespace.Namespaces.TryGetValue(
                    namespaceParts[i], out NamespaceProxy? childNamespace))
                {
                    // Export a new child namespace.
                    childNamespace = new NamespaceProxy(
                        parentNamespace.Name + '.' + namespaceParts[i],
                        parentNamespace,
                        this);
                    parentNamespace.Namespaces.Add(namespaceParts[i], childNamespace);
                }

                parentNamespace = childNamespace;
            }

            string typeName = TypeProxy.GetTypeProxyName(type);

            // Multiple generic types may have the same name but with
            // different numbers of type args. They are only exported once.
            if (!(type.IsGenericTypeDefinition && parentNamespace.Types.ContainsKey(typeName)))
            {
                TypeProxy typeProxy = new(parentNamespace, type);
                parentNamespace.Types.Add(typeName, typeProxy);
                typeProxies.Add(typeProxy);
                Trace($"    {parentNamespace}.{typeName}");
                count++;
            }

            extensionMethods.AddRange(GetExtensionMethods(type));

            foreach (Type nestedType in type.GetNestedTypes())
            {
                string nestedTypeName = TypeProxy.GetTypeProxyName(nestedType);
                if (!(nestedType.IsGenericTypeDefinition &&
                    parentNamespace.Types.ContainsKey(nestedTypeName)))
                {
                    TypeProxy typeProxy = new(parentNamespace, nestedType);
                    parentNamespace.Types.Add(nestedTypeName, typeProxy);
                    typeProxies.Add(typeProxy);
                    Trace($"    {parentNamespace}.{typeName}");
                    count++;
                }
            }
        }

        // Register derived types after loading all types, because types within the assembly may
        // derive from each other.
        foreach (TypeProxy typeProxy in typeProxies)
        {
            RegisterDerivedType(typeProxy);
        }

        // Load extension methods after loading all types, because the extension methods can
        // depend on other types in the same assembly.
        foreach (MethodInfo extensionMethod in extensionMethods)
        {
            ExportExtensionMethod(extensionMethod);
        }

        Trace($"< ManagedHost.LoadAssemblyTypes({assembly.GetName().Name}) => {count} types");
    }

    private void RegisterDerivedType(TypeProxy derivedType, Type? baseOrInterfaceType = null)
    {
        if (baseOrInterfaceType == null)
        {
            if (derivedType.Type.BaseType != null &&
                derivedType.Type.BaseType != typeof(object))
            {
                RegisterDerivedType(derivedType, derivedType.Type.BaseType);
            }

            foreach (Type interfaceType in derivedType.Type.GetInterfaces())
            {
                RegisterDerivedType(derivedType, interfaceType);
            }
        }
        else
        {
            string baseOrInterfaceTypeName = TypeProxy.GetTypeProxyName(baseOrInterfaceType);

            NamespaceProxy? ns = GetNamespaceProxy(baseOrInterfaceType.Namespace!);
            if (ns == null)
            {
                Trace(
                    $"Namespace '{baseOrInterfaceType.Namespace}' not found for base type or " +
                    $"interface '{baseOrInterfaceTypeName}'.");
                return;
            }

            if (!ns.Types.TryGetValue(baseOrInterfaceTypeName, out TypeProxy? typeProxy))
            {
                Trace(
                    $"Base or interface type '{baseOrInterfaceTypeName}' not found for " +
                    $"derived type '{derivedType.Name}'.");
                return;
            }

            typeProxy.AddDerivedType(derivedType);
        }
    }

    private void ExportExtensionMethod(MethodInfo extensionMethod)
    {
        Type targetType = extensionMethod.GetParameters()[0].ParameterType;
        if (!IsExtensionTargetTypeSupported(targetType, extensionMethod.Name))
        {
            return;
        }

        string targetTypeName = TypeProxy.GetTypeProxyName(targetType);
        Trace($"    +{targetTypeName}.{extensionMethod.Name}()");

        // Target namespaces and types should be already loaded because either they are in the
        // current assembly (where types are loaded before extension methods) or in an assembly
        // this one depends on which would have been loaded already.
        NamespaceProxy? targetTypeNamespace = GetNamespaceProxy(targetType.Namespace!);
        if (targetTypeNamespace == null)
        {
            Trace(
                $"Namespace '{targetType.Namespace}' not found for extension method " +
                $"'{targetTypeName}.{extensionMethod.Name}()'.");
            return;
        }

        if (!targetTypeNamespace.Types.TryGetValue(targetTypeName, out TypeProxy? targetTypeProxy))
        {
            Trace(
                $"Target type '{targetTypeName}' not found for " +
                $"extension method '{extensionMethod.Name}'.");
            return;
        }

        Trace($"    +{targetTypeName}.{extensionMethod.Name}()");
        targetTypeProxy.AddExtensionMethod(extensionMethod);
    }

    private static bool IsExtensionTargetTypeSupported(Type targetType, string extensionMethodName)
    {
        // There are a lot of unsupported extension methods in the .NET BCL, so this tracing can be
        // noisy and is only enabled in DEBUG builds.

        if (targetType.IsValueType)
        {
            TraceDebug($"Struct target type '{targetType.FormatName()}' not supported for " +
                $"extension method '{extensionMethodName}'.");
            return false;
        }
        else if (targetType.IsPrimitive ||
            targetType == typeof(object) ||
            targetType == typeof(string) ||
            targetType == typeof(Type) ||
            targetType.Name == nameof(Task) || targetType.Name.StartsWith(nameof(Task) + '`'))
        {
            TraceDebug($"Target type '{targetType.FormatName()}' not supported for " +
                $"extension method '{extensionMethodName}'.");
            return false;
        }
        else if (targetType.IsArray)
        {
            TraceDebug($"Array target type '{targetType.FormatName()}' not supported for " +
                $"extension method '{extensionMethodName}'.");
            return false;
        }
        else if ((targetType.GetInterface(nameof(System.Collections.IEnumerable)) != null &&
            (targetType.Namespace == typeof(System.Collections.IEnumerable).Namespace ||
            targetType.Namespace == typeof(IEnumerable<>).Namespace)) ||
            targetType.Name.StartsWith("IAsyncEnumerable`") ||
            targetType.Name == nameof(Tuple) || targetType.Name.StartsWith(nameof(Tuple) + '`'))
        {
            TraceDebug($"Collection target type '{targetType.FormatName()}' not supported for " +
                $"extension method '{extensionMethodName}'.");
            return false;
        }

        return true;
    }

    public NamespaceProxy? GetNamespaceProxy(string ns)
    {
        string[] namespaceParts = ns.Split('.');
        if (!_exportedNamespaces.TryGetValue(
            namespaceParts[0], out NamespaceProxy? namespaceProxy))
        {
            return null;
        }

        foreach (string nsPart in namespaceParts.Skip(1))
        {
            if (!namespaceProxy.Namespaces.TryGetValue(nsPart, out NamespaceProxy? childNamespace))
            {
                return null;
            }

            namespaceProxy = childNamespace;
        }

        return namespaceProxy;
    }

    public TypeProxy? GetTypeProxy(Type type)
    {
        if (type.IsConstructedGenericType)
        {
            TypeProxy? typeDefinitionProxy = GetTypeProxy(type.GetGenericTypeDefinition());
            return typeDefinitionProxy?.GetOrCreateConstructedGeneric(type);
        }
        else
        {
            TypeProxy? typeProxy = null;
            string typeName = TypeProxy.GetTypeProxyName(type);
            NamespaceProxy? namespaceProxy = GetNamespaceProxy(type.Namespace ?? string.Empty);
            namespaceProxy?.Types.TryGetValue(typeName, out typeProxy);
            return typeProxy;
        }
    }

    /// <summary>
    /// Attempts to project a .NET type as a JS object.
    /// </summary>
    /// <param name="type">A type to export.</param>
    /// <param name="deferMembers">True to delay exporting of all type members until each one is
    /// accessed. If false, all type members are immediately exported, which may cascade to
    /// exporting many additional types referenced by the members, including members that are
    /// never actually used. The default is from <see cref="IsDelayLoadEnabled"/>.</param>
    /// <returns>A strong reference to a JS object that represents the exported type, or null
    /// if the type could not be exported.</returns>
    public JSReference? TryExportType(Type type, bool? deferMembers = null)
    {
        try
        {
            return ExportType(type, deferMembers ?? IsDelayLoadEnabled);
        }
        catch (NotSupportedException ex)
        {
            Trace($"Cannot export type {type}: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            Trace($"Failed to export type {type}: {ex}");
            return null;
        }
    }

    private JSReference ExportType(Type type, bool deferMembers)
    {
        if (!IsSupportedType(type))
        {
            throw new NotSupportedException("The type is not supported for JS export.");
        }
        else if (type.IsEnum)
        {
            return ExportEnum(type);
        }
        else if (type.IsGenericTypeDefinition)
        {
            throw new NotSupportedException(
                "Generic type definitions cannot be exported directly. " +
                $"Use {nameof(ExportGenericTypeDefinition)}() instead.");
        }
        else if (type.IsClass || type.IsInterface || type.IsValueType)
        {
            if (type.IsClass && type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
            {
                // Delegate types are not exported as type objects, but the JS marshaller can
                // still dynamically convert delegate instances to/from JS functions.
                throw new NotSupportedException("Delegate types are not exported.");
            }
            else
            {
                return ExportClass(type, deferMembers);
            }
        }
        else
        {
            throw new NotSupportedException("Unknown type kind.");
        }
    }

    private JSReference ExportClass(Type type, bool deferMembers)
    {
        string typeName = type.Name;

        if (_exportedTypes.TryGetValue(type, out JSReference? classObjectReference))
        {
            return classObjectReference;
        }

        Trace($"> {nameof(TypeExporter)}.ExportClass({type.FormatName()})");

        // Add a temporary null entry to the dictionary while exporting this type, in case the
        // type is encountered while exporting members. It will be non-null by the time this method returns
        // (or removed if an exception is thrown).
        _exportedTypes.Add(type, null!);
        try
        {
            bool isStatic = type.IsAbstract && type.IsSealed;
            Type classBuilderType =
                (type.IsValueType ? typeof(JSStructBuilder<>) : typeof(JSClassBuilder<>))
                .MakeGenericType(isStatic ? typeof(object) : type);

            object classBuilder;
            if (type.IsInterface || isStatic || type.IsValueType)
            {
                classBuilder = classBuilderType.CreateInstance(
                    new[] { typeof(string) }, new[] { type.Name });
            }
            else
            {
                JSCallbackDescriptor constructorDescriptor =
                    CreateConstructorDescriptor(type, deferMembers);
                classBuilder = classBuilderType.CreateInstance(
                    new[] { typeof(string), typeof(JSCallbackDescriptor) },
                    new object[] { type.Name, constructorDescriptor });
            }

            ExportProperties(type, classBuilder, deferMembers);
            ExportMethods(type, classBuilder, deferMembers);
            ExportNestedTypes(type, classBuilder);

            string defineMethodName = type.IsInterface ? "DefineInterface" :
                isStatic ? "DefineStaticClass" : type.IsValueType ? "DefineStruct" : "DefineClass";
            MethodInfo defineClassMethod = classBuilderType.GetInstanceMethod(defineMethodName);
            JSValue classObject = (JSValue)defineClassMethod.Invoke(
                classBuilder,
                defineClassMethod.GetParameters().Select((_) => (object?)null).ToArray())!;

            classObjectReference = new JSReference(classObject);
            _exportedTypes[type] = classObjectReference;
        }
        catch
        {
            // Clean up the temporary null entry.
            _exportedTypes.Remove(type);
            throw;
        }

        if (!deferMembers)
        {
            // Also export any types returned by properties or methods of this type, because
            // they might otherwise not be referenced by JS before they are used.
            ExportClassDependencies(type);
        }

        Trace($"< {nameof(TypeExporter)}.ExportClass()");
        return classObjectReference;
    }

    private JSCallbackDescriptor CreateConstructorDescriptor(Type type, bool defer)
    {
        ConstructorInfo[] constructors =
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsSupportedConstructor)
            .ToArray();

        JSCallbackDescriptor constructorDescriptor;
        if (defer)
        {
            // Create a descriptor that does deferred loading and resolution of overloads.
            // (It also handles the case when there is no overloading, only one constructor.)
            constructorDescriptor = JSCallbackOverload.CreateDescriptor(type.Name, () =>
            {
                return _marshaller.GetConstructorOverloads(constructors);
            });
        }
        else
        {
            if (constructors.Length == 1 &&
                !constructors[0].GetParameters().Any((p) => p.IsOptional))
            {
                // No deferral and no overload resolution - use the single callback descriptor.
                constructorDescriptor = new JSCallbackDescriptor(
                    type.Name,
                    _marshaller.BuildFromJSConstructorExpression(constructors[0]).Compile());
            }
            else
            {
                // Multiple constructors or optional parameters require overload resolution.
                constructorDescriptor = JSCallbackOverload.CreateDescriptor(
                    type.Name, _marshaller.GetConstructorOverloads(constructors));
            }
        }

        return constructorDescriptor;
    }

    private void ExportClassDependencies(Type type)
    {

        foreach (MemberInfo member in type.GetMembers
            (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (member is PropertyInfo property &&
                !JSMarshaller.IsConvertedType(property.PropertyType))
            {
                ExportTypeIfSupported(property.PropertyType, deferMembers: false);
            }

            if (member is MethodInfo method &&
                IsSupportedMethod(method) &&
                !JSMarshaller.IsConvertedType(method.ReturnType))
            {
                ExportTypeIfSupported(method.ReturnType, deferMembers: false);
            }

            if (member is MethodInfo interfaceMethod && type.IsInterface)
            {
                // Interface method parameter types must be exported in case the interface
                // will be implemented by JS.
                foreach (ParameterInfo interfaceMethodParameter in interfaceMethod.GetParameters())
                {
                    ExportTypeIfSupported(
                        interfaceMethodParameter.ParameterType, deferMembers: false);
                }

                ExportTypeIfSupported(interfaceMethod.ReturnType, deferMembers: false);
            }
        }
    }

    private void ExportTypeIfSupported(Type dependencyType, bool deferMembers)
    {
        if (dependencyType.IsArray || dependencyType.IsByRef)
        {
            ExportTypeIfSupported(dependencyType.GetElementType()!, deferMembers);
            return;
        }
        else if (dependencyType.IsGenericType)
        {
            Type genericTypeDefinition = dependencyType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(Nullable<>) ||
                genericTypeDefinition == typeof(Task<>) ||
                genericTypeDefinition.Namespace == typeof(IList<>).Namespace)
            {
                foreach (Type typeArg in dependencyType.GetGenericArguments())
                {
                    ExportTypeIfSupported(typeArg, deferMembers);
                }
                return;
            }
        }

        if (
#if !NETFRAMEWORK // TODO: Find an alternative for .NET Framework.
            !dependencyType.IsGenericTypeParameter &&
            !dependencyType.IsGenericMethodParameter &&
#endif
            IsSupportedType(dependencyType))
        {
            TypeProxy typeProxy = GetTypeProxy(dependencyType) ??
                throw new InvalidOperationException(
                    $"Type proxy not found for dependency: {dependencyType.FormatName()}");
            typeProxy.Export();
        }
    }

    private void ExportProperties(Type type, object classBuilder, bool defer)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo? addValuePropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty", new[] { typeof(string), typeof(JSPropertyAttributes) });
        MethodInfo addPropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });

        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        bool isStatic = type.IsAbstract && type.IsSealed;

        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Static |
            (isStatic ? default : BindingFlags.Instance)))
        {
            if (!IsSupportedType(property.PropertyType))
            {
                continue;
            }

            JSPropertyAttributes propertyAttributes = attributes;
            bool isStaticProperty = property.GetMethod?.IsStatic == true ||
                property.SetMethod?.IsStatic == true;

            Trace($"    {(isStaticProperty ? "static " : string.Empty)}{property.Name}");

            if (type.IsValueType && !isStaticProperty)
            {
                // Struct instance properties are not backed by getter/setter methods. The
                // entire struct is always passed by value. Properties are converted to/from
                // `JSValue` by the struct adapter method.
                propertyAttributes |= JSPropertyAttributes.Writable;
                addValuePropertyMethod.Invoke(
                    classBuilder,
                    new object[] { property.Name, propertyAttributes });
            }
            else
            {
                if (isStaticProperty)
                {
                    propertyAttributes |= JSPropertyAttributes.Static;
                }

                if (property.SetMethod != null)
                {
                    propertyAttributes |= JSPropertyAttributes.Writable;
                }

                JSCallback? getterCallback = null;
                if (property.GetMethod != null)
                {
                    if (defer)
                    {
                        // Set up a callback that defers generation of marshalling callbacks
                        // for the property until the first time it is accessed.
                        getterCallback = (args) =>
                        {
                            JSCallback getter =
                                _marshaller.BuildFromJSPropertyGetExpression(property).Compile();
                            JSCallback? setter = property.SetMethod == null ? null :
                                _marshaller.BuildFromJSPropertySetExpression(property).Compile();
                            args.ThisArg.DefineProperties(JSPropertyDescriptor.Accessor(
                                property.Name, getter, setter, propertyAttributes));

                            ExportTypeIfSupported(property.PropertyType, deferMembers: true);

                            return getter(args);
                        };
                    }
                    else
                    {
                        getterCallback = _marshaller.BuildFromJSPropertyGetExpression(property)
                            .Compile();
                    }
                }

                JSCallback? setterCallback = null;
                if (property.SetMethod != null)
                {
                    if (defer)
                    {
                        setterCallback = (args) =>
                        {
                            JSCallback? getter = property.GetMethod == null ? null :
                                _marshaller.BuildFromJSPropertyGetExpression(property).Compile();
                            JSCallback setter =
                                _marshaller.BuildFromJSPropertySetExpression(property).Compile();
                            args.ThisArg.DefineProperties(JSPropertyDescriptor.Accessor(
                                property.Name, getter, setter, propertyAttributes));

                            ExportTypeIfSupported(property.PropertyType, deferMembers: true);

                            return setter(args);
                        };
                    }
                    else
                    {
                        setterCallback = _marshaller.BuildFromJSPropertySetExpression(property)
                            .Compile();
                    }
                }

                addPropertyMethod.Invoke(
                    classBuilder,
                    new object?[]
                    {
                        property.Name,
                        getterCallback,
                        setterCallback,
                        propertyAttributes,
                        null,
                    });
            }
        }
    }

    private void ExportMethods(Type type, object classBuilder, bool defer)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo addMethodMethod = classBuilderType.GetInstanceMethod(
            "AddMethod",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });

        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        bool isStatic = type.IsAbstract && type.IsSealed;

        foreach (IGrouping<(bool IsStatic, string Name), MethodInfo> methodGroup in type.GetMethods(
            BindingFlags.Public | BindingFlags.Static |
            (isStatic ? default : BindingFlags.Instance))
            .Where((m) => !m.IsSpecialName)
            .GroupBy((m) => (m.IsStatic, m.Name)))
        {
            bool methodIsStatic = methodGroup.Key.IsStatic;
            string methodName = methodGroup.Key.Name;
            MethodInfo[] methods = methodGroup.Where(IsSupportedMethod).ToArray();
            if (methods.Length == 0)
            {
                continue;
            }

            if (methods.Any((m) => m.IsGenericMethodDefinition))
            {
                Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}<>()");

                // Exporting generic methods is always essentially deferred because the methods
                // cannot be fully exported until the type parameter(s) are known.
                MethodInfo[] genericMethods = methods.Where(
                    (m) => m.IsGenericMethodDefinition).ToArray();
                ExportGenericMethodDefinition(classBuilder, genericMethods);

                methods = methods.Where((m) => !m.IsGenericMethodDefinition).ToArray();
                if (methods.Length == 0)
                {
                    continue;
                }
            }

            JSCallbackDescriptor methodDescriptor = CreateMethodDescriptor(methods, false, defer);

            addMethodMethod.Invoke(
                classBuilder,
                new object?[]
                {
                    methodName,
                    methodDescriptor.Callback,
                    attributes | (methodIsStatic ? JSPropertyAttributes.Static : default),
                    methodDescriptor.Data,
                });
            if (!methodIsStatic && methodName == nameof(Object.ToString))
            {
                // Also export non-uppercased toString(), which is a special method in JavaScript.
                addMethodMethod.Invoke(
                    classBuilder,
                    new object?[]
                    {
                        "toString",
                        methodDescriptor.Callback,
                        attributes,
                        methodDescriptor.Data,
                    });
            }
        }
    }

    /// <summary>
    /// Exports or re-exports an instance method on an already-exported type. This is used when
    /// adding extension methods to a type.
    /// </summary>
    /// <param name="type">The .NET type the </param>
    /// <param name="methodName">Name of the method to export.</param>
    /// <param name="extensionMethods">Collection of known extension methods having the same name
    /// as the method, to incorporate in overload resolution of the method.</param>
    /// <param name="jsType">The JS class object that was originally exported for the type.</param>
    /// <param name="deferExport">True to delay exporting of the method until it is accessed.
    /// If false, all method overloads (including extension methods) are immediately exported,
    /// which may cascade to exporting many additional types referenced by the methods.
    /// The default is from <see cref="IsDelayLoadEnabled"/>.</param>
    public void ExportMethod(
        Type type,
        string methodName,
        IEnumerable<MethodInfo> extensionMethods,
        JSObject jsType,
        bool? deferExport = null)
    {
        Trace($"> {nameof(TypeExporter)}.ExportMethod({type.FormatName()}.{methodName})");

        // Find instance methods on the type with the given name, and
        // concatenate with extension methods (with the same name).
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where((m) => m.Name == methodName)
            .Concat(extensionMethods)
            .ToArray();

        // Create a callback descriptor (deferred or not) for all the overloads.
        JSCallbackDescriptor methodDescriptor = CreateMethodDescriptor(
            methods, staticAsExtensions: true, deferExport ?? IsDelayLoadEnabled);

        // Call DefineProperty on the JS class object with the callback descriptor;
        // this will either create a new method or redefine the existing method.
        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        ((JSObject)jsType["prototype"]).DefineProperties(JSPropertyDescriptor.Function(
            methodName, methodDescriptor.Callback, attributes, methodDescriptor.Data));

        Trace($"< {nameof(TypeExporter)}.ExportMethod()");
    }

    private JSCallbackDescriptor CreateMethodDescriptor(
        MethodInfo[] methods, bool staticAsExtensions, bool defer)
    {
        JSCallbackDescriptor methodDescriptor;
        string methodName = methods[0].Name;
        bool methodIsStatic = methods[0].IsStatic && !staticAsExtensions;
        Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}()" +
            (methods.Length > 1 ? " [" + methods.Length + "]" : string.Empty));

        if (defer)
        {
            // Create a descriptor that does deferred loading and resolution of overloads.
            // (It also handles the case when there is no overloading, only one method.)
            methodDescriptor = JSCallbackOverload.CreateDescriptor(methodName, () =>
            {
                JSCallbackOverload[] overloads = _marshaller.GetMethodOverloads(
                    methods, staticAsExtensions);

                ExportTypeIfSupported(methods[0].ReturnType, deferMembers: true);

                return overloads;
            });
        }
        else
        {
            if (methods.Length == 1 &&
                !methods[0].GetParameters().Any((p) => p.IsOptional))
            {
                // No deferral and no overload resolution - use the single callback descriptor.
                methodDescriptor = _marshaller.BuildFromJSMethodExpression(methods[0]).Compile();
            }
            else
            {
                // Multiple overloads or optional parameters require overload resolution.
                methodDescriptor = JSCallbackOverload.CreateDescriptor(
                    methodName, _marshaller.GetMethodOverloads(methods));
            }
        }
        return methodDescriptor;

    }

    private void ExportNestedTypes(Type type, object classBuilder)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo? addValuePropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty", new[] { typeof(string), typeof(JSValue), typeof(JSPropertyAttributes) });

        JSPropertyAttributes propertyAttributes = JSPropertyAttributes.Static |
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;

        foreach (Type nestedType in type.GetNestedTypes())
        {
            if (!nestedType.IsNestedPublic || !IsSupportedType(nestedType))
            {
                continue;
            }

            JSValue? nestedTypeValue = GetTypeProxy(nestedType)?.Value;
            if (nestedTypeValue != null)
            {
                addValuePropertyMethod.Invoke(
                    classBuilder,
                    new object[]
                    {
                        nestedType.Name,
                        nestedTypeValue,
                        propertyAttributes,
                    });
            }
        }
    }

    private JSReference ExportEnum(Type type)
    {
        Trace($"> {nameof(TypeExporter)}.ExportEnum({type.FormatName()})");

        if (_exportedTypes.TryGetValue(type, out JSReference? enumObjectReference))
        {
            return enumObjectReference;
        }

        JSClassBuilder<object> enumBuilder = new(type.Name);

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            enumBuilder.AddProperty(
                field.Name,
                (JSValue)Convert.ToInt64(field.GetRawConstantValue()),
                JSPropertyAttributes.Static | JSPropertyAttributes.Enumerable);
        }

        JSValue enumObject = enumBuilder.DefineEnum();
        enumObjectReference = new JSReference(enumObject);
        _exportedTypes.Add(type, enumObjectReference);

        Trace($"< {nameof(TypeExporter)}.ExportEnum()");
        return enumObjectReference;
    }

    private static bool IsSupportedType(Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        if (type.IsPointer ||
            type == typeof(void) ||
            type.Namespace == "System.Reflection" ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Memory<>)) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>)) ||
            (type.Namespace?.StartsWith("System.Collections.") == true && !type.IsGenericType) ||
            (type.Namespace?.StartsWith("System.Threading.") == true && type != typeof(Task) &&
            !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))))
        {
            return false;
        }

#if !NETFRAMEWORK
        if (type.IsByRefLike)
        {
            // ref structs like Span<T> aren't yet supported.
            return false;
        }
#endif

        if (typeof(Stream).IsAssignableFrom(type))
        {
            // Streams should be projected as Duplex.
            return false;
        }

        if (type.Assembly == typeof(JSValue).Assembly)
        {
            return false;
        }

        return true;
    }

    private static bool IsSupportedConstructor(ConstructorInfo constructor)
    {
        return constructor.GetParameters().All(IsSupportedParameter);
    }

    private static bool IsSupportedMethod(MethodInfo method)
    {
        return method.CallingConvention != CallingConventions.VarArgs &&
            method.Name != nameof(System.Collections.IEnumerable.GetEnumerator) &&
            method.GetParameters().All(IsSupportedParameter) &&
            (method.ReturnType == typeof(void) || IsSupportedParameter(method.ReturnParameter));
    }

    private static bool IsSupportedParameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;

        if (parameter.Position < 0 && parameterType.IsByRef)
        {
            // Ref return values are not supported.
            return false;
        }

        return IsSupportedType(parameterType);
    }

    public JSReference ExportGenericTypeDefinition(
        Type type,
        Func<Type, JSValue> exportConstructedGeneric)
    {
        // TODO: Support multiple generic types with same name and differing type arg counts.

        if (_exportedTypes.TryGetValue(type, out JSReference? genericTypeFunctionReference))
        {
            return genericTypeFunctionReference;
        }

        Trace($"> {nameof(TypeExporter)}.ExportGenericTypeDefinition({type.FormatName()})");

        // A generic type definition is exported as a function that constructs the
        // generic type from type args.
        JSFunction function = new(
            (args) => MakeGenericType(args, exportConstructedGeneric), callbackData: type);

        // Override the type's toString() to return the formatted generic type name.
        ((JSValue)function).SetProperty("toString", new JSFunction(() => type.FormatName()));

        genericTypeFunctionReference = new JSReference(function);
        _exportedTypes.Add(type, genericTypeFunctionReference);

        Trace($"< {nameof(TypeExporter)}.ExportGenericTypeDefinition({type.FormatName()})");

        return genericTypeFunctionReference;
    }

    /// <summary>
    /// Makes and exports a constructed generic type from a generic type definition and
    /// type arguments.
    /// </summary>
    /// <param name="args">Type arguments passed as JS values.</param>
    /// <param name="exportConstructedGeneric">A callback that exports a constructed generic
    /// type to JS and returns the exported JS class object.
    /// <returns>A JS value that represents the constructed generic type.</returns>
    private static JSValue MakeGenericType(
        JSCallbackArgs args,
        Func<Type, JSValue> exportConstructedGeneric)
    {
        Type genericTypeDefinition = args.Data as Type ??
            throw new ArgumentException("Missing generic type definition.");

        Type[] typeArgs = new Type[args.Length];
        for (int i = 0; i < typeArgs.Length; i++)
        {
            typeArgs[i] = args[i].TryUnwrap() as Type ??
                throw new ArgumentException($"Invalid generic type argument at position {i}.");
        }

        Type genericType;
        try
        {
            genericType = genericTypeDefinition.MakeGenericType(typeArgs);
        }
        catch (Exception ex)
        {
            throw new JSException(
                $"Failed to make generic type {genericTypeDefinition.FormatName()} with supplied " +
                $"type arguments: [{string.Join(", ", typeArgs.Select((t) => t.FormatName()))}]. " +
                ex.Message,
                ex);
        }

        return exportConstructedGeneric(genericType);
    }

    private void ExportGenericMethodDefinition(object classBuilder, MethodInfo[] methods)
    {
        // Add method that is a function that makes the generic method.
        MethodInfo addMethodMethod = classBuilder.GetType().GetInstanceMethod(
            "AddMethod",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });
        addMethodMethod.Invoke(
            classBuilder,
            new object[]
            {
                methods[0].Name + '$',
                (JSCallback)MakeGenericMethod,
                JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable |
                    (methods[0].IsStatic ? JSPropertyAttributes.Static : default),
                methods,
            });
    }

    private JSValue MakeGenericMethod(JSCallbackArgs args)
    {
        MethodInfo[] genericMethodDefinitions = args.Data as MethodInfo[] ??
            throw new ArgumentException("Missing generic type definition.");

        Type[] typeArgs = new Type[args.Length];
        for (int i = 0; i < typeArgs.Length; i++)
        {
            typeArgs[i] = args[i].TryUnwrap() as Type ??
                throw new ArgumentException($"Invalid generic type argument at position {i}.");
        }

        MethodInfo[] matchingMethodDefinitions = genericMethodDefinitions
            .Where((m) => m.GetGenericArguments().Length == typeArgs.Length)
            .ToArray();

        if (matchingMethodDefinitions.Length == 0)
        {
            throw new JSException(
                "Incorrect number of type arguments for method: +" +
                genericMethodDefinitions[0].Name);
        }

        MethodInfo[] matchingMethods;
        try
        {
            matchingMethods = genericMethodDefinitions.Select((m) => m.MakeGenericMethod(typeArgs))
                .ToArray();
        }
        catch (Exception ex)
        {
            throw new JSException(
                $"Failed to make generic method {genericMethodDefinitions[0].Name} with supplied " +
                $"type arguments: [{string.Join(", ", typeArgs.Select((t) => t.FormatName()))}]. " +
                ex.Message,
                ex);
        }

        JSCallbackDescriptor descriptor = CreateMethodDescriptor(matchingMethods, false, defer: false);
        JSFunction function = new(descriptor.Callback, descriptor.Data);

        if (!args.ThisArg.IsUndefined())
        {
            function = function.Bind(args.ThisArg);
        }

        return function;
    }

    private static IEnumerable<MethodInfo> GetExtensionMethods(Type type)
    {
        if (!type.IsDefined(typeof(ExtensionAttribute), inherit: false))
        {
            return Enumerable.Empty<MethodInfo>();
        }

        return type.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where((m) => m.IsDefined(typeof(ExtensionAttribute), inherit: false));
    }
}
