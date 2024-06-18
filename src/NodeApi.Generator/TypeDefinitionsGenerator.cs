// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    /// Enables application code to load an assembly (containing explicit JS exports) as an ES
    /// module, along with type definitions, in one simple import statement.
    /// </summary>
    /// <remarks>
    /// The `__filename` and `__dirname` values are computed for compatibility with ES modules;
    /// they are equivalent to those predefined values defined for CommonJS modules.
    /// The required ES export declarations will be appended to this code by the generator.
    /// An MSBuild task during the AOT publish process sets the `dotnet` variable to undefined.
    /// </remarks>
    private const string LoadModuleMJS = @"
import dotnet from 'node-api-dotnet';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
// @ts-ignore - https://github.com/DefinitelyTyped/DefinitelyTyped/discussions/65252
import { dlopen, platform, arch } from 'node:process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const moduleName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
const exports = dotnet ? importDotnetModule(moduleName) : importAotModule(moduleName);

function importDotnetModule(moduleName) {
    const moduleFilePath = path.join(__dirname, moduleName + '.dll');
    return dotnet.require(moduleFilePath);
}

function importAotModule(moduleName) {
    const ridPlatform = platform === 'win32' ? 'win' : platform === 'darwin' ? 'osx' : platform;
    const ridArch = arch === 'ia32' ? 'x86' : arch;
    const rid = `${ridPlatform}-${ridArch}`;
    const moduleFilePath = path.join(__dirname, rid, moduleName + '.node');
    const module = { exports: {} };
    dlopen(module, moduleFilePath);
    return module.exports;
}";
    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load an assembly (containing explicit JS exports) as a CommonJS
    /// module, along with type definitions, in one simple import statement.
    /// </summary>
    /// <remarks>
    /// An MSBuild task during the AOT publish process sets the `dotnet` variable to undefined.
    /// </remarks>
    private const string LoadModuleCJS = @"
const dotnet = require('node-api-dotnet');
const path = require('node:path');
// @ts-ignore - https://github.com/DefinitelyTyped/DefinitelyTyped/discussions/65252
const { dlopen, platform, arch } = require('node:process');

const moduleName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
module.exports = dotnet ? importDotnetModule(moduleName) : importAotModule(moduleName);

function importDotnetModule(moduleName) {
    const moduleFilePath = path.join(__dirname, moduleName + '.dll');
    return dotnet.require(moduleFilePath);
}

function importAotModule(moduleName) {
    const ridPlatform = platform === 'win32' ? 'win' : platform === 'darwin' ? 'osx' : platform;
    const ridArch = arch === 'ia32' ? 'x86' : arch;
    const rid = `${ridPlatform}-${ridArch}`;
    const moduleFilePath = path.join(__dirname, rid, moduleName + '.node');
    const module = { exports: {} };
    dlopen(module, moduleFilePath);
    return module.exports;
}";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load an assembly file and type definitions as an ES module with
    /// one simple import statement. The module does not have direct exports; it augments the
    /// node-api-dotnet module.
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
const assemblyName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
const assemblyFilePath = path.join(__dirname, assemblyName + '.dll');
dotnet.load(assemblyFilePath);";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load an assembly file and type definitions as a CommonJS module
    /// with one simple require statement. The module does not have direct exports; it augments
    /// the node-api-dotnet module.
    /// </summary>
    private const string LoadAssemblyCJS = @"
const dotnet = require('node-api-dotnet');
const path = require('node:path');

const assemblyName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
const assemblyFilePath = path.join(__dirname, assemblyName + '.dll');
dotnet.load(assemblyFilePath);";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load a system assembly and type definitions as an ES module
    /// with one simple import statement. The module does not have direct exports; it augments
    /// the node-api-dotnet module.
    /// </summary>
    private const string LoadSystemAssemblyMJS = @"
import dotnet from 'node-api-dotnet';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const assemblyName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
dotnet.load(assemblyName);";

    /// <summary>
    /// JavaScript (not TypeScript) code that is emitted to a `.js` file alongside the `.d.ts`.
    /// Enables application code to load a system assembly and type definitions as a CommonJS
    /// module with one simple require statement. The module does not have direct exports;
    /// it augments the node-api-dotnet module.
    /// </summary>
    private const string LoadSystemAssemblyCJS = @"
const dotnet = require('node-api-dotnet');
const path = require('node:path');

const assemblyName = path.basename(__filename, __filename.match(/(\.[cm]?js)?$/)[0]);
dotnet.load(assemblyName);";

    private const string UndefinedTypeSuffix = " | undefined";

    private static readonly Regex s_newlineRegex = new("\n *");

    private readonly NullabilityInfoContext _nullabilityContext = new();

    private readonly Assembly _assembly;
    private readonly IDictionary<string, Assembly> _referenceAssemblies;
    private readonly HashSet<string> _imports;
    private readonly Dictionary<string, XDocument> _assemblyDocs = new();
    private readonly List<MemberInfo> _exportedMembers = new();
    private bool _isModule;
    private bool _autoCamelCase;
    private bool _emitDisposable;
    private bool _emitDuplex;
    private bool _emitType;
    private bool _emitDateTime;
    private bool _emitDateTimeOffset;

    /// <summary>
    /// When generating type definitions for a system assembly, some supplemental type definitions
    /// need an extra namespace qualifier to prevent conflicts with types in the "System" namespace.
    /// </summary>
    private readonly bool _isSystemAssembly;

    public static void GenerateTypeDefinitions(
        string assemblyPath,
        IEnumerable<string> referenceAssemblyPaths,
        IEnumerable<string> systemReferenceAssemblyDirectories,
        string typeDefinitionsPath,
        IDictionary<ModuleType, string> modulePaths,
        bool isSystemAssembly = false,
        bool suppressWarnings = false)
    {
        if (string.IsNullOrEmpty(assemblyPath))
        {
            throw new ArgumentNullException(nameof(assemblyPath));
        }
        else if (referenceAssemblyPaths is null)
        {
            throw new ArgumentNullException(nameof(referenceAssemblyPaths));
        }
        else if (systemReferenceAssemblyDirectories is null)
        {
            throw new ArgumentNullException(nameof(systemReferenceAssemblyDirectories));
        }
        else if (string.IsNullOrEmpty(typeDefinitionsPath))
        {
            throw new ArgumentNullException(nameof(typeDefinitionsPath));
        }
        else if (modulePaths is null)
        {
            throw new ArgumentNullException(nameof(modulePaths));
        }

        // Create a metadata load context that includes a resolver for system assemblies,
        // referenced assemblies, and the target assembly.
        IEnumerable<string> allReferenceAssemblyPaths = MergeSystemReferenceAssemblies(
            referenceAssemblyPaths, systemReferenceAssemblyDirectories);
        bool isCoreAssembly = Path.GetFileNameWithoutExtension(assemblyPath).Equals(
            typeof(object).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase);
        if (!isCoreAssembly)
        {
            allReferenceAssemblyPaths = allReferenceAssemblyPaths.Append(assemblyPath);
        }

        PathAssemblyResolver assemblyResolver = new(allReferenceAssemblyPaths);
        using MetadataLoadContext loadContext = new(assemblyResolver);

        Assembly assembly = isCoreAssembly ? loadContext.CoreAssembly! :
            loadContext.LoadFromAssemblyPath(assemblyPath);

        Dictionary<string, Assembly> referenceAssemblies = new();
        foreach (string referenceAssemblyPath in referenceAssemblyPaths)
        {
            if (!allReferenceAssemblyPaths.Contains(referenceAssemblyPath))
            {
                // The referenced assembly was replaced by a system assembly.
                continue;
            }

            Assembly referenceAssembly = loadContext.LoadFromAssemblyPath(referenceAssemblyPath);
            string referenceAssemblyName = referenceAssembly.GetName().Name!;
            referenceAssemblies.Add(referenceAssemblyName, referenceAssembly);
        }

        CustomAttributeData? assemblyExportAttribute = assembly.GetCustomAttributesData()
            .FirstOrDefault((a) => a.AttributeType.FullName == typeof(JSExportAttribute).FullName);

        try
        {
            TypeDefinitionsGenerator generator = new(assembly, referenceAssemblies)
            {
                SuppressWarnings = suppressWarnings,
                ExportAll = assemblyExportAttribute != null &&
                    GetExportAttributeValue(assemblyExportAttribute),
            };

            generator.LoadAssemblyDocs();
            SourceText generatedSource = generator.GenerateTypeDefinitions();
            File.WriteAllText(typeDefinitionsPath, generatedSource.ToString());

            foreach (KeyValuePair<ModuleType, string> moduleTypeAndPath in modulePaths)
            {
                ModuleType moduleType = moduleTypeAndPath.Key;
                string moduleFilePath = moduleTypeAndPath.Value;

                SourceText generatedModule = generator.GenerateModuleLoader(
                    moduleType, isSystemAssembly);
                File.WriteAllText(moduleFilePath, generatedModule.ToString());
            }
        }
        finally
        {
            SymbolExtensions.Reset();
        }
    }

    /// <summary>
    /// Finds system assemblies that may be referenced by project code, and resolves
    /// conflicts between project-referenced assemblies and system assemblies by selecting the
    /// highest version of each assembly.
    /// </summary>
    private static IEnumerable<string> MergeSystemReferenceAssemblies(
        IEnumerable<string> referenceAssemblyPaths,
        IEnumerable<string> systemReferenceAssemblyDirectories)
    {
        // Resolve all assemblies in all the system reference assembly directories.
        IEnumerable<string> systemAssemblyPaths = systemReferenceAssemblyDirectories
            .SelectMany((d) => Directory.GetFiles(d, "*.dll"));

        // Concatenate system reference assemblies with project (nuget) reference assemblies.
        IEnumerable<string> allAssemblyPaths = new[] { typeof(object).Assembly.Location }
            .Concat(systemAssemblyPaths)
            .Concat(referenceAssemblyPaths);

        // Select the latest version of each referenced assembly.
        // First group by assembly name, then pick the highest version in each group.
        IEnumerable<IGrouping<string, string>> assembliesByVersion = allAssemblyPaths.Concat(referenceAssemblyPaths)
            .GroupBy(a => Path.GetFileNameWithoutExtension(a).ToLowerInvariant());
        IEnumerable<string> mergedAssemblyPaths = assembliesByVersion.Select(
            (g) => g.OrderByDescending((a) => InferReferenceAssemblyVersionFromPath(a)).First());
        return mergedAssemblyPaths;
    }

    private static Version InferReferenceAssemblyVersionFromPath(string assemblyPath)
    {
        var pathParts = assemblyPath.Split(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();

        // Infer the version from a system reference assembly path such as
        // dotnet\packs\Microsoft.NETCore.App.Ref\<version>\ref\net6.0\AssemblyName.dll
        int refIndex = pathParts.IndexOf("ref");
        if (refIndex > 0 && Version.TryParse(pathParts[refIndex - 1], out Version? refVersion))
        {
            return refVersion;
        }

        // Infer the version from a nuget package assembly reference path such as
        // <packageName>\<version>\lib\net6.0\AssemblyName.dll
        int libIndex = pathParts.IndexOf("lib");
        if (libIndex > 0 && Version.TryParse(pathParts[libIndex - 1], out Version? libVersion))
        {
            return libVersion;
        }

        // The version cannot be inferred from the path. The reference will still be used
        // if it is the only one with that assembly name.
        return new Version();
    }

    public TypeDefinitionsGenerator(
        Assembly assembly,
        IDictionary<string, Assembly> referenceAssemblies)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }
        else if (referenceAssemblies is null)
        {
            throw new ArgumentNullException(nameof(referenceAssemblies));
        }

        _assembly = assembly;
        _referenceAssemblies = referenceAssemblies;
        _imports = new HashSet<string>();
        _isSystemAssembly = assembly.GetName().Name!.StartsWith("System.");
    }

    public bool ExportAll { get; set; }

    public bool SuppressWarnings { get; set; }

    public override void ReportDiagnostic(Diagnostic diagnostic)
    {
        if (SuppressWarnings && diagnostic.Severity == DiagnosticSeverity.Warning)
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
            $"/* eslint-disable */";
    }

    public SourceText GenerateTypeDefinitions(bool? autoCamelCase = null)
    {
        var s = new SourceBuilder();
        s += GetGeneratedFileHeader();

        // Imports will be inserted here later, after the used references are determined.
        int importsIndex = s.Length;

        _exportedMembers.AddRange(GetExportedMembers());
        if (!_isModule)
        {
            ExportAll = true;
        }

        // Default to camel-case for modules, preserve case otherwise.
        _autoCamelCase = autoCamelCase ?? _isModule;

        s++;

        if (!_isModule)
        {
            // Declare this types as members of the 'node-api-dotnet' module.
            // This causes types across multiple .NET assemblies to be merged into
            // a shared .NET namespace hierarchy.
            s += "declare module 'node-api-dotnet' {";
        }

        foreach (Type type in _assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (IsExported(type))
            {
                ExportType(ref s, type);
            }
            else
            {
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (IsExported(member))
                    {
                        ExportMember(ref s, member);
                    }
                }
            }
        }

        if (!_isModule)
        {
            s += "}";
        }

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

        return s;
    }

    public SourceText GenerateModuleLoader(ModuleType moduleType, bool isSystemAssembly = false)
    {
        var s = new SourceBuilder();
        s += GetGeneratedFileHeader();

        if (_isModule)
        {
            if (moduleType == ModuleType.ES)
            {
                s += LoadModuleMJS.Replace("    ", ""); // The SourceBuilder will auto-indent.

                bool isFirstMember = true;
                bool hasDefaultExport = false;
                foreach (MemberInfo member in _exportedMembers)
                {
                    string exportName = GetExportName(member);

                    if (member is PropertyInfo exportedProperty &&
                        exportedProperty.SetMethod != null)
                    {
                        ReportWarning(
                            DiagnosticId.ESModulePropertiesAreConst,
                            $"Module-level property '{exportName}' with setter will be " +
                            "exported as read-only because ES module properties are constant.");
                    }

                    if (exportName == "default")
                    {
                        hasDefaultExport = true;
                    }
                    else
                    {
                        if (isFirstMember)
                        {
                            s++;
                            isFirstMember = false;
                        }

                        s += $"export const {exportName} = exports.{exportName};";
                    }
                }

                if (hasDefaultExport)
                {
                    s++;
                    s += $"export default exports['default'];";
                }
            }
            else if (moduleType == ModuleType.CommonJS)
            {
                s += LoadModuleCJS.Replace("    ", ""); // The SourceBuilder will auto-indent.
            }
            else
            {
                throw new ArgumentException(
                    "Invalid module type: " + moduleType, nameof(moduleType));
            }
        }
        else
        {
            if (moduleType == ModuleType.ES)
            {
                s += isSystemAssembly ? LoadSystemAssemblyMJS : LoadAssemblyMJS;
            }
            else if (moduleType == ModuleType.CommonJS)
            {
                s += isSystemAssembly ? LoadSystemAssemblyCJS : LoadAssemblyCJS;
            }
            else
            {
                throw new ArgumentException(
                    "Invalid module type: " + moduleType, nameof(moduleType));
            }
        }

        return s;
    }

    private bool IsExported(MemberInfo member)
    {
        Type type = member as Type ?? member.DeclaringType!;

        if (IsExcluded(type))
        {
            return false;
        }

        // Types not in the current assembly are not exported from this TS module.
        // (But support mscorlib and System.Runtime forwarding to System.Private.CoreLib.)
        if (type.Assembly != _assembly &&
            !(type.Assembly.GetName().Name == "System.Private.CoreLib" &&
            (_assembly.GetName().Name == "mscorlib" || _assembly.GetName().Name == "System.Runtime")))
        {
            return false;
        }

        CustomAttributeData? exportAttribute = GetAttribute<JSExportAttribute>(member);
        if (exportAttribute == null && !IsPublic(member))
        {
            return false;
        }

        // If the member doesn't have a [JSExport] attribute, check its declaring type.
        while (exportAttribute == null && member.DeclaringType != null)
        {
            member = member.DeclaringType;
            exportAttribute = GetAttribute<JSExportAttribute>(member);
            if (exportAttribute == null && !IsPublic(member))
            {
                return false;
            }
        }

        // Return export attribute value if found, or else the default for the assembly.
        return exportAttribute != null ? GetExportAttributeValue(exportAttribute) : ExportAll;
    }

    private static CustomAttributeData? GetAttribute<T>(MemberInfo member)
    {
        return member.GetCustomAttributesData().FirstOrDefault((a) =>
            a.AttributeType.FullName == typeof(T).FullName);
    }

    private static bool GetExportAttributeValue(CustomAttributeData exportAttribute)
    {
        // If the [JSExport] attribute has a single boolean constructor argument, use that.
        // Any other constructor defaults to true.
        CustomAttributeTypedArgument constructorArgument =
            exportAttribute.ConstructorArguments.SingleOrDefault();
        return constructorArgument.Value as bool? ?? true;
    }

    private static bool IsPublic(MemberInfo member)
    {
        if (member is not Type &&
            member.DeclaringType is Type declaringType && declaringType.IsInterface)
        {
            // Interface members are always public even if not declared.
            return true;
        }

        return member switch
        {
            Type type => type.IsPublic || type.IsNestedPublic,
            MethodBase method => method.IsPublic,
            PropertyInfo property => (property.GetMethod?.IsPublic ?? false) ||
                (property.SetMethod?.IsPublic ?? false),
            EventInfo @event => (@event.AddMethod?.IsPublic ?? false) ||
                (@event.RemoveMethod?.IsPublic ?? false),
            FieldInfo field => field.IsPublic,
            _ => false,
        };
    }

    private IEnumerable<MemberInfo> GetExportedMembers()
    {
        foreach (Type type in _assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (GetAttribute<JSModuleAttribute>(type) != null)
            {
                _isModule = true;
            }
            else if (IsExported(type))
            {
                _isModule = true;
                yield return type;
            }
            else
            {
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
                {
                    if (GetAttribute<JSModuleAttribute>(member) != null)
                    {
                        _isModule = true;
                    }
                    else if (IsExported(member))
                    {
                        _isModule = true;
                        yield return member;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates a type definition for a single type. Primarily for unit-testing purposes.
    /// </summary>
    public string GenerateTypeDefinition(Type type)
    {
        // Don't use namespaces when generating a single type definition.
        _isModule = true;

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

            GenerateExtensionMethods(ref s, type);
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
interface IType {
	/**
	 * Constructs a new instance of the type.
	 * (Not available for static class or interface types.)
	 */
	new?(...args: any[]): IType;

	/** Gets the full name of the .NET type. */
	toString(): string;
}
");
        }

        if (_emitDisposable)
        {
            s.Insert(insertIndex, @"
interface IDisposable { dispose(): void; }
");
        }

        if (_emitDuplex)
        {
            s.Insert(insertIndex, @"
import { Duplex } from 'stream';
");
        }

        if (_emitDateTimeOffset)
        {
            s.Insert(insertIndex, _isSystemAssembly ? @"
namespace js { type DateTimeOffset = Date | { offset?: number } }
" : @"
type DateTimeOffset = Date | { offset?: number }
");
        }

        if (_emitDateTime)
        {
            s.Insert(insertIndex, _isSystemAssembly ? @"
namespace js { type DateTime = Date | { kind?: 'utc' | 'local' | 'unspecified' } }
" : @"
type DateTime = Date | { kind?: 'utc' | 'local' | 'unspecified' }
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

        if (type.IsGenericTypeDefinition)
        {
            GenerateGenericTypeFactory(ref s, type);
        }

        GenerateDocComments(ref s, type);

        bool isStaticClass = type.IsAbstract && type.IsSealed && !type.IsGenericTypeDefinition;
        bool isStreamSubclass = type.BaseType?.FullName == typeof(Stream).FullName;
        string classKind = type.IsInterface ? "interface" : isStaticClass ? "namespace" : "class";
        string implementsKind = type.IsInterface ? "extends" : "implements";

        string implements = string.Empty;
        Type[] interfaceTypes = type.GetInterfaces().Where(IsExported).ToArray();
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

        string exportPrefix = "export ";
        if (exportName == "default")
        {
            // For default exports first declare the class then export it separately.
            exportPrefix = "declare ";
            exportName = "__default";
        }

        s += $"{exportPrefix}{classKind} {exportName}{GetGenericParams(type)}{implements} {{";

        bool isFirstMember = true;
        foreach (ConstructorInfo constructor in type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance).Where(IsExported))
        {
            if (!IsExcluded(constructor))
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
                (type.IsInterface ? default : BindingFlags.Static)).Where(IsExported))
            {
                // Indexed properties are not implemented.
                if (!IsExcluded(property) && property.GetIndexParameters().Length == 0)
                {
                    if (isFirstMember) isFirstMember = false; else s++;
                    ExportTypeMember(ref s, property);
                }
            }

            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance |
                (isStaticClass ? BindingFlags.DeclaredOnly : default) |
                (type.IsInterface ? default : BindingFlags.Static)).Where(IsExported))
            {
                if (!IsExcluded(method))
                {
                    if (isFirstMember) isFirstMember = false; else s++;
                    ExportTypeMember(ref s, method);
                }
            }
        }

        s += "}";
        if (exportName == "__default")
        {
            s += $"export default {exportName};";
        }

        EndNamespace(ref s, type);

        foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public).Where(IsExported))
        {
            ExportType(ref s, nestedType);
        }
    }

    private static bool HasExplicitInterfaceImplementations(Type type, Type interfaceType)
    {
        if (type.IsInterface)
        {
            if ((interfaceType.Name == nameof(IComparable) &&
                type.GetInterfaces().Any((i) => i.Name == typeof(IComparable<>).Name)) ||
                (interfaceType.Name == "ISpanFormattable" &&
                (type.Name == "INumberBase`1" ||
                type.GetInterfaces().Any((i) => i.Name == "INumberBase`1"))) ||
                (interfaceType.Name == "ICollection" &&
                type.Name == "IProducerConsumerCollection`1"))
            {
                // TS interfaces cannot extend multiple interfaces that have non-identical methods
                // with the same name. This is most commonly an issue with IComparable and
                // ISpanFormattable/INumberBase generic and non-generic interfaces.
                return true;
            }

            return false;
        }
        else if (interfaceType.Name == "IReflectableType")
        {
            // Special case: Reflectable types have explicit implementations of this interface,
            // but they aren't detected by reflection due to the runtime type delegation.
            return true;
        }

        // Note the InterfaceMapping class is not supported for assemblies loaded by a
        // MetadataLoadContext, so the answer is a little harder to find.

        if (interfaceType.IsConstructedGenericType)
        {
            interfaceType = interfaceType.GetGenericTypeDefinition();
        }

        // Get the interface type name prefix for matching the method name.
        // It would be more precise to match the generic type params also,
        // but also more complicated.
        string methodNamePrefix = interfaceType.FullName!;
        int genericMarkerIndex = methodNamePrefix.IndexOf('`');
        if (genericMarkerIndex >= 0)
        {
#if !STRING_AS_SPAN
            methodNamePrefix = methodNamePrefix.Substring(0, genericMarkerIndex) + '<';
#else
            methodNamePrefix = string.Concat(methodNamePrefix.AsSpan(0, genericMarkerIndex), "<");
#endif
        }
        else
        {
            methodNamePrefix += '.';
        }

        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsFinal && method.IsPrivate && method.Name.StartsWith(methodNamePrefix))
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

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            return HasExplicitInterfaceImplementations(type.BaseType!, interfaceType);
        }

        return false;
    }

    private void GenerateGenericTypeFactory(ref SourceBuilder s, Type type)
    {
        GenerateDocComments(ref s, type, "[Generic type factory] ");
        string exportName = GetExportName(type);
        Type[] typeArgs = type.GetGenericArguments();
        string typeParams = string.Join(", ", typeArgs.Select((t) => $"{t.Name}: IType"));
        string typeParamsAsAny = string.Join(", ", typeArgs.Select((_) => $"any"));

        // TODO: Instead of `any` here, use TypeScript to map each generic type arg to JS.
        s += $"export function {exportName}$({typeParams}): " +
            (type.IsInterface || type.BaseType?.FullName == typeof(MulticastDelegate).FullName ?
            "IType;" : $"typeof {exportName}${typeArgs.Length}<{typeParamsAsAny}>;");
        s++;
        _emitType = true;
    }

    private void GenerateExtensionMethods(ref SourceBuilder s, Type type)
    {
        bool isStaticClass = type.IsAbstract && type.IsSealed;
        if (!type.IsClass || !isStaticClass || !IsExtensionMember(type))
        {
            return;
        }

        IEnumerable<IGrouping<Type, MethodInfo>> extensionMethodsByTargetType =
            type.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where((m) => IsExtensionMember(m) && IsExported(m) && !IsExcluded(m))
            .GroupBy((m) => m.GetParameters()[0].ParameterType)
            .Where((g) => IsExtensionTargetTypeSupported(g.Key));
        foreach (IGrouping<Type, MethodInfo> extensionMethodGroup in extensionMethodsByTargetType)
        {
            Type targetType = extensionMethodGroup.Key;
            if (targetType.IsConstructedGenericType)
            {
                // Extension methods for constructed generic types can't be represented in TS.
                continue;
            }

            s++;

            BeginNamespace(ref s, targetType);

            s += $"/** Extension methods from {{@link {type.Namespace}.{GetExportName(type)}}} */";

            if (targetType.IsGenericTypeDefinition)
            {
                string exportName = GetExportName(targetType);
                Type[] typeArgs = targetType.GetGenericArguments();
                string typeParams = string.Join(", ", typeArgs.Select((t) => t.Name));
                s += $"export interface {exportName}${typeArgs.Length}<{typeParams}> {{";
            }
            else
            {
                s += $"export interface {GetExportName(targetType)} {{";
            }

            bool isFirstMember = true;
            foreach (MethodInfo method in extensionMethodGroup)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                ExportTypeMember(ref s, method, asExtension: true);
            }

            s += "}";

            EndNamespace(ref s, targetType);
        }
    }

    private static bool IsExtensionMember(MemberInfo member)
    {
        return GetAttribute<ExtensionAttribute>(member) != null;
    }

    private static bool IsExtensionTargetTypeSupported(Type targetType)
    {
        if (targetType.IsValueType || targetType.IsPrimitive ||
            targetType == typeof(object) ||
            targetType == typeof(string) ||
            targetType == typeof(Type) ||
            targetType.IsArray ||
            (targetType.GetInterface(nameof(System.Collections.IEnumerable)) != null &&
             (targetType.Namespace == typeof(System.Collections.IEnumerable).Namespace ||
              targetType.Namespace == typeof(IEnumerable<>).Namespace)) ||
            targetType.Name.StartsWith("IAsyncEnumerable`") ||
            targetType.Name == nameof(Tuple) || targetType.Name.StartsWith(nameof(Tuple) + '`') ||
            targetType.Name == nameof(Task) || targetType.Name.StartsWith(nameof(Task) + '`'))
        {
            return false;
        }

        return true;
    }

    public string GenerateMemberDefinition(MemberInfo member)
    {
        SourceBuilder s = new();
        ExportTypeMember(ref s, member);
        return s.ToString();
    }

    private void ExportTypeMember(ref SourceBuilder s, MemberInfo member, bool asExtension = false)
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

            if (declaringType.IsAbstract && declaringType.IsSealed &&
                !declaringType.IsGenericTypeDefinition)
            {
                string varKind = property.SetMethod == null ? "const " : "var ";
                s += $"export {varKind}{propertyName}: {propertyType};";
            }
            else
            {
                bool isStatic = property.GetMethod?.IsStatic ??
                    property.SetMethod?.IsStatic ?? false;
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
            string parameters = GetTSParameters(
                asExtension ? method.GetParameters().Skip(1).ToArray() : method.GetParameters());
            string returnType = GetTSType(method.ReturnParameter);

            if (methodName == nameof(IDisposable.Dispose))
            {
                // Match JS disposable naming convention.
                methodName = "dispose";
            }

            if (declaringType.IsAbstract && declaringType.IsSealed && !asExtension &&
                !declaringType.IsGenericTypeDefinition)
            {
                s += "export function " +
                    $"{methodName}{genericParams}({parameters}): {returnType};";
            }
            else
            {
                s += (method.IsStatic && !asExtension ? "static " : "") +
                    $"{methodName}{genericParams}({parameters}): {returnType};";
            }
        }
    }

    private void BeginNamespace(ref SourceBuilder s, Type type)
    {
        if (_isModule)
        {
            // Modules with [JSExport] attributes are not namespaced.
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
        if (_isModule)
        {
            // Modules with [JSExport] attributes are not namespaced.
            return;
        }

        if (type.Namespace != null || type.IsNested)
        {
            s += "}";
        }
    }

    private static bool IsExcluded(MemberInfo member)
    {
        if (member is PropertyInfo property)
        {
            return IsExcluded(property);
        }
        else if (member is MethodBase method)
        {
            return IsExcluded(method);
        }

        Type type = member as Type ?? member.DeclaringType!;

        // While most types in InteropServices are excluded, safe handle classes are useful
        // to include because they are extended by handle classes in other assemblies.
        if (type.FullName == typeof(System.Runtime.InteropServices.SafeHandle).FullName ||
            type.FullName == typeof(System.Runtime.InteropServices.CriticalHandle).FullName)
        {
            return false;
        }

        // These namespaces contain APIs that are problematic for TS generation.
        // (Mostly old .NET Framework APIs.)
        return type.Namespace switch
        {
            "System.Runtime.CompilerServices" or
            "System.Runtime.InteropServices" or
            "System.Runtime.Remoting.Messaging" or
            "System.Runtime.Serialization" or
            "System.Security.AccessControl" or
            "System.Security.Policy" => true,
            _ => false,
        };
    }

    private static bool IsExcluded(PropertyInfo property)
    {
        if (property.PropertyType.IsPointer)
        {
            return true;
        }

        if (IsExcluded(property.PropertyType))
        {
            return true;
        }

        return false;
    }

    private static bool IsExcluded(MethodBase method)
    {
        // Exclude "special" methods like property get/set and event add/remove.
        if (method is MethodInfo && method.IsSpecialName)
        {
            return true;
        }

        // Exclude old style Begin/End async methods, as they always have Task-based alternatives.
        if ((method.Name.StartsWith("Begin") &&
            (method as MethodInfo)?.ReturnType.FullName == typeof(IAsyncResult).FullName) ||
            (method.Name.StartsWith("End") && method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType.FullName == typeof(IAsyncResult).FullName))
        {
            return true;
        }

        // Exclude instance methods declared by System.Object like ToString() and Equals().
        if (!method.IsStatic && method.DeclaringType!.FullName == "System.Object")
        {
            return true;
        }

        // Exclude methods that have pointer parameters because they can't be marshalled to JS.
        if (method.GetParameters().Any((p) => p.ParameterType.IsPointer) ||
            method is MethodInfo { ReturnParameter.ParameterType.IsPointer: true })
        {
            return true;
        }

        if (method.Name == nameof(TaskAsyncEnumerableExtensions.ConfigureAwait) &&
            method.DeclaringType?.FullName == typeof(TaskAsyncEnumerableExtensions).FullName)
        {
            // ConfigureAwait() doesn't work from JS.
            return true;
        }

        if (method.GetParameters().Any((p) => IsExcluded(p.ParameterType)) ||
            (method is MethodInfo methodWithReturn && IsExcluded(methodWithReturn.ReturnType)))
        {
            return true;
        }

        return false;
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
        // TypeScript does not allow type parameters in static members.
        // See https://github.com/microsoft/TypeScript/issues/32211
        bool allowTypeParameters =
            !(property.GetMethod?.IsStatic == true || property.SetMethod?.IsStatic == true);

        Type propertyType = property.PropertyType;
        if (propertyType.IsByRef)
        {
            propertyType = propertyType.GetElementType()!;
        }

        NullabilityInfo nullability = FixNullability(_nullabilityContext.Create(property));

#if NETFRAMEWORK || NETSTANDARD
        // IEnumerator.Current property is not attributed as nullable but should be.
        if (property.Name == nameof(System.Collections.IEnumerator.Current) &&
            property.DeclaringType.FullName == typeof(System.Collections.IEnumerator).FullName)
        {
            nullability = new NullabilityInfo(
                nullability.Type,
                NullabilityState.Nullable,
                nullability.WriteState,
                nullability.ElementType,
                nullability.GenericTypeArguments);
        }
#endif

        string tsType = GetTSType(
            propertyType,
            nullability,
            allowTypeParameters);

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

        // TypeScript does not allow type parameters in static members.
        // See https://github.com/microsoft/TypeScript/issues/32211
        bool allowTypeParameters = method?.IsStatic != true;

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
                        FixNullability(_nullabilityContext.Create(parameter)),
                        allowTypeParameters);
                    return $"{{ {resultName}: {tsType}, {outProperties} }}";
                }
            }
        }

        Type parameterType = parameter.ParameterType;
        if (parameterType.IsByRef)
        {
            parameterType = parameterType.GetElementType()!;
        }

        tsType = GetTSType(
            parameterType,
            FixNullability(_nullabilityContext.Create(parameter)),
            allowTypeParameters);
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

    private string GetTSType(
        Type type,
        NullabilityInfo? nullability,
        bool allowTypeParams = true)
    {
        string tsType = "unknown";
        if (type.IsPointer)
        {
            return tsType;
        }

        string? primitiveType = type.FullName switch
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
            "System.TimeSpan" => "number",
            "System.Guid" => "string",
            "System.Numerics.BigInteger" => "bigint",
            _ => null,
        };

        if (primitiveType != null)
        {
            tsType = primitiveType;
        }
#if NETFRAMEWORK || NETSTANDARD
        else if (type.IsGenericTypeParameter())
        {
            tsType = allowTypeParams ? type.Name : "any";
        }
        else if (type.IsGenericMethodParameter())
        {
            tsType = type.Name;
        }
#else
        else if (type.IsGenericTypeParameter)
        {
            tsType = allowTypeParams ? type.Name : "any";
        }
        else if (type.IsGenericMethodParameter)
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
            tsType = GetTSType(elementType, nullability?.ElementType, allowTypeParams) + "[]";
        }
        else if (type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
        {
            if (type.FullName == typeof(Action).FullName)
            {
                tsType = "() => void";
            }
            else if (type.IsGenericType && type.Name.StartsWith(nameof(Action) + "`"))
            {
                NullabilityInfo[]? typeArgsNullability = nullability?.GenericTypeArguments;
                string[] parameters = type.GetGenericArguments().Select((t, i) =>
                        $"arg{i + 1}: {GetTSType(t, typeArgsNullability?[i], allowTypeParams)}")
                    .ToArray();
                tsType = $"({string.Join(", ", parameters)}) => void";
            }
            else if (type.IsGenericType && type.Name.StartsWith("Func`"))
            {
                Type[] typeArgs = type.GetGenericArguments();
                NullabilityInfo[]? typeArgsNullability = nullability?.GenericTypeArguments;
                string[] parameters = typeArgs.Take(typeArgs.Length - 1).Select((t, i) =>
                        $"arg{i + 1}: {GetTSType(t, typeArgsNullability?[i], allowTypeParams)}")
                    .ToArray();
                string returnType = GetTSType(
                    typeArgs[typeArgs.Length - 1],
                    nullability?.GenericTypeArguments[typeArgs.Length - 1],
                    allowTypeParams);
                tsType = $"({string.Join(", ", parameters)}) => {returnType}";
            }
            else if (type.IsGenericType && type.Name.StartsWith("Predicate`"))
            {
                Type typeArg = type.GetGenericArguments()[0];
                NullabilityInfo[]? typeArgsNullability = nullability?.GenericTypeArguments;
                string tsTypeArg = GetTSType(typeArg, typeArgsNullability?[0], allowTypeParams);
                tsType = $"(value: {tsTypeArg}) => boolean";
            }
            else if (IsExported(type))
            {
                // Types exported from a module are not namespaced.
                string nsPrefix = !_isModule && type.Namespace != null ? type.Namespace + '.' : "";

                tsType = (type.IsNested ? GetTSType(type.DeclaringType!, null) + '.' : nsPrefix) +
                    (_isModule ? GetExportName(type) : type.Name);
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
        else if (type.FullName == typeof(DateTime).FullName)
        {
            _emitDateTime = true;
            tsType = (_isSystemAssembly ? "js." : "") + type.Name;
        }
        else if (type.FullName == typeof(DateTimeOffset).FullName)
        {
            _emitDateTimeOffset = true;
            tsType = (_isSystemAssembly ? "js." : "") + type.Name;
        }
        else if (IsExported(type))
        {
            // Types exported from a module are not namespaced.
            string nsPrefix = !_isModule && type.Namespace != null ? type.Namespace + '.' : "";

            tsType = (type.IsNested ? GetTSType(type.DeclaringType!, null) + '.' : nsPrefix) +
                (_isModule ? GetExportName(type) : type.Name);
        }
        else if (_referenceAssemblies.ContainsKey(type.Assembly.GetName().Name!))
        {
            tsType = type.IsNested ?
                GetTSType(type.DeclaringType!, null, allowTypeParams) + '.' + type.Name :
                (type.Namespace != null ? type.Namespace + '.' + type.Name : type.Name);
            _imports.Add(type.Assembly.GetName().Name!);
        }

        if (type.IsGenericType)
        {
            string typeDefinitionName = type.GetGenericTypeDefinition().FullName!;
            Type[] typeArgs = type.GetGenericArguments();
            NullabilityInfo[]? typeArgsNullability = nullability?.GenericTypeArguments;
            if (typeArgsNullability?.Length < typeArgs.Length)
            {
                // NullabilityContext doesn't handle generic type arguments of by-ref parameters.
                typeArgsNullability = null;
            }

            if (typeDefinitionName == typeof(Nullable<>).FullName)
            {
                tsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams) +
                    UndefinedTypeSuffix;
            }
            else if (typeDefinitionName == typeof(Task<>).FullName ||
                typeDefinitionName == typeof(ValueTask<>).FullName)
            {
                tsType = $"Promise<{GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams)}>";
            }
            else if (typeDefinitionName == typeof(Memory<>).FullName ||
                typeDefinitionName == typeof(ReadOnlyMemory<>).FullName)
            {
                Type elementType = typeArgs[0];
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
                string elementType =
                    GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                if (elementType.EndsWith(UndefinedTypeSuffix))
                {
                    elementType = $"({elementType})";
                }
                tsType = elementType + "[]";
            }
            else if (typeDefinitionName == typeof(IReadOnlyList<>).FullName)
            {
                string elementType =
                    GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                if (elementType.EndsWith(UndefinedTypeSuffix))
                {
                    elementType = $"({elementType})";
                }
                tsType = "readonly " + elementType + "[]";
            }
            else if (typeDefinitionName == typeof(ICollection<>).FullName)
            {
                string elementTsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                return $"Iterable<{elementTsType}> & {{ length: number, " +
                    $"add(item: {elementTsType}): void, delete(item: {elementTsType}): boolean }}";
            }
            else if (typeDefinitionName == typeof(IReadOnlyCollection<>).FullName ||
                typeDefinitionName == typeof(ReadOnlyCollection<>).FullName)
            {
                string elementTsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                return $"Iterable<{elementTsType}> & {{ length: number }}";
            }
            else if (typeDefinitionName == typeof(ISet<>).FullName)
            {
                string elementTsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                return $"Set<{elementTsType}>";
            }
#if READONLY_SET
            else if (typeDefinitionName == typeof(IReadOnlySet<>).FullName)
            {
                string elementTsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                return $"ReadonlySet<{elementTsType}>";
            }
#endif
            else if (typeDefinitionName == typeof(IEnumerable<>).FullName)
            {
                string elementTsType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                return $"Iterable<{elementTsType}>";
            }
            else if (typeDefinitionName == typeof(IDictionary<,>).FullName)
            {
                string keyTSType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                string valueTSType = GetTSType(typeArgs[1], typeArgsNullability?[1], allowTypeParams);
                tsType = $"Map<{keyTSType}, {valueTSType}>";
            }
            else if (typeDefinitionName == typeof(IReadOnlyDictionary<,>).FullName)
            {
                string keyTSType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                string valueTSType = GetTSType(typeArgs[1], typeArgsNullability?[1], allowTypeParams);
                tsType = $"ReadonlyMap<{keyTSType}, {valueTSType}>";
            }
            else if (typeDefinitionName == typeof(KeyValuePair<,>).FullName)
            {
                string keyTSType = GetTSType(typeArgs[0], typeArgsNullability?[0], allowTypeParams);
                string valueTSType = GetTSType(typeArgs[1], typeArgsNullability?[1], allowTypeParams);
                tsType = $"[{keyTSType}, {valueTSType}]";
            }
            else if (typeDefinitionName.StartsWith("System.Tuple`") ||
                typeDefinitionName.StartsWith("System.ValueTuple`"))
            {
                IEnumerable<string> itemTSTypes = typeArgs.Select((typeArg, index) =>
                    GetTSType(typeArg, typeArgsNullability?[index], allowTypeParams));
                tsType = $"[{string.Join(", ", itemTSTypes)}]";
            }
            else
            {
                int typeNameEnd = tsType.IndexOf('`');
                if (typeNameEnd > 0)
                {
                    tsType = tsType.Substring(0, typeNameEnd);

                    string typeParams = string.Join(", ", typeArgs.Select(
                        (t, i) => GetTSType(t, typeArgsNullability?[i], allowTypeParams)));
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
#if !(NETFRAMEWORK || NETSTANDARD)
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
        CustomAttributeData? attribute = GetAttribute<JSExportAttribute>(member);
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

    /// <summary>
    /// Loads the XML documentation files for the primary assembly and all reference assemblies.
    /// (Ignores any missing documentation files.)
    /// </summary>
    public void LoadAssemblyDocs()
    {
        LoadAssemblyDoc(_assembly);
        foreach (Assembly referenceAssembly in _referenceAssemblies.Values)
        {
            LoadAssemblyDoc(referenceAssembly);
        }
    }

    public void LoadAssemblyDoc(Assembly assembly)
    {
        string? assemblyDocFilePath = Path.ChangeExtension(assembly.Location, ".xml");
        if (!LoadAssemblyDoc(assembly.GetName().Name!, assemblyDocFilePath))
        {
            // Some doc XML files are missing the first-level namespace prefix.
            string assemblyFileName = Path.GetFileNameWithoutExtension(assembly.Location);
            assemblyDocFilePath = Path.Combine(
                Path.GetDirectoryName(assembly.Location)!,
#if !STRING_AS_SPAN
                assemblyFileName.Substring(assemblyFileName.IndexOf('.') + 1) + ".xml");
#else
                string.Concat(assemblyFileName.AsSpan(assemblyFileName.IndexOf('.') + 1), ".xml"));
#endif
            LoadAssemblyDoc(assembly.GetName().Name!, assemblyDocFilePath);
        }
    }

    public bool LoadAssemblyDoc(string assemblyName, string xmlDocFilePath)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlDocFilePath);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ReportWarning(
                DiagnosticId.DocLoadError,
                $"Failed to load assembly documentation XML file '{xmlDocFilePath}': {ex.Message}");
            return false;
        }

        LoadAssemblyDoc(assemblyName, doc);
        return true;
    }

    public void LoadAssemblyDoc(string assemblyName, XDocument doc)
    {
        _assemblyDocs[assemblyName] = doc;
    }

    private void GenerateDocComments(
        ref SourceBuilder s,
        MemberInfo member,
        string? summaryPrefix = null)
    {
        if (!_assemblyDocs.TryGetValue(
            member.Module.Assembly.GetName().Name!, out XDocument? assemblyDoc))
        {
            return;
        }

        string memberDocName = GetMemberDocName(member);
        XElement? memberElement = assemblyDoc?.Root?.Element("members")?.Elements("member")
            .FirstOrDefault((m) => m.Attribute("name")?.Value == memberDocName);

        // If the member doc is inherited, resolve the inherited member and use its assembly doc.
        MemberInfo? inheritedMember = member;
        while (memberElement?.Element("inheritdoc") != null && inheritedMember != null)
        {
            inheritedMember = GetInheritedMember(inheritedMember);
            if (inheritedMember != null && _assemblyDocs.TryGetValue(
                inheritedMember.Module.Assembly.GetName().Name!, out assemblyDoc))
            {
                string inheritedMemberDocName = GetMemberDocName(inheritedMember);
                memberElement = assemblyDoc?.Root?.Element("members")?.Elements("member")
                    .FirstOrDefault((m) => m.Attribute("name")?.Value == inheritedMemberDocName);
            }
        }

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
            s += $"/** {summary.Replace(NonBreakingSpace, ' ')} */";
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

    private static string GetMemberDocName(MemberInfo member)
    {
        return member switch
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
    }

    /// <summary>
    /// Gets the inherited declaration of a member from a base class or interface,
    /// for the purpose of inheriting documentation comments.
    /// </summary>
    private static MemberInfo? GetInheritedMember(MemberInfo member)
    {
        if (member.DeclaringType == null)
        {
            return null;
        }

        BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly |
            BindingFlags.Public | BindingFlags.NonPublic;

        if (member is PropertyInfo property)
        {
            Type[] indexParameterTypes =
                property.GetIndexParameters().Select((p) => p.ParameterType).ToArray();

            Type? baseType = member.DeclaringType.BaseType;
            while (baseType != null)
            {
                PropertyInfo? baseProperty = baseType.GetProperty(
                    property.Name,
                    bindingFlags,
                    binder: null,
                    property.PropertyType,
                    indexParameterTypes,
                    modifiers: null);
                if (baseProperty != null)
                {
                    return baseProperty;
                }

                baseType = baseType.BaseType;
            }

            foreach (Type interfaceType in member.DeclaringType.GetInterfaces())
            {
                PropertyInfo? interfaceProperty = interfaceType.GetProperty(
                    property.Name,
                    bindingFlags,
                    binder: null,
                    property.PropertyType,
                    indexParameterTypes,
                    modifiers: null);
                if (interfaceProperty != null)
                {
                    return interfaceProperty;
                }
            }
        }
        else if (member is MethodInfo method)
        {
            Type[] parameterTypes = method.GetParameters().Select((p) => p.ParameterType).ToArray();

            Type? baseType = member.DeclaringType.BaseType;
            while (baseType != null)
            {
                MethodInfo? baseMethod = baseType.GetMethod(
                    method.Name,
                    bindingFlags,
                    binder: null,
                    parameterTypes,
                    modifiers: null);
                if (baseMethod != null)
                {
                    return baseMethod;
                }

                baseType = baseType.BaseType;
            }

            foreach (Type interfaceType in member.DeclaringType.GetInterfaces())
            {
                MethodInfo? interfaceMethod = interfaceType.GetMethod(
                    method.Name,
                    bindingFlags,
                    binder: null,
                    parameterTypes,
                    modifiers: null);
                if (interfaceMethod != null)
                {
                    return interfaceMethod;
                }
            }
        }

        return null;
    }

    private static string FormatDocText(XNode? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (node is XElement element)
        {
            if (element.Name == "see" && element.Attribute("cref") != null)
            {
                string target = element.Attribute("cref")!.Value;
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

                // Use a non-breaking space char to prevent wrapping from breaking the link.
                // It will be replaced with by a regular space char in the final output.
                return $"{{@link {target}}}".Replace(' ', NonBreakingSpace);
            }
            else if (element.Name == "see" && element.Attribute("langword") != null)
            {
                string target = element.Attribute("langword")!.Value;
                return $"`{target}`";
            }
            else if (element.Name == "paramref" && element.Attribute("name") != null)
            {
                string target = element.Attribute("name")!.Value;
                return $"`{target}`";
            }
            else
            {
                return string.Join(" ", element.Nodes().Select(FormatDocText))
                    .Replace("} ,", "},")
                    .Replace("} .", "}.")
                    .Replace("` ,", "`,")
                    .Replace("` .", "`.");
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
#if NETFRAMEWORK || NETSTANDARD
        if (type.IsGenericTypeParameter() && genericTypeParams != null)
        {
            return "`" + Array.IndexOf(genericTypeParams, type);
        }
        else if (type.IsGenericMethodParameter() && genericMethodParams != null)
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
