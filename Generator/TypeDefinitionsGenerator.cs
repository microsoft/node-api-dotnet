using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi.Generator;

internal class TypeDefinitionsGenerator : SourceGenerator
{
    private static readonly Regex s_newlineRegex = new("\n *");
    private static readonly Regex s_summaryRegex = new("<summary>(.*)</summary>");
    private static readonly Regex s_remarksRegex = new("<remarks>(.*)</remarks>");

    internal static SourceText GenerateTypeDefinitions(IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "// Generated type definitions for .NET module";

        foreach (ISymbol exportItem in exportItems)
        {
            if (exportItem is ITypeSymbol exportType &&
                (exportType.TypeKind == TypeKind.Class || exportType.TypeKind == TypeKind.Struct))
            {
                GenerateClassTypeDefinitions(ref s, exportType);
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

    private static void GenerateClassTypeDefinitions(ref SourceBuilder s, ITypeSymbol exportClass)
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

    private static string GetTSType(ITypeSymbol type)
    {
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
            return specialType;
        }

        if (type.TypeKind == TypeKind.Class)
        {
            // TODO: Check if class is exported.
        }
        else if (type.TypeKind == TypeKind.Array)
        {
            // TODO: Get element type.
            return "any[]";
        }

        return "any";
    }

    private static string GetTSParameters(IMethodSymbol method, string indent)
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
