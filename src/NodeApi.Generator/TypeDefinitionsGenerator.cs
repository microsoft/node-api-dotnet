// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.DotNetHost.JSMarshaller;
using NullabilityInfo = System.Reflection.NullabilityInfo;

namespace Microsoft.JavaScript.NodeApi.Generator;

// This class is packaged with the analyzer, but runs as a separate command-line tool.
#pragma warning disable RS1035 // Do not do file IO in alayzers

/// <summary>
/// Generates TypeScript type definitions for .NET APIs exported to JavaScript.
/// </summary>
/// <remarks>
/// If some specific types or static methods in the assembly are tagged with
/// <see cref="JSExportAttribute"/>, then type definitions are generated for only those items.
/// Otherwise, type definitions are generated for all public APIs in the assembly that are
/// usable by JavaScript.
/// <para />
/// If there is a documentation comments XML file in the same directory as the assembly then
/// the doc-comments will be automatically included with the generated type definitions.
/// </remarks>
public class TypeDefinitionsGenerator : SourceGenerator
{
    public enum ModuleType
    {
        None,
        CommonJS,
        ES,
    }

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load an assembly file and type definitions as an ES module with
    /// one simple import statement.
    /// </summary>
    /// <remarks>
    /// The `__filename` and `__dirname` values are computed for compatibility with ES modules;
    /// they are equivalent to those predefined values defined for CommonJS modules.
    /// </remarks>
    private const string LoadAssemblyMJS = @"
import dotnet from 'node-api-dotnet';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const assemblyName = path.basename(__filename, '.js');
const assemblyFilePath = path.join(__dirname, assemblyName + '.dll');
dotnet.load(assemblyFilePath);
";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load an assembly file and type definitions as a CommonJS module
    /// with one simple require statement.
    /// </summary>
    private const string LoadAssemblyCJS = @"
const dotnet = require('node-api-dotnet');
const path = require('node:path');

const assemblyName = path.basename(__filename, '.js');
const assemblyFilePath = path.join(__dirname, assemblyName + '.dll');
dotnet.load(assemblyFilePath);
";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load a system assembly and type definitions as an ES module
    /// with one simple import statement.
    /// </summary>
    private const string LoadSystemAssemblyMJS = @"
import dotnet from 'node-api-dotnet';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const assemblyName = path.basename(__filename, '.js');
dotnet.load(assemblyName);
";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load a system assembly and type definitions as a CommonJS
    /// module with one simple require statement.
    /// </summary>
    private const string LoadSystemAssemblyCJS = @"
const dotnet = require('node-api-dotnet');
const path = require('node:path');

const assemblyName = path.basename(__filename, '.js');
dotnet.load(assemblyName);
";

    private const string UndefinedTypeSuffix = " | undefined";

    private static readonly Regex s_newlineRegex = new("\n *");

    private readonly NullabilityInfoContext _nullabilityContext = new();

    private readonly Assembly _assembly;
    private readonly IDictionary<string, Assembly> _referenceAssemblies;
    private readonly HashSet<string> _imports;
    private readonly XDocument? _assemblyDoc;
    private bool _exportAll;
    private bool _autoCamelCase;
    private bool _emitDisposable;
    private bool _emitDuplex;
    private bool _emitType;
    private readonly bool _suppressWarnings;

    public static void GenerateTypeDefinitions(
        string assemblyPath,
        IEnumerable<string> referenceAssemblyPaths,
        IEnumerable<string> systemReferenceAssemblyDirectories,
        string typeDefinitionsPath,
        ModuleType loaderModuleType,
        bool isSystemAssembly = false,
        bool suppressWarnings = false)
    {
        // Create a metadata load context that includes a resolver for .NET system assemblies
        // along with the target assembly.

        // Resolve all assemblies in all the system reference assembly directories.
        string[] systemAssemblies = systemReferenceAssemblyDirectories
            .SelectMany((d) => Directory.GetFiles(d, "*.dll"))
            .ToArray();

        // Drop reference assemblies that are already in any system ref assembly directories.
        // (They would only support older framework versions.)
        referenceAssemblyPaths = referenceAssemblyPaths.Where(
            (r) => !systemAssemblies.Any((a) => Path.GetFileName(a).Equals(
                Path.GetFileName(r), StringComparison.OrdinalIgnoreCase)));

        PathAssemblyResolver assemblyResolver = new(
            new[] { typeof(object).Assembly.Location }
            .Concat(systemAssemblies)
            .Concat(referenceAssemblyPaths)
            .Append(assemblyPath));
        using MetadataLoadContext loadContext = new(
            assemblyResolver, typeof(object).Assembly.GetName().Name);

        Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

        Dictionary<string, Assembly> referenceAssemblies = new();
        foreach (string referenceAssemblyPath in referenceAssemblyPaths)
        {
            Assembly referenceAssembly = loadContext.LoadFromAssemblyPath(referenceAssemblyPath);
            string referenceAssemblyName = referenceAssembly.GetName().Name!;
            referenceAssemblies.Add(referenceAssemblyName, referenceAssembly);
        }

        XDocument? assemblyDoc = null;
        string? assemblyDocFilePath = Path.ChangeExtension(assemblyPath, ".xml");
        if (!File.Exists(assemblyDocFilePath))
        {
            // Some doc XML files are missing the first-level namespace prefix.
            string assemblyFileName = Path.GetFileNameWithoutExtension(assemblyPath);
            assemblyDocFilePath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
#if NETFRAMEWORK
                assemblyFileName.Substring(assemblyFileName.IndexOf('.') + 1) + ".xml");
#else
                string.Concat(assemblyFileName.AsSpan(assemblyFileName.IndexOf('.') + 1), ".xml"));
#endif
        }

        if (File.Exists(assemblyDocFilePath))
        {
            assemblyDoc = XDocument.Load(assemblyDocFilePath);
        }

        string[] referenceAssemblyNames = referenceAssemblyPaths
            .Select((r) => Path.GetFileNameWithoutExtension(r))
            .ToArray();

        try
        {
            TypeDefinitionsGenerator generator = new(
                assembly,
                assemblyDoc,
                referenceAssemblies,
                suppressWarnings);
            SourceText generatedSource = generator.GenerateTypeDefinitions();

            File.WriteAllText(typeDefinitionsPath, generatedSource.ToString());

            if (typeDefinitionsPath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            {
                string pathWithoutExtension = typeDefinitionsPath.Substring(
                    0, typeDefinitionsPath.Length - 5);
                string loaderModulePath = pathWithoutExtension + ".js";
                string header = generator.GetGeneratedFileHeader();

                if (loaderModuleType == ModuleType.ES)
                {
                    File.WriteAllText(loaderModulePath, header +
                        (isSystemAssembly ? LoadSystemAssemblyMJS : LoadAssemblyMJS));
                }
                else if (loaderModuleType == ModuleType.CommonJS)
                {
                    File.WriteAllText(loaderModulePath, header +
                        (isSystemAssembly ? LoadSystemAssemblyCJS : LoadAssemblyCJS));
                }
            }
        }
        finally
        {
            SymbolExtensions.Reset();
        }
    }

    public TypeDefinitionsGenerator(
        Assembly assembly,
        XDocument? assemblyDoc,
        IDictionary<string, Assembly> referenceAssemblies,
        bool suppressWarnings)
    {
        _assembly = assembly;
        _assemblyDoc = assemblyDoc;
        _referenceAssemblies = referenceAssemblies;
        _imports = new HashSet<string>();
        _suppressWarnings = suppressWarnings;
    }

    public override void ReportDiagnostic(Diagnostic diagnostic)
    {
        if (_suppressWarnings && diagnostic.Severity == DiagnosticSeverity.Warning)
        {
            return;
        }

        string severity = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };
        Console.WriteLine($"{severity} {diagnostic.Id}: {diagnostic.GetMessage()}");
    }

    private string GetGeneratedFileHeader()
    {
        string targetName = _assembly.GetName()!.Name!;
        Version? targetVersion = _assembly.GetName().Version;
        string generatorName = typeof(TypeDefinitionsGenerator).Assembly.GetName()!.Name!;
        Version? generatorVersion = typeof(TypeDefinitionsGenerator).Assembly.GetName().Version;
        return $"// Generated for: {targetName} {targetVersion}{Environment.NewLine}" +
            $"// Generated by: {generatorName} {generatorVersion}{Environment.NewLine}" +
            $"/* eslint-disable */{Environment.NewLine}";
    }

    public SourceText GenerateTypeDefinitions(bool? autoCamelCase = null)
    {
        var s = new SourceBuilder();
        s += GetGeneratedFileHeader();

        // Imports will be inserted here later, after the used references are determined.
        int importsIndex = s.Length;

        _exportAll = !AreAnyItemsExported();

        // Default to camel-case for modules, preserve case otherwise.
        _autoCamelCase = autoCamelCase ?? !_exportAll;

        s++;

        // Declare this types as members of the 'node-api-dotnet' module.
        // This causes types across multiple .NET assemblies to be merged into
        // a shared .NET namespace hierarchy.
        s += "declare module 'node-api-dotnet' {";

        foreach (Type type in _assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (IsTypeExported(type))
            {
                ExportType(ref s, type);
            }
            else
            {
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.Static))
                {
                    if (IsMemberExported(member))
                    {
                        ExportMember(ref s, member);
                    }
                }
            }
        }

        s += "}";

        GenerateSupportingInterfaces(ref s, importsIndex);

        if (_imports.Count > 0)
        {
            StringBuilder insertBuilder = new();
            insertBuilder.AppendLine();
            foreach (string referenceName in _imports)
            {
                insertBuilder.AppendLine($"import './{referenceName}';");
            }
            s.Insert(importsIndex, insertBuilder.ToString());
        }

        // Re-export this module's types in a module that matches the assembly name.
        // This supports AOT when the module is directly imported by name instead of
        // importing via the .NET host.
        s++;
        s += $"declare module '{_assembly.GetName().Name}' {{";
        s += "export * from 'node-api-dotnet';";
        s += "}";

        return s;
    }

    private bool IsTypeExported(Type type)
    {
        // Types not in the current assembly are not exported from this TS module.
        // (But support mscorlib and System.Runtime forwarding to System.Private.CoreLib.)
        if (type.Assembly != _assembly &&
            !(type.Assembly.GetName().Name == "System.Private.CoreLib" &&
            (_assembly.GetName().Name == "mscorlib" || _assembly.GetName().Name == "System.Runtime")))
        {
            return false;
        }

        if (_exportAll || type.GetCustomAttributesData().Any((a) =>
            a.AttributeType.FullName == typeof(JSModuleAttribute).FullName ||
            a.AttributeType.FullName == typeof(JSExportAttribute).FullName))
        {
            return true;
        }

        if (type.IsNested)
        {
            return IsTypeExported(type.DeclaringType!);
        }

        return false;
    }

    private static bool IsMemberExported(MemberInfo member)
    {
        return member.GetCustomAttributesData().Any((a) =>
            a.AttributeType.FullName == typeof(JSExportAttribute).FullName);
    }

    private static bool IsCustomModuleInitMethod(MemberInfo member)
    {
        return member is MethodInfo && member.GetCustomAttributesData().Any((a) =>
            a.AttributeType.FullName == typeof(JSModuleAttribute).FullName);
    }

    private bool AreAnyItemsExported()
    {
        foreach (Type type in _assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (IsTypeExported(type))
            {
                return true;
            }
            else
            {
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
                {
                    if (IsMemberExported(member))
                    {
                        return true;
                    }
                    else if (IsCustomModuleInitMethod(member))
                    {
                        throw new InvalidOperationException(
                            "Cannot generate type definitions for an assembly with a " +
                            "custom [JSModule] initialization method.");
                    }
                }
            }
        }

        return false;
    }

    public string GenerateTypeDefinition(Type type)
    {
        SourceBuilder s = new();
        ExportType(ref s, type);
        return s.ToString();
    }

    private void ExportType(ref SourceBuilder s, Type type)
    {
        if (type.IsClass && type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
        {
            GenerateDelegateDefinition(ref s, type);
        }
        else if (type.IsEnum || (type.IsClass && type.BaseType?.FullName == typeof(Enum).FullName))
        {
            GenerateEnumDefinition(ref s, type);
        }
        else if (type.IsClass || type.IsInterface || type.IsValueType)
        {
            GenerateClassDefinition(ref s, type);
        }
        else
        {
        }
    }

    private void ExportMember(ref SourceBuilder s, MemberInfo member)
    {
        if (member is MethodInfo method)
        {
            s++;
            GenerateDocComments(ref s, method);
            string exportName = GetExportName(method);
            string parameters = GetTSParameters(method.GetParameters());
            string returnType = GetTSType(method.ReturnParameter);
            s += $"export function {exportName}({parameters}): {returnType};";
        }
        else if (member is PropertyInfo property)
        {
            s++;
            GenerateDocComments(ref s, property);
            string exportName = GetExportName(property);
            string propertyType = GetTSType(property);
            string varKind = property.SetMethod == null ? "const " : "var ";
            s += $"export {varKind}{exportName}: {propertyType};";
        }
        else
        {
            // TODO: Events, const fields?
        }
    }

    private void GenerateSupportingInterfaces(ref SourceBuilder s, int insertIndex)
    {
        if (_emitType)
        {
            // This interface is named `IType` rather than `Type` primarily to distinguish it
            // from tye .NET System.Type type, which could also be accessed from JS.
            s.Insert(insertIndex, @"
/** A JavaScript projection of a .NET type. */
interface IType<T> {
	/**
	 * Constructs a new instance of the type.
	 * (Not available for static class or interface types.)
	 */
	new?(...args: any[]): T;

	/** Gets the full name of the .NET type. */
	toString(): string;
}
");
        }

        if (_emitDisposable)
        {
            s.Insert(insertIndex, @"
interface IDisposable {
	dispose(): void;
}
");
        }

        if (_emitDuplex)
        {
            s.Insert(insertIndex, @"
import { Duplex } from 'stream';
");
        }
    }
    private static string GetGenericParams(Type type)
    {
        string genericParams = string.Empty;
        if (type.IsGenericTypeDefinition)
        {
            Type[] typeArgs = type.GetGenericArguments();
            genericParams = string.Join(", ", typeArgs.Select((t) => t.Name));
            genericParams = $"${typeArgs.Length}<{genericParams}>";
        }
        return genericParams;
    }

    private static string GetGenericParams(MethodInfo method)
    {
        string genericParams = string.Empty;
        if (method.IsGenericMethodDefinition)
        {
            genericParams = string.Join(", ", method.GetGenericArguments().Select((t) => t.Name));
            genericParams = $"<{genericParams}>";
        }
        return genericParams;
    }

    private void GenerateDelegateDefinition(ref SourceBuilder s, Type type)
    {
        s++;
        BeginNamespace(ref s, type);

        string exportName = GetExportName(type);
        MethodInfo invokeMethod = type.GetMethod(nameof(Action.Invoke))!;

        if (type.IsGenericTypeDefinition)
        {
            GenerateGenericTypeFactory(ref s, type);

            GenerateDocComments(ref s, type);

            Type[] typeArgs = type.GetGenericArguments();
            string typeParams = string.Join(", ", typeArgs.Select((t) => t.Name));
            s += $"export interface {exportName}$${typeArgs.Length}<{typeParams}> {{";

            string invokeArgs = string.Join(", ", invokeMethod.GetParameters().Select(
                (p) => $"{p.Name}: {GetTSType(p)}"));
            string invokeRet = GetTSType(invokeMethod.ReturnParameter);
            s += $"new(func: ({invokeArgs}) => {invokeRet}): " +
                $"{exportName}${typeArgs.Length}<{typeParams}>;";

            s += "}";
            s++;
        }

        GenerateDocComments(ref s, type);

        s += $"export interface {exportName}{GetGenericParams(type)} {{ (" +
            $"{GetTSParameters(invokeMethod.GetParameters())}): " +
            $"{GetTSType(invokeMethod.ReturnParameter)}; }}";

        EndNamespace(ref s, type);
    }

    private void GenerateClassDefinition(ref SourceBuilder s, Type type)
    {
        s++;
        BeginNamespace(ref s, type);

        string exportName = GetExportName(type);

        bool isFirstMember = true;
        bool isGenericTypeDefinition = type.IsGenericTypeDefinition;
        if (isGenericTypeDefinition)
        {
            GenerateGenericTypeFactory(ref s, type);

            GenerateDocComments(ref s, type);

            Type[] typeArgs = type.GetGenericArguments();
            string typeParams = string.Join(", ", typeArgs.Select((t) => t.Name));
            s += $"export interface {exportName}$${typeArgs.Length}<{typeParams}> {{";

            foreach (ConstructorInfo constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (isFirstMember) isFirstMember = false; else s++;
                ExportTypeMember(ref s, constructor);
            }

            if (type.IsClass)
            {
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Public | BindingFlags.Static))
                {
                    if (isFirstMember) isFirstMember = false; else s++;
                    ExportTypeMember(ref s, property);
                }

                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static))
                {
                    if (!IsExcludedMethod(method))
                    {
                        if (isFirstMember) isFirstMember = false; else s++;
                        ExportTypeMember(ref s, method);
                    }
                }
            }

            s += "}";
            s++;
        }

        GenerateDocComments(ref s, type);

        bool isStaticClass = type.IsAbstract && type.IsSealed && !isGenericTypeDefinition;
        bool isStreamSubclass = type.BaseType?.FullName == typeof(Stream).FullName;
        string classKind = type.IsInterface || type.IsGenericTypeDefinition ?
            "interface" : isStaticClass ? "namespace" : "class";
        string implementsKind = type.IsInterface || type.IsGenericTypeDefinition ?
            "extends" : "implements";

        string implements = string.Empty;

        Type[] interfaceTypes = type.GetInterfaces();
        foreach (Type interfaceType in interfaceTypes)
        {
            string prefix = (implements.Length == 0 ? $" {implementsKind}" : ",") +
                (interfaceTypes.Length > 1 ? "\n\t" : " ");

            if (isStreamSubclass &&
                (interfaceType.Name == nameof(IDisposable) ||
                interfaceType.Name == nameof(IAsyncDisposable)))
            {
                // Stream projections extend JS Duplex class which has different close semantics.
                continue;
            }
            else if (interfaceType == typeof(IDisposable))
            {
                implements += prefix + nameof(IDisposable);
                _emitDisposable = true;
            }
            else if (interfaceType.Namespace != typeof(IList<>).Namespace &&
                !HasExplicitInterfaceImplementations(type, interfaceType))
            {
                // Extending generic collection interfaces gets tricky because of the way
                // those are projected to JS types. For now, those are just omitted here.

                // If any of the class's interface methods are implemented explicitly,
                // the interface is omitted. TypeScript does not and cannot support
                // explicit interface implementations because it uses duck typing.

                string tsType = GetTSType(interfaceType, nullability: null);
                if (tsType != "unknown")
                {
                    implements += prefix + tsType;
                }
            }
        }

        if (isStreamSubclass)
        {
            implements = " extends Duplex" + implements;
            _emitDuplex = true;
        }

        s += $"export {classKind} {exportName}{GetGenericParams(type)}{implements} {{";

        isFirstMember = true;

        if (!isGenericTypeDefinition)
        {
            foreach (ConstructorInfo constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (isFirstMember) isFirstMember = false; else s++;
                ExportTypeMember(ref s, constructor);
            }
        }

        if (!isStreamSubclass)
        {
            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance |
                (isStaticClass ? BindingFlags.DeclaredOnly : default) |
                (type.IsInterface || isGenericTypeDefinition ? default : BindingFlags.Static)))
            {
                if (isFirstMember) isFirstMember = false; else s++;
                ExportTypeMember(ref s, property);
            }

            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance |
                (isStaticClass ? BindingFlags.DeclaredOnly : default) |
                (type.IsInterface || isGenericTypeDefinition ? default : BindingFlags.Static)))
            {
                if (!IsExcludedMethod(method))
                {
                    if (isFirstMember) isFirstMember = false; else s++;
                    ExportTypeMember(ref s, method);
                }
            }
        }

        s += "}";

        EndNamespace(ref s, type);

        foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            ExportType(ref s, nestedType);
        }
    }

    private static bool HasExplicitInterfaceImplementations(Type type, Type interfaceType)
    {
        if (!type.IsClass)
        {
            if ((interfaceType.Name == nameof(IComparable) && type.IsInterface &&
                type.GetInterfaces().Any((i) => i.Name == typeof(IComparable<>).Name)) ||
                (interfaceType.Name == "ISpanFormattable" && type.IsInterface &&
                (type.Name == "INumberBase`1" ||
                type.GetInterfaces().Any((i) => i.Name == "INumberBase`1"))))
            {
                // TS interfaces cannot extend multiple interfaces that have non-identical methods
                // with the same name. This is most commonly an issue with IComparable and
                // ISpanFormattable/INumberBase generic and non-generic interfaces.
                return true;
            }

            return false;
        }
        else if (type.Name == "TypeDelegator" && interfaceType.Name == "IReflectableType")
        {
            // Special case: TypeDelegator has an explicit implementation of this interface,
            // but it isn't detected by reflection due to the runtime type delegation.
            return true;
        }

        // Note the InterfaceMapping class is not supported for assemblies loaded by a
        // MetadataLoadContext, so the answer is a little harder to find.

        if (interfaceType.IsConstructedGenericType)
        {
            interfaceType = interfaceType.GetGenericTypeDefinition();
        }

        // Get the interface type name with generic type parameters for matching.
        // It would be more precise to match the generic type params also,
        // but also more complicated.
        string interfaceTypeName = interfaceType.FullName!;
        int genericMarkerIndex = interfaceTypeName.IndexOf('`');
        if (genericMarkerIndex >= 0)
        {
            interfaceTypeName = interfaceTypeName.Substring(0, genericMarkerIndex);
        }

        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsFinal && method.IsPrivate &&
                method.Name.StartsWith(interfaceTypeName))
            {
                return true;
            }
        }

        foreach (Type baseInterfaceType in interfaceType.GetInterfaces())
        {
            if (HasExplicitInterfaceImplementations(type, baseInterfaceType))
            {
                return true;
            }
        }

        return false;
    }

    private void GenerateGenericTypeFactory(ref SourceBuilder s, Type type)
    {
        GenerateDocComments(ref s, type, "[Generic type factory] ");
        string exportName = GetExportName(type);
        Type[] typeArgs = type.GetGenericArguments();
        string typeParams = string.Join(", ", typeArgs.Select((t) => $"{t.Name}: IType<any>"));

        // TODO: Instead of `any` here, use TypeScript to map each generic type arg to JS.
        s += $"export function {exportName}$({typeParams}): " +
            $"{exportName}$${typeArgs.Length}<{string.Join(", ", typeArgs.Select((_) => "any"))}>;";
        s++;
        _emitType = true;
    }

    public string GenerateMemberDefinition(MemberInfo member)
    {
        SourceBuilder s = new();
        ExportTypeMember(ref s, member);
        return s.ToString();
    }

    private void ExportTypeMember(ref SourceBuilder s, MemberInfo member)
    {
        Type declaringType = member.DeclaringType!;

        if (member is ConstructorInfo constructor)
        {
            GenerateDocComments(ref s, constructor);
            string parameters = GetTSParameters(constructor.GetParameters());

            if (declaringType.IsGenericTypeDefinition)
            {
                string exportName = GetExportName(declaringType);
                Type[] typeArgs = declaringType.GetGenericArguments();
                string typeParams = string.Join(", ", typeArgs.Select((t) => t.Name));
                s += $"new({parameters}): {exportName}${typeArgs.Length}<{typeParams}>;";
            }
            else
            {
                s += $"constructor({parameters});";
            }
        }
        else if (member is PropertyInfo property)
        {
            string memberName = GetExportName(property);

            GenerateDocComments(ref s, property);
            string propertyName = TSIdentifier(memberName);
            string propertyType = GetTSType(property);

            if (declaringType.IsAbstract && declaringType.IsSealed)
            {
                string varKind = property.SetMethod == null ? "const " : "var ";
                s += $"export {varKind}{propertyName}: {propertyType};";
            }
            else
            {
                bool isStatic = (property.GetMethod?.IsStatic ??
                    property.SetMethod?.IsStatic ?? false) &&
                    !declaringType.IsGenericTypeDefinition;
                string modifiers = (isStatic ? "static " : "") +
                    (property.SetMethod == null ? "readonly " : "");
                string optionalToken = string.Empty;
                if (propertyType.EndsWith(UndefinedTypeSuffix))
                {
                    propertyType = propertyType.Substring(
                        0, propertyType.Length - UndefinedTypeSuffix.Length);
                    optionalToken = "?";
                }
                s += $"{modifiers}{propertyName}{optionalToken}: " +
                    $"{propertyType};";
            }
        }
        else if (member is MethodInfo method)
        {
            string memberName = GetExportName(method);

            GenerateDocComments(ref s, method);
            string methodName = TSIdentifier(memberName);
            string genericParams = GetGenericParams(method);
            string parameters = GetTSParameters(method.GetParameters());
            string returnType = GetTSType(method.ReturnParameter);

            if (methodName == nameof(IDisposable.Dispose))
            {
                // Match JS disposable naming convention.
                methodName = "dispose";
            }

            if (declaringType.IsAbstract && declaringType.IsSealed &&
                !declaringType.IsGenericTypeDefinition)
            {
                s += "export function " +
                    $"{methodName}{genericParams}({parameters}): {returnType};";
            }
            else
            {
                bool isStatic = method.IsStatic && !declaringType.IsGenericTypeDefinition;
                s += (isStatic ? "static " : "") +
                    $"{methodName}{genericParams}({parameters}): {returnType};";
            }
        }
    }

    private void BeginNamespace(ref SourceBuilder s, Type type)
    {
        if (!_exportAll)
        {
            // Presence of [JSExport] attributes indicates a module, which is not namespaced.
            return;
        }

        List<string> namespaceParts = new(type.Namespace?.Split('.') ?? Enumerable.Empty<string>());

        int namespacePartsCount = namespaceParts.Count;
        Type? declaringType = type.DeclaringType;
        while (declaringType != null)
        {
            namespaceParts.Insert(namespacePartsCount, GetExportName(declaringType));
            declaringType = declaringType.DeclaringType;
        }

        if (namespaceParts.Count > 0)
        {
            s += $"export namespace {string.Join(".", namespaceParts)} {{";
        }
    }

    private void EndNamespace(ref SourceBuilder s, Type type)
    {
        if (!_exportAll)
        {
            // Presence of [JSExport] attributes indicates a module, which is not namespaced.
            return;
        }

        if (type.Namespace != null || type.IsNested)
        {
            s += "}";
        }
    }

    private static bool IsExcludedMethod(MethodInfo method)
    {
        // Exclude "special" methods like property get/set and event add/remove.
        // Exclude old style Begin/End async methods, as they always have Task-based alternatives.
        // Exclude instance methods declared by System.Object like ToString() and Equals().
        return method.IsSpecialName ||
            (method.Name.StartsWith("Begin") &&
                method.ReturnType.FullName == typeof(IAsyncResult).FullName) ||
            (method.Name.StartsWith("End") && method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType.FullName == typeof(IAsyncResult).FullName) ||
            (!method.IsStatic && method.DeclaringType!.FullName == "System.Object");
    }

    private void GenerateEnumDefinition(ref SourceBuilder s, Type type)
    {
        s++;
        BeginNamespace(ref s, type);
        GenerateDocComments(ref s, type);
        string exportName = GetExportName(type);
        s += $"export enum {exportName} {{";

        bool isFirstMember = true;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (isFirstMember) isFirstMember = false; else s++;
            GenerateDocComments(ref s, field);
            s += $"{field.Name} = {field.GetRawConstantValue()},";
        }

        s += "}";
        EndNamespace(ref s, type);
    }

    private string GetTSType(PropertyInfo property)
    {
        Type propertyType = property.PropertyType;
        if (propertyType.IsByRef)
        {
            propertyType = propertyType.GetElementType()!;
        }

        string tsType = GetTSType(
            propertyType,
            FixNullability(_nullabilityContext.Create(property)));

        if (tsType == "unknown" || tsType.Contains("unknown"))
        {
            string className = property.DeclaringType!.Name;
            string typeName = ExpressionExtensions.FormatType(property.PropertyType);
            ReportWarning(
                DiagnosticId.UnsupportedPropertyType,
                $"Property {className}.{property.Name} with unsupported property type {typeName} " +
                    $"will be projected as {tsType}.");
        }

        return tsType;
    }

    private string GetTSType(ParameterInfo parameter)
    {
        string tsType;
        MethodInfo? method = parameter.Member as MethodInfo;

        if (parameter.Position < 0 && method != null)
        {
            if (parameter.ParameterType.FullName == typeof(bool).FullName &&
                parameter.Member.Name.StartsWith("Try") &&
                method.GetParameters().Count((p) => p.IsOut) == 1)
            {
                // A method with Try* pattern simply returns the out-value or undefined
                // instead of an object with the bool and out-value properties.
                tsType = GetTSType(method.GetParameters().Last());
                if (!tsType.EndsWith(UndefinedTypeSuffix))
                {
                    tsType += UndefinedTypeSuffix;
                }
                return tsType;
            }
            else if (method.GetParameters().Any((p) => p.IsOut))
            {
                // A method with ref/out parameters returns an object with properties for those,
                // along with a result property for the return value (if not void).
                string outProperties = string.Join(", ", method.GetParameters()
                    .Where((p) => p.IsOut || p.ParameterType.IsByRef)
                    .Select((p) =>
                    {
                        string propertyType = GetTSType(p);
                        string optionalToken = string.Empty;
                        if (propertyType.EndsWith(UndefinedTypeSuffix))
                        {
                            propertyType = propertyType.Substring(
                                0, propertyType.Length - UndefinedTypeSuffix.Length);
                            optionalToken = "?";
                        }
                        return $"{p.Name}{optionalToken}: {propertyType}";
                    }));

                if (method.ReturnType.FullName == typeof(void).FullName)
                {
                    return $"{{ {outProperties} }}";
                }
                else
                {
                    string resultName = ResultPropertyName;
                    if (method.GetParameters().Any(
                        (p) => p.Name == resultName && (p.IsOut || p.ParameterType.IsByRef)))
                    {
                        resultName = '_' + resultName;
                    }

                    tsType = GetTSType(
                        parameter.ParameterType,
                        FixNullability(_nullabilityContext.Create(parameter)));
                    return $"{{ {resultName}: {tsType}, {outProperties} }}";
                }
            }
        }

        Type parameterType = parameter.ParameterType;
        if (parameterType.IsByRef)
        {
            parameterType = parameterType.GetElementType()!;
        }

        tsType = GetTSType(parameterType, FixNullability(_nullabilityContext.Create(parameter)));
        if (tsType == "unknown" || tsType.Contains("unknown"))
        {
            string className = parameter.Member.DeclaringType!.Name;
            string typeName = ExpressionExtensions.FormatType(parameterType);

            if (parameter.Position < 0 && method != null)
            {
                ReportWarning(
                    DiagnosticId.UnsupportedMethodReturnType,
                    $"Method {className}.{method.Name} unsupported return type {typeName} " +
                        $"will be projected as {tsType}.");
            }
            else
            {
                ConstructorInfo? constructor = parameter.Member as ConstructorInfo;
                string description =
                    method != null ? $"{parameter.Name} in method {className}.{method.Name}" :
                    constructor != null ? $"{parameter.Name} in {className} constructor" :
                    parameter.Name!;
                ReportWarning(
                    DiagnosticId.UnsupportedMethodParameterType,
                    $"Parameter {description} with unsupported type {typeName} " +
                        $"will be projected as {tsType}.");
            }
        }

        return tsType;
    }

    /// <summary>
    /// The generator loads all referenced types in a separate MetadataLoadContext,
    /// which causes a problem with NullabilityInfoContext when it tries to detect
    /// whether a type is a value type, because the referenced ValueType type is not
    /// the same as the system ValueType type. This method overrides the nullability
    /// state for value types, which can never be nullable. (Note Nullable<T> is itself
    /// a non-nullable value type; it is handled by the generator as a special case.)
    /// </summary>
    private static NullabilityInfo FixNullability(NullabilityInfo nullability)
    {
        if (nullability.Type.BaseType?.FullName == typeof(ValueType).FullName)
        {
            // Use reflection to override these properties which have internal setters.
            // There is no public constructor and no other way to set these properties.
            typeof(NullabilityInfo).GetProperty(nameof(NullabilityInfo.ReadState))!
                .SetValue(nullability, NullabilityState.NotNull);
            typeof(NullabilityInfo).GetProperty(nameof(NullabilityInfo.WriteState))!
                .SetValue(nullability, NullabilityState.NotNull);
        }

        for (int i = 0; i < nullability.GenericTypeArguments.Length; i++)
        {
            FixNullability(nullability.GenericTypeArguments[i]);
        }

        return nullability;
    }

    private string GetTSType(Type type, NullabilityInfo? nullability)
    {
        string tsType = "unknown";
        if (type.IsPointer)
        {
            return tsType;
        }

        string? specialType = type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "boolean",
            "System.SByte" => "number",
            "System.Int16" => "number",
            "System.Int32" => "number",
            "System.Int64" => "number",
            "System.Byte" => "number",
            "System.UInt16" => "number",
            "System.UInt32" => "number",
            "System.UInt64" => "number",
            "System.Single" => "number",
            "System.Double" => "number",
            "System.String" => "string",
            "System.DateTime" => "Date",
            _ => null,
        };

        if (specialType != null)
        {
            tsType = specialType;
        }
#if !NETFRAMEWORK
        else if (type.IsGenericTypeParameter || type.IsGenericMethodParameter)
        {
            tsType = type.Name;
        }
#endif
        else if (type.FullName == typeof(JSValue).FullName)
        {
            tsType = "any";
        }
        else if (type.FullName == typeof(JSCallbackArgs).FullName)
        {
            tsType = "...any[]";
        }
        else if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            tsType = GetTSType(elementType, nullability?.ElementType) + "[]";
        }
        else if (type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
        {
            if (type.FullName == typeof(Action).FullName)
            {
                tsType = "() => void";
            }
            else if (type.IsGenericType && type.Name.StartsWith(nameof(Action) + "`"))
            {
                string[] parameters = type.GetGenericArguments().Select((t, i) =>
                        $"arg{i + 1}: {GetTSType(t, nullability?.GenericTypeArguments[i])}")
                    .ToArray();
                tsType = $"({string.Join(", ", parameters)}) => void";
            }
            else if (type.IsGenericType && type.Name.StartsWith("Func`"))
            {
                Type[] typeArgs = type.GetGenericArguments();
                string[] parameters = typeArgs.Take(typeArgs.Length - 1).Select((t, i) =>
                        $"arg{i + 1}: {GetTSType(t, nullability?.GenericTypeArguments[i])}")
                    .ToArray();
                string returnType = GetTSType(
                    typeArgs[typeArgs.Length - 1],
                    nullability?.GenericTypeArguments[typeArgs.Length - 1]);
                tsType = $"({string.Join(", ", parameters)}) => {returnType}";
            }
            else if (type.IsGenericType && type.Name.StartsWith("Predicate`"))
            {
                Type typeArg = type.GetGenericArguments()[0];
                string tsTypeArg = GetTSType(typeArg, nullability?.GenericTypeArguments[0]);
                tsType = $"(value: {tsTypeArg}) => boolean";
            }
            else if (IsTypeExported(type))
            {
                tsType = type.IsNested ? GetTSType(type.DeclaringType!, null) + '.' + type.Name :
                    (type.Namespace != null ? type.Namespace + '.' + type.Name : type.Name);
            }
        }
        else if (type.FullName == typeof(ValueTuple).FullName)
        {
            tsType = "[]";
        }
        else if (type.FullName == typeof(Task).FullName ||
            type.FullName == typeof(ValueTask).FullName)
        {
            tsType = "Promise<void>";
        }
        else if (type.FullName == typeof(CancellationToken).FullName)
        {
            tsType = "AbortSignal";
        }
        else if (type.FullName == typeof(IDisposable).FullName)
        {
            tsType = type.Name;
            _emitDisposable = true;
        }
        else if (type.FullName == typeof(Stream).FullName)
        {
            tsType = "Duplex";
            _emitDuplex = true;
        }
        else if (IsTypeExported(type))
        {
            tsType = type.IsNested ? GetTSType(type.DeclaringType!, null) + '.' + type.Name :
                (type.Namespace != null ? type.Namespace + '.' + type.Name : type.Name);
        }
        else if (_referenceAssemblies.ContainsKey(type.Assembly.GetName().Name!))
        {
            tsType = type.IsNested ? GetTSType(type.DeclaringType!, null) + '.' + type.Name :
                (type.Namespace != null ? type.Namespace + '.' + type.Name : type.Name);
            _imports.Add(type.Assembly.GetName().Name!);
        }

        if (type.IsGenericType)
        {
            string typeDefinitionName = type.GetGenericTypeDefinition().FullName!;
            Type[] typeArguments = type.GetGenericArguments();
            NullabilityInfo[]? typeArgumentsNullability = nullability?.GenericTypeArguments;
            if (typeArgumentsNullability?.Length < typeArguments.Length)
            {
                // NullabilityContext doesn't handle generic type arguments of by-ref parameters.
                typeArgumentsNullability = null;
            }

            if (typeDefinitionName == typeof(Nullable<>).FullName)
            {
                tsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]) +
                    UndefinedTypeSuffix;
            }
            else if (typeDefinitionName == typeof(Task<>).FullName ||
                typeDefinitionName == typeof(ValueTask<>).FullName)
            {
                tsType = $"Promise<{GetTSType(typeArguments[0], typeArgumentsNullability?[0])}>";
            }
            else if (typeDefinitionName == typeof(Memory<>).FullName ||
                typeDefinitionName == typeof(ReadOnlyMemory<>).FullName)
            {
                Type elementType = typeArguments[0];
                tsType = elementType.FullName switch
                {
                    "System.SByte" => "Int8Array",
                    "System.Int16" => "Int16Array",
                    "System.Int32" => "Int32Array",
                    "System.Int64" => "BigInt64Array",
                    "System.Byte" => "Uint8Array",
                    "System.UInt16" => "Uint16Array",
                    "System.UInt32" => "Uint32Array",
                    "System.UInt64" => "BigUint64Array",
                    "System.Single" => "Float32Array",
                    "System.Double" => "Float64Array",
                    _ => "unknown",
                };
            }
            else if (typeDefinitionName == typeof(IList<>).FullName)
            {
                tsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]) + "[]";
            }
            else if (typeDefinitionName == typeof(IReadOnlyList<>).FullName)
            {
                tsType = "readonly " + GetTSType(typeArguments[0], typeArgumentsNullability?[0]) +
                    "[]";
            }
            else if (typeDefinitionName == typeof(ICollection<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"Iterable<{elementTsType}> & {{ length: number, " +
                    $"add(item: {elementTsType}): void, delete(item: {elementTsType}): boolean }}";
            }
            else if (typeDefinitionName == typeof(IReadOnlyCollection<>).FullName ||
                typeDefinitionName == typeof(ReadOnlyCollection<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"Iterable<{elementTsType}> & {{ length: number }}";
            }
            else if (typeDefinitionName == typeof(ISet<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"Set<{elementTsType}>";
            }
#if !NETFRAMEWORK
            else if (typeDefinitionName == typeof(IReadOnlySet<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"ReadonlySet<{elementTsType}>";
            }
#endif
            else if (typeDefinitionName == typeof(IEnumerable<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"Iterable<{elementTsType}>";
            }
            else if (typeDefinitionName == typeof(IDictionary<,>).FullName)
            {
                string keyTSType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                string valueTSType = GetTSType(typeArguments[1], typeArgumentsNullability?[1]);
                tsType = $"Map<{keyTSType}, {valueTSType}>";
            }
            else if (typeDefinitionName == typeof(IReadOnlyDictionary<,>).FullName)
            {
                string keyTSType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                string valueTSType = GetTSType(typeArguments[1], typeArgumentsNullability?[1]);
                tsType = $"ReadonlyMap<{keyTSType}, {valueTSType}>";
            }
            else if (typeDefinitionName == typeof(KeyValuePair<,>).FullName)
            {
                string keyTSType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                string valueTSType = GetTSType(typeArguments[1], typeArgumentsNullability?[1]);
                tsType = $"[{keyTSType}, {valueTSType}]";
            }
            else if (typeDefinitionName.StartsWith("System.Tuple`") ||
                typeDefinitionName.StartsWith("System.ValueTuple`"))
            {
                IEnumerable<string> itemTSTypes = typeArguments.Select((typeArg, index) =>
                    GetTSType(typeArg, typeArgumentsNullability?[index]));
                tsType = $"[{string.Join(", ", itemTSTypes)}]";
            }
            else
            {
                int typeNameEnd = tsType.IndexOf('`');
                if (typeNameEnd > 0)
                {
                    tsType = tsType.Substring(0, typeNameEnd);

                    Type[] typeArgs = type.GetGenericArguments();
                    string typeParams = string.Join(", ", typeArgs.Select(
                        (t, i) => GetTSType(t, typeArgumentsNullability?[i])));
                    tsType = $"{tsType}${typeArgs.Length}<{typeParams}>";
                }
                else if (type.IsNested && type.DeclaringType!.IsGenericTypeDefinition)
                {
                    int genericParamsStart = tsType.IndexOf('$');
                    int genericParamsEnd = tsType.IndexOf('>');
                    if (genericParamsStart > 0 && genericParamsEnd > 0)
                    {
                        // TS doesn't support nested types (static properties) on a generic class.
                        // For now, move the generic type parameters onto the nested class.
                        string declaringType = tsType.Substring(0, genericParamsStart);
                        string genericParams = tsType.Substring(
                                genericParamsStart, genericParamsEnd + 1 - genericParamsStart);
                        string nestedType = tsType.Substring(genericParamsEnd + 1);
                        tsType = $"{declaringType}{nestedType}{genericParams}";
                    }
                }
            }
        }

        if (nullability?.ReadState == NullabilityState.Nullable &&
#if !NETFRAMEWORK
            !type.IsGenericTypeParameter && !type.IsGenericMethodParameter &&
#endif
            !tsType.EndsWith(UndefinedTypeSuffix))
        {
            tsType += UndefinedTypeSuffix;
        }

        return tsType;
    }

    private string GetTSParameters(ParameterInfo[] parameters)
    {
        static string GetOptionalToken(ParameterInfo parameter, ref string parameterType)
        {
            if (parameter.IsOptional)
            {
                if (parameterType.EndsWith(UndefinedTypeSuffix))
                {
                    parameterType = parameterType.Substring(
                        0, parameterType.Length - UndefinedTypeSuffix.Length);
                }
                return "?";
            }
            return string.Empty;
        }

        // Exclude out-only parameters.
        parameters = parameters.Where((p) => !(p.IsOut && !p.IsIn)).ToArray();

        if (parameters.Length == 0)
        {
            return string.Empty;
        }
        else if (parameters.Length == 1)
        {
            string parameterType = GetTSType(parameters[0]);
            if (parameterType.StartsWith("..."))
            {
                return $"...{TSIdentifier(parameters[0].Name)}: {parameterType.Substring(3)}";
            }
            else
            {
                string optionalToken = GetOptionalToken(parameters[0], ref parameterType);
                return $"{TSIdentifier(parameters[0].Name)}{optionalToken}: {parameterType}";
            }
        }

        var s = new StringBuilder();
        s.AppendLine();

        foreach (ParameterInfo p in parameters)
        {
            string parameterType = GetTSType(p);
            string optionalToken = GetOptionalToken(p, ref parameterType);
            s.AppendLine($"{TSIdentifier(p.Name)}{optionalToken}: {parameterType},");
        }

        return s.ToString();
    }

    private string GetExportName(MemberInfo member)
    {
        CustomAttributeData? attribute = member.GetCustomAttributesData().FirstOrDefault(
            (a) => a.AttributeType.FullName == typeof(JSExportAttribute).FullName);
        if (attribute != null && attribute.ConstructorArguments.Count > 0 &&
            !string.IsNullOrEmpty(attribute.ConstructorArguments[0].Value as string))
        {
            return (string)attribute.ConstructorArguments[0].Value!;
        }
        else
        {
            string name = member.Name;
            if (member is Type memberType && memberType.IsGenericTypeDefinition)
            {
                int nameEnd = name.IndexOf('`');
                if (nameEnd > 0)
                {
                    name = name.Substring(0, nameEnd);
                }
            }

            return _autoCamelCase && member is not Type ? ToCamelCase(name) : name;
        }
    }

    private void GenerateDocComments(
        ref SourceBuilder s,
        MemberInfo member,
        string? summaryPrefix = null)
    {
        string memberDocName = member switch
        {
            Type type => $"T:{type.FullName}",
            PropertyInfo property => $"P:{property.DeclaringType!.FullName}.{property.Name}",
            MethodInfo method => $"M:{FormatDocMethodName(method)}" +
                FormatDocMethodParameters(method),
            ConstructorInfo constructor => $"M:{constructor.DeclaringType!.FullName}.#ctor" +
                FormatDocMethodParameters(constructor),
            FieldInfo field => $"F:{field.DeclaringType!.FullName}.{field.Name}",
            _ => string.Empty,
        };

        XElement? memberElement = _assemblyDoc?.Root?.Element("members")?.Elements("member")
            .FirstOrDefault((m) => m.Attribute("name")?.Value == memberDocName);

        XElement? summaryElement = memberElement?.Element("summary");
        XElement? remarksElement = memberElement?.Element("remarks");
        if (memberElement == null || summaryElement == null ||
            string.IsNullOrWhiteSpace(summaryElement.Value))
        {
            return;
        }

        string summary = (summaryPrefix ?? string.Empty) + FormatDocText(summaryElement);
        string remarks = FormatDocText(remarksElement);

        if (string.IsNullOrEmpty(remarks) && summary.Length < 83 && summary.IndexOf('\n') < 0)
        {
            s += $"/** {summary} */";
        }
        else
        {
            s += "/**";

            foreach (string commentLine in WrapComment(summary, 90 - 3 - s.Indent.Length))
            {
                s += " * " + commentLine;
            }

            if (!string.IsNullOrEmpty(remarks))
            {
                s += " *";
                foreach (string commentLine in WrapComment(remarks, 90 - 3 - s.Indent.Length))
                {
                    s += " * " + commentLine;
                }
            }

            s += " */";
        }
    }

    private static string FormatDocText(XNode? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (node is XElement element)
        {
            if (element.Name == "see")
            {
                string target = element.Attribute("cref")?.Value?.ToString() ?? string.Empty;
                target = target.Substring(target.IndexOf(':') + 1);

                int genericCountIndex = target.LastIndexOf('`');
#pragma warning disable CA1846 // Prefer 'AsSpan' over 'Substring'
                if (genericCountIndex > 0 &&
                    int.TryParse(target.Substring(genericCountIndex + 1), out int genericCount))
#pragma warning restore CA1846
                {
                    // TODO: Resolve generic type paramter names.
                    target = target.Substring(0, genericCountIndex);
                    target += $"<{new string(',', genericCount - 1)}>";
                }

                return $"`{target}`";
            }
            else if (element.Name == "paramref")
            {
                string target = element.Attribute("name")?.Value?.ToString() ?? string.Empty;
                return $"`{target}`";
            }
            else
            {
                return string.Join(" ", element.Nodes().Select(FormatDocText));
            }
        }

        return s_newlineRegex.Replace(
            (node?.ToString() ?? string.Empty).Replace("\r", "").Trim(), " ");
    }

    private static string FormatDocMethodName(MethodInfo method)
    {
        string genericSuffix = method.IsGenericMethodDefinition ?
            "``" + method.GetGenericArguments().Length : string.Empty;
        return $"{method.DeclaringType!.FullName}.{method.Name}{genericSuffix}";
    }

    private static string FormatDocMethodParameters(MethodBase method)
    {
        Type[]? genericTypeParams = null;
        if (method.DeclaringType!.IsGenericTypeDefinition)
        {
            // Constructors and methods may include generic parameters from the type.
            genericTypeParams = method.DeclaringType.GetGenericArguments();
        }

        Type[]? genericMethodParams = null;
        try
        {
            if (method.ContainsGenericParameters)
            {
                genericMethodParams = method.GetGenericArguments();
            }
        }
        catch (NotSupportedException)
        {
            // A constructor or method that contains generic type parameters but not
            // generic method parameters may return true for ContainsGenericParameters
            // and then throw NotSupportedException from GetGenericArguments().
        }

        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length == 0 ? string.Empty :
            '(' + string.Join(",", parameters.Select(
                (p) => FormatDocMemberParameterType(
                    p.ParameterType, genericTypeParams, genericMethodParams))) + ')';
    }

    private static string FormatDocMemberParameterType(
        Type type,
        Type[]? genericTypeParams,
        Type[]? genericMethodParams)
    {
#if NETFRAMEWORK
        if (type.IsGenericMethodParameter() && genericMethodParams != null)
#else
        if (type.IsGenericTypeParameter && genericTypeParams != null)
        {
            return "`" + Array.IndexOf(genericTypeParams, type);
        }
        else if (type.IsGenericMethodParameter && genericMethodParams != null)
#endif
        {
            return "``" + Array.IndexOf(genericMethodParams, type);
        }
        else if (type.IsGenericType)
        {
            if (type.IsNested && type.DeclaringType!.IsGenericType)
            {
                string declaringTypeName = type.DeclaringType.Name;
                declaringTypeName = declaringTypeName.Substring(0, declaringTypeName.IndexOf('`'));
                string typeArgs = string.Join(
                    ",",
                    type.DeclaringType.GenericTypeArguments.Select(
                        (t) => FormatDocMemberParameterType(
                            t, genericTypeParams, genericMethodParams)));
                return $"{type.Namespace}.{declaringTypeName}{{{typeArgs}}}.{type.Name}";
            }
            else
            {
                string typeName = type.Name.Substring(0, type.Name.IndexOf('`'));
                string typeArgs = string.Join(
                    ",", type.GenericTypeArguments.Select(
                        (t) => FormatDocMemberParameterType(
                            t, genericTypeParams, genericMethodParams)));
                return $"{type.Namespace}.{typeName}{{{typeArgs}}}";
            }
        }
        else
        {
            return type.FullName ?? type.Name;
        }
    }

    private static string TSIdentifier(string? identifier)
    {
        return identifier switch
        {
            // A method parameter named "function" is valid in C# but invalid in TS.
            "function" => "_" + identifier,
            null => "_",
            _ => identifier,
        };
    }
}
