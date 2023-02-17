using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi.Generator;

// An analyzer bug results in incorrect reports of CA1822 against methods in this class.
#pragma warning disable CA1822 // Mark members as static

internal class TypeDefinitionsGenerator : SourceGenerator
{
    private static readonly Regex s_newlineRegex = new("\n *");
    private static readonly Regex s_summaryRegex = new("<summary>(.*)</summary>");
    private static readonly Regex s_remarksRegex = new("<remarks>(.*)</remarks>");

    private readonly IEnumerable<ISymbol> _exportItems;

    public TypeDefinitionsGenerator(IEnumerable<ISymbol> exportItems)
    {
        _exportItems = exportItems;
    }

    internal SourceText GenerateTypeDefinitions()
    {
        var s = new SourceBuilder();

        s += "// Generated type definitions for .NET module";

        foreach (ISymbol exportItem in _exportItems)
        {
            if (exportItem is ITypeSymbol exportClass &&
                (exportClass.TypeKind == TypeKind.Class || exportClass.TypeKind == TypeKind.Struct))
            {
                GenerateClassDefinition(ref s, exportClass);
            }
            else if (exportItem is ITypeSymbol exportEnum && exportEnum.TypeKind == TypeKind.Enum)
            {
                GenerateEnumDefinition(ref s, exportEnum);
            }
            else if (exportItem is IMethodSymbol exportMethod)
            {
                s++;
                GenerateDocComments(ref s, exportItem);
                string exportName = ModuleGenerator.GetExportName(exportItem);
                string parameters = GetTSParameters(exportMethod, s.Indent);
                string returnType = GetTSType(exportMethod.ReturnType);
                s += $"export declare function {exportName}({parameters}): {returnType};";
            }
            else if (exportItem is IPropertySymbol exportProperty)
            {
                s++;
                GenerateDocComments(ref s, exportItem);
                string exportName = ModuleGenerator.GetExportName(exportItem);
                string propertyType = GetTSType(exportProperty.Type);
                string varKind = exportProperty.SetMethod == null ? "const " : "var ";
                s += $"export declare {varKind}{exportName}: {propertyType};";
            }
        }

        return s;
    }

    private void GenerateClassDefinition(ref SourceBuilder s, ITypeSymbol exportClass)
    {
        s++;
        GenerateDocComments(ref s, exportClass);
        string classKind = exportClass.IsStatic ? "namespace" : "class";
        string exportName = ModuleGenerator.GetExportName(exportClass);
        s += $"export declare {classKind} {exportName} {{";

        bool isFirstMember = true;
        foreach (ISymbol member in exportClass.GetMembers()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            string memberName = ToCamelCase(member.Name);

            if (!exportClass.IsStatic &&
                member is IMethodSymbol exportConstructor &&
                exportConstructor.MethodKind == MethodKind.Constructor &&
                !exportConstructor.IsImplicitlyDeclared)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, member);
                string parameters = GetTSParameters(exportConstructor, s.Indent);
                s += $"constructor({parameters});";
            }
            else if (member is IMethodSymbol exportMethod &&
                exportMethod.MethodKind == MethodKind.Ordinary)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, member);
                string parameters = GetTSParameters(exportMethod, s.Indent);
                string returnType = GetTSType(exportMethod.ReturnType);

                if (exportClass.IsStatic)
                {
                    s += "export declare function " +
                        $"{memberName}({parameters}): {returnType};";
                }
                else
                {
                    s += $"{(member.IsStatic ? "static " : "")}{memberName}({parameters}): " +
                        $"{returnType};";
                }
            }
            else if (member is IPropertySymbol exportProperty)
            {
                if (isFirstMember) isFirstMember = false; else s++;
                GenerateDocComments(ref s, member);
                string propertyType = GetTSType(exportProperty.Type);

                if (exportClass.IsStatic)
                {
                    string varKind = exportProperty.SetMethod == null ? "const " : "var ";
                    s += $"export declare {varKind}{memberName}: {propertyType};";
                }
                else
                {
                    string readonlyModifier =
                        exportProperty.SetMethod == null ? "readonly " : "";
                    s += $"{(member.IsStatic ? "static " : "")}{readonlyModifier}{memberName}: " +
                        $"{propertyType};";
                }
            }
        }

        s += "}";
    }

    private void GenerateEnumDefinition(ref SourceBuilder s, ITypeSymbol exportEnum)
    {
        s++;
        GenerateDocComments(ref s, exportEnum);
        string exportName = ModuleGenerator.GetExportName(exportEnum);
        s += $"export declare enum {exportName} {{";

        bool isFirstMember = true;
        foreach (IFieldSymbol field in exportEnum.GetMembers().OfType<IFieldSymbol>())
        {
            if (isFirstMember) isFirstMember = false; else s++;
            GenerateDocComments(ref s, field);
            s += $"{field.Name} = {field.ConstantValue},";
        }

        s += "}";
    }

    private string GetTSType(ITypeSymbol type)
    {
        string tsType = "unknown";

        string? specialType = type.SpecialType switch
        {
            SpecialType.System_Void => "void",
            SpecialType.System_Boolean => "boolean",
            SpecialType.System_SByte => "number",
            SpecialType.System_Int16 => "number",
            SpecialType.System_Int32 => "number",
            SpecialType.System_Int64 => "number",
            SpecialType.System_Byte => "number",
            SpecialType.System_UInt16 => "number",
            SpecialType.System_UInt32 => "number",
            SpecialType.System_UInt64 => "number",
            SpecialType.System_Single => "number",
            SpecialType.System_Double => "number",
            SpecialType.System_String => "string",
            ////SpecialType.System_DateTime => "Date",
            _ => null,
        };

        if (specialType != null)
        {
            tsType = specialType;
        }
        else if (type.TypeKind == TypeKind.Array)
        {
            ITypeSymbol elementType = ((IArrayTypeSymbol)type).ElementType;
            tsType = GetTSType(elementType) + "[]";
        }
        else if (type is INamedTypeSymbol namedType && namedType.TypeParameters.Length > 0)
        {
            if (namedType.OriginalDefinition.Name == "Nullable")
            {
                tsType = GetTSType(namedType.TypeArguments[0]) + " | null";
            }
            else if (namedType.OriginalDefinition.Name == "Memory")
            {
                ITypeSymbol elementType = namedType.TypeArguments[0];
                tsType = elementType.SpecialType switch
                {
                    SpecialType.System_SByte => "Int8Array",
                    SpecialType.System_Int16 => "Int16Array",
                    SpecialType.System_Int32 => "Int32Array",
                    SpecialType.System_Int64 => "BigInt64Array",
                    SpecialType.System_Byte => "Uint8Array",
                    SpecialType.System_UInt16 => "Uint16Array",
                    SpecialType.System_UInt32 => "Uint32Array",
                    SpecialType.System_UInt64 => "BigUint64Array",
                    SpecialType.System_Single => "Float32Array",
                    SpecialType.System_Double => "Float64Array",
                    _ => "unknown",
                };
            }
            else if (namedType.OriginalDefinition.Name == "IList")
            {
                tsType = GetTSType(namedType.TypeArguments[0]) + "[]";
            }
            else if (namedType.OriginalDefinition.Name == "IReadOnlyList")
            {
                tsType = "readonly " + GetTSType(namedType.TypeArguments[0]) + "[]";
            }
            else if (namedType.OriginalDefinition.Name == "ICollection")
            {
                string elementTsType = GetTSType(namedType.TypeArguments[0]);
                return $"Iterable<{elementTsType}> & {{ length: number }}";
            }
            else if (namedType.OriginalDefinition.Name == "IReadOnlyCollection")
            {
                string elementTsType = GetTSType(namedType.TypeArguments[0]);
                return $"Iterable<{elementTsType}> & {{ length: number, " +
                    $"add(item: {elementTsType}): this, delete(item: {elementTsType}): boolean }}";
            }
            else if (namedType.OriginalDefinition.Name == "ISet")
            {
                string elementTsType = GetTSType(namedType.TypeArguments[0]);
                return $"Set<{elementTsType}>";
            }
            else if (namedType.OriginalDefinition.Name == "IReadOnlySet")
            {
                string elementTsType = GetTSType(namedType.TypeArguments[0]);
                return $"ReadonlySet<{elementTsType}>";
            }
            else if (namedType.OriginalDefinition.Name == "IEnumerable")
            {
                string elementTsType = GetTSType(namedType.TypeArguments[0]);
                return $"Iterable<{elementTsType}>";
            }
            else if (namedType.OriginalDefinition.Name == "IDictionary")
            {
                string keyTSType = GetTSType(namedType.TypeArguments[0]);
                string valueTSType = GetTSType(namedType.TypeArguments[1]);
                tsType = $"Map<{keyTSType}, {valueTSType}>";
            }
            else if (namedType.OriginalDefinition.Name == "IReadOnlyDictionary")
            {
                string keyTSType = GetTSType(namedType.TypeArguments[0]);
                string valueTSType = GetTSType(namedType.TypeArguments[1]);
                tsType = $"ReadonlyMap<{keyTSType}, {valueTSType}>";
            }
        }
        else if (_exportItems.Contains(type, SymbolEqualityComparer.Default))
        {
            tsType = type.Name;
        }

        if (type.NullableAnnotation == NullableAnnotation.Annotated &&
            tsType != "any" && !tsType.EndsWith(" | null"))
        {
            tsType += " | null";
        }

        return tsType;
    }

    private string GetTSParameters(IMethodSymbol method, string indent)
    {
        if (method.Parameters.Length == 0)
        {
            return string.Empty;
        }
        else if (method.Parameters.Length == 1)
        {
            string parameterType = GetTSType(method.Parameters[0].Type);
            return $"{method.Parameters[0].Name}: {parameterType}";
        }

        var s = new StringBuilder();
        s.AppendLine();

        foreach (IParameterSymbol p in method.Parameters)
        {
            string parameterType = GetTSType(p.Type);
            s.AppendLine($"{indent}\t{p.Name}: {parameterType},");
        }

        s.Append(indent);
        return s.ToString();
    }

    private static void GenerateDocComments(ref SourceBuilder s, ISymbol symbol)
    {
        string? comment = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(comment))
        {
            return;
        }

        comment = comment.Replace("\r", "");
        comment = s_newlineRegex.Replace(comment, " ");
        /*
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?(\\w+)\\.(\\w+)\" ?/>")
            .Replace(comment, (m) => $"{{@link {m.Groups[2].Value}.{ToCamelCase(m.Groups[3].Value)}}}");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?([^\"]+)\" ?/>")
            .Replace(comment, "{@link $2}");
        */

        string summary = s_summaryRegex.Match(comment).Groups[1].Value.Trim();
        string remarks = s_remarksRegex.Match(comment).Groups[1].Value.Trim();

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
