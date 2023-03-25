// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.JavaScript.NodeApi.Interop;
using NullabilityInfo = System.Reflection.NullabilityInfo;

namespace Microsoft.JavaScript.NodeApi.Generator;

// This class is packaged with the analyzer, but runs as a separate command-line tool.
#pragma warning disable RS1035 // Do not do file IO in alayzers

/// <summary>
/// Generats TypeScript type definitions for .NET APIs exported to JavaScript.
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
    private static readonly Regex s_newlineRegex = new("\n *");

    private readonly NullabilityInfoContext _nullabilityContext = new();

    private readonly Assembly _assembly;
    private readonly IDictionary<string, Assembly> _referenceAssemblies;
    private readonly XDocument? _assemblyDoc;
    private bool _exportAll;
    private bool _autoCamelCase;
    private bool _emitDisposable;
    private bool _emitCancellation;
    private bool _emitDuplex;

    public static void GenerateTypeDefinitions(
        string assemblyPath,
        IEnumerable<string> referenceAssemblyPaths,
        string typeDefinitionsPath)
    {
        // Create a metadata load context that includes a resolver for .NET runtime assemblies
        // along with the NodeAPI assembly and the target assembly.

        string[] runtimeAssemblies = Directory.GetFiles(
            RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        PathAssemblyResolver assemblyResolver = new(
            runtimeAssemblies
            .Concat(referenceAssemblyPaths)
            .Concat(new[]
            {
                typeof(JSExportAttribute).Assembly.Location,
                assemblyPath,
            }));
        using MetadataLoadContext loadContext = new(
            assemblyResolver, typeof(object).Assembly.GetName().Name);

        Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

        Dictionary<string, Assembly> referenceAssemblies = new();
        foreach (var referenceAssemblyPath in referenceAssemblyPaths)
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
                assemblyFileName.Substring(assemblyFileName.IndexOf('.') + 1) + ".xml");
        }

        if (File.Exists(assemblyDocFilePath))
        {
            assemblyDoc = XDocument.Load(assemblyDocFilePath);
        }

        string[] referenceAssemblyNames = referenceAssemblyPaths
            .Select((r) => Path.GetFileNameWithoutExtension(r))
            .ToArray();

        TypeDefinitionsGenerator generator = new(assembly, assemblyDoc, referenceAssemblies);
        SourceText generatedSource = generator.GenerateTypeDefinitions();

        File.WriteAllText(typeDefinitionsPath, generatedSource.ToString());
    }

    public TypeDefinitionsGenerator(
        Assembly assembly,
        XDocument? assemblyDoc,
        IDictionary<string, Assembly> referenceAssemblies)
    {
        _assembly = assembly;
        _assemblyDoc = assemblyDoc;
        _referenceAssemblies = referenceAssemblies;
    }

    public override void ReportDiagnostic(Diagnostic diagnostic)
    {
        string severity = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };
        Console.WriteLine($"{severity} {diagnostic.Id}: {diagnostic.GetMessage()}");
    }

    public SourceText GenerateTypeDefinitions()
    {
        var s = new SourceBuilder();

        string generatorName = typeof(TypeDefinitionsGenerator).Assembly.GetName()!.Name!;
        Version? generatorVersion = typeof(TypeDefinitionsGenerator).Assembly.GetName().Version;
        s += $"// Generated by {generatorName} {generatorVersion}";

        if (_referenceAssemblies.Count > 0)
        {
            s++;
            foreach (var referenceName in _referenceAssemblies.Keys)
            {
                string importName = referenceName.Replace('.', '_');
                s += $"import * as {importName} from './{referenceName}';";
            }
        }

        _exportAll = !AreAnyItemsExported();
        _autoCamelCase = !_exportAll;

        foreach (Type type in _assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (IsTypeExported(type))
            {
                ExportType(ref s, type);
            }
            else
            {
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
                {
                    if (IsMemberExported(member))
                    {
                        ExportMember(ref s, member);
                    }
                }
            }
        }

        GenerateSupportingInterfaces(ref s);

        return s;
    }

    private bool IsTypeExported(Type type)
    {
        if (type.Assembly != _assembly)
        {
            return false;
        }

        return _exportAll || type.GetCustomAttributesData().Any((a) =>
            a.AttributeType.FullName == typeof(JSModuleAttribute).FullName ||
            a.AttributeType.FullName == typeof(JSExportAttribute).FullName);
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

    private void ExportType(ref SourceBuilder s, Type type)
    {
        if (type.IsClass || type.IsInterface ||
            (type.IsValueType && !type.IsEnum))
        {
            GenerateClassDefinition(ref s, type);
        }
        else if (type.IsEnum)
        {
            GenerateEnumDefinition(ref s, type);
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
            s += $"export declare function {exportName}({parameters}): {returnType};";
        }
        else if (member is PropertyInfo property)
        {
            s++;
            GenerateDocComments(ref s, property);
            string exportName = GetExportName(property);
            string propertyType = GetTSType(property);
            string varKind = property.SetMethod == null ? "const " : "var ";
            s += $"export declare {varKind}{exportName}: {propertyType};";
        }
        else
        {
            // TODO: Events, const fields?
        }
    }

    private void GenerateSupportingInterfaces(ref SourceBuilder s)
    {
        if (_emitCancellation)
        {
            s++;
            s += "export interface CancellationToken {";
            s += "readonly isCancellationRequested: boolean;";
            s += "readonly onCancellationRequested: (listener: (e: any) => any) => IDisposable;";
            s += "}";
        }

        if (_emitDisposable || _emitCancellation)
        {
            s++;
            s += "export interface IDisposable {";
            s += "dispose(): void;";
            s += "}";
        }

        if (_emitDuplex)
        {
            s++;
            s += "import { Duplex } from 'stream';";
        }
    }

    private void GenerateClassDefinition(ref SourceBuilder s, Type type)
    {
        s++;
        GenerateDocComments(ref s, type);
        string classKind = type.IsInterface ? "interface" :
            (type.IsAbstract && type.IsSealed) ? "declare namespace" : "declare class";

        string implements = string.Empty;
        /*
        foreach (INamedTypeSymbol? implemented in exportClass.Interfaces.Where(
            (type) => _exportItems.Contains(type, SymbolEqualityComparer.Default)))
        {
            implements += (implements.Length == 0 ? " implements " : ", ");
            implements += implemented.Name;
        }
        */

        bool isStreamSubclass = type.BaseType?.FullName == typeof(Stream).FullName;
        if (isStreamSubclass)
        {
            implements = " extends Duplex";
            _emitDuplex = true;
        }

        string exportName = GetExportName(type);
        if (type.IsNested)
        {
            exportName = GetExportName(type.DeclaringType!) + "_" + exportName;
        }

        s += $"export {classKind} {exportName}{implements} {{";

        bool isFirstMember = true;
        foreach (MemberInfo member in type.GetMembers(
            BindingFlags.Public | BindingFlags.DeclaredOnly |
            BindingFlags.Static | BindingFlags.Instance))
        {
            string memberName = GetExportName(member);

            if (!(type.IsAbstract && type.IsSealed) && member is ConstructorInfo constructor)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, constructor);
                string parameters = GetTSParameters(constructor.GetParameters());
                s += $"constructor({parameters});";
            }
            else if (!isStreamSubclass && member is MethodInfo method && !IsExcludedMethod(method))
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, method);
                string methodName = TSIdentifier(memberName);
                string parameters = GetTSParameters(method.GetParameters());
                string returnType = GetTSType(method.ReturnParameter);

                if (type.IsAbstract && type.IsSealed)
                {
                    s += "export function " +
                        $"{methodName}({parameters}): {returnType};";
                }
                else
                {
                    s += $"{(method.IsStatic ? "static " : "")}{methodName}({parameters}): " +
                        $"{returnType};";
                }
            }
            else if (!isStreamSubclass && member is PropertyInfo property)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, member);
                string propertyName = TSIdentifier(memberName);
                string propertyType = GetTSType(property);

                if (type.IsAbstract && type.IsSealed)
                {
                    string varKind = property.SetMethod == null ? "const " : "var ";
                    s += $"export {varKind}{propertyName}: {propertyType};";
                }
                else
                {
                    bool isStatic = property.GetMethod?.IsStatic ??
                        property.SetMethod?.IsStatic ?? false;
                    string readonlyModifier = property.SetMethod == null ? "readonly " : "";
                    s += $"{(isStatic ? "static " : "")}{readonlyModifier}{propertyName}: " +
                        $"{propertyType};";
                }
            }
        }

        s += "}";

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            ExportType(ref s, nestedType);
        }
    }

    private static bool IsExcludedMethod(MethodInfo method)
    {
        // Exclude "special" methods like property get/set and event add/remove.
        // Exclude old style Begin/End async methods, as they always have Task-based alternatives.
        return method.IsSpecialName ||
            (method.Name.StartsWith("Begin") &&
                method.ReturnType.FullName == typeof(IAsyncResult).FullName) ||
            (method.Name.StartsWith("End") && method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType.FullName == typeof(IAsyncResult).FullName);
    }

    private void GenerateEnumDefinition(ref SourceBuilder s, Type type)
    {
        s++;
        GenerateDocComments(ref s, type);
        string exportName = GetExportName(type);
        if (type.IsNested)
        {
            exportName = GetExportName(type.DeclaringType!) + "_" + exportName;
        }

        s += $"export declare enum {exportName} {{";

        bool isFirstMember = true;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (isFirstMember) isFirstMember = false; else s++;
            GenerateDocComments(ref s, field);
            s += $"{field.Name} = {field.GetRawConstantValue()},";
        }

        s += "}";
    }

    private string GetTSType(PropertyInfo property)
    {
        string tsType = GetTSType(property.PropertyType, _nullabilityContext.Create(property));

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
        string tsType = GetTSType(parameter.ParameterType, _nullabilityContext.Create(parameter));

        if (tsType == "unknown" || tsType.Contains("unknown"))
        {
            string className = parameter.Member.DeclaringType!.Name;
            string typeName = ExpressionExtensions.FormatType(parameter.ParameterType);

            MethodInfo? method = parameter.Member as MethodInfo;
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

    private string GetTSType(Type type, NullabilityInfo? nullability)
    {
        string? tsType = "unknown";

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
        else if (type.IsGenericType)
        {
            string typeDefinitionName = type.GetGenericTypeDefinition().FullName!;
            Type[] typeArguments = type.GetGenericArguments();
            NullabilityInfo[]? typeArgumentsNullability = nullability?.GenericTypeArguments;
            if (typeDefinitionName == typeof(Nullable<>).FullName)
            {
                tsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]) + " | null";
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
            else if (typeDefinitionName == typeof(IReadOnlySet<>).FullName)
            {
                string elementTsType = GetTSType(typeArguments[0], typeArgumentsNullability?[0]);
                return $"ReadonlySet<{elementTsType}>";
            }
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
        }
        else if (type.FullName == typeof(Task).FullName ||
            type.FullName == typeof(ValueTask).FullName)
        {
            tsType = "Promise<void>";
        }
        else if (type.FullName == typeof(CancellationToken).FullName)
        {
            tsType = type.Name;
            _emitCancellation = true;
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
        else if (type.IsNested && !type.IsGenericTypeParameter && !type.IsGenericMethodParameter)
        {
            tsType = GetTSType(type.DeclaringType!, null) + "_" + type.Name;
        }
        else if (IsTypeExported(type))
        {
            tsType = type.Name;
        }
        else if (_referenceAssemblies.ContainsKey(type.Assembly.GetName().Name!))
        {
            string importName = type.Assembly.GetName().Name!.Replace('.', '_');
            tsType = $"{importName}.{type.Name}";
        }

        if (nullability?.ReadState == NullabilityState.Nullable &&
            tsType != "any" && !tsType.EndsWith(" | null"))
        {
            tsType += " | null";
        }

        return tsType;
    }

    private string GetTSParameters(ParameterInfo[] parameters)
    {
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
                return $"{TSIdentifier(parameters[0].Name)}: {parameterType}";
            }
        }

        var s = new StringBuilder();
        s.AppendLine();

        foreach (ParameterInfo p in parameters)
        {
            string parameterType = GetTSType(p);
            s.AppendLine($"{TSIdentifier(p.Name)}: {parameterType},");
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
            if ((member as Type)?.IsGenericTypeDefinition == true)
            {
                // TODO: Handle generic types and interfaces, somehow.
                name = name.Substring(0, name.IndexOf('`'));
            }

            return _autoCamelCase && member is not Type ? ToCamelCase(name) : name;
        }
    }

    private void GenerateDocComments(ref SourceBuilder s, MemberInfo member)
    {
        string memberDocName = member switch
        {
            Type type => $"T:{type.FullName}",
            PropertyInfo property => $"P:{property.DeclaringType!.FullName}.{property.Name}",
            MethodInfo method => $"M:{method.DeclaringType!.FullName}.{method.Name}" +
                FormatDocMemberParameters(method.GetParameters()),
            ConstructorInfo constructor => $"M:{constructor.DeclaringType!.FullName}.#ctor" +
                FormatDocMemberParameters(constructor.GetParameters()),
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

        string summary = FormatDocText(summaryElement);
        string remarks = FormatDocText(remarksElement);

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

    private static string FormatDocMemberParameters(ParameterInfo[] parameters)
    {
        return parameters.Length == 0 ? string.Empty :
            '(' + string.Join(",", parameters.Select(
                (p) => FormatDocMemberParameterType(p.ParameterType))) + ')';
    }

    private static string FormatDocMemberParameterType(Type type)
    {
        if (type.IsGenericType)
        {
            string typeName = type.Name.Substring(0, type.Name.IndexOf('`'));
            string typeArgs = string.Join(
                ", ", type.GenericTypeArguments.Select(FormatDocMemberParameterType));
            return $"{type.Namespace}.{typeName}{{{typeArgs}}}";
        }
        else
        {
            return type.FullName!;
        }
    }

    private static string TSIdentifier(string? identifier)
    {
        switch (identifier)
        {
            // A method parameter named "function" is valid in C# but invalid in TS.
            case "function":
                return "_" + identifier;
            case null:
                return "_";
            default:
                return identifier;
        }
    }
}
