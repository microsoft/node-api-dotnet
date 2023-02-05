using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace NodeApi.Generator;

/// <summary>
/// Base class for source generators for C# APIs exported to JS.
/// Contains shared definitions and utility methods.
/// </summary>
public abstract class SourceGenerator
{
    private const string DiagnosticPrefix = "NAPI";
    private const string DiagnosticCategory = "NodeApi";

    private static readonly Regex s_paragraphBreakRegex = new(@" *\<para */\> *");

    public enum DiagnosticId
    {
        GeneratorError = 1000,
        MultipleModuleAttributes,
        InvalidModuleInitializer,
        ModuleInitializerIsNotPublic,
        ModuleInitializerIsNotStatic,
        ExportIsNotPublic,
        ExportIsNotStatic,
        UnsupportedTypeKind,
        UnsupportedPropertyType,
        UnsupportedMethodParameterType,
        UnsupportedMethodReturnType,
        UnsupportedOverloads,
        ReferenedTypeNotExported,
    }

    public GeneratorExecutionContext Context { get; protected set; }

    public static string GetNamespace(ISymbol symbol)
    {
        string ns = string.Empty;
        for (INamespaceSymbol s = symbol.ContainingNamespace;
            !s.IsGlobalNamespace;
            s = s.ContainingNamespace)
        {
            ns = s.Name + (ns.Length > 0 ? "." + ns : string.Empty);
        }
        return ns;
    }

    public static string ToCamelCase(string name)
    {
        StringBuilder sb = new(name);
        sb[0] = char.ToLowerInvariant(sb[0]);
        return sb.ToString();
    }

    // An analyzer bug results in incorrect reports of CA1822 against this method. (It can't be static.)
#pragma warning disable CA1822 // Mark members as static
    public void ReportError(
        DiagnosticId id,
        ISymbol? symbol,
        string title,
        string? description = null)
    {
        ReportDiagnostic(
            DiagnosticSeverity.Error,
            id,
            symbol?.Locations.Single(),
            title,
            description);
    }
#pragma warning restore CA1822 // Mark members as static

    public void ReportDiagnostic(
        DiagnosticSeverity severity,
        DiagnosticId id,
        Location? location,
        string title,
        string? description = null)
    {
        var descriptor = new DiagnosticDescriptor(
            id: DiagnosticPrefix + id,
            title,
            messageFormat: title +
            (!string.IsNullOrEmpty(description) ? " " + description : string.Empty),
            DiagnosticCategory,
            severity,
            isEnabledByDefault: true);
        Context.ReportDiagnostic(
            Diagnostic.Create(descriptor, location));
    }

    protected static IEnumerable<string> WrapComment(string comment, int wrapColumn)
    {
        bool isFirst = true;
        foreach (string paragraph in s_paragraphBreakRegex.Split(comment))
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                // Insert a blank line between paragraphs.
                yield return string.Empty;
            }

            comment = paragraph;
            while (comment.Length > wrapColumn)
            {
                int i = wrapColumn;
                while (i > 0 && comment[i] != ' ')
                {
                    i--;
                }

                if (i == 0)
                {
                    i = comment.IndexOf(' ');
                }

                yield return comment.Substring(0, i).TrimEnd();
                comment = comment.Substring(i + 1);
            }

            yield return comment.TrimEnd();
        }
    }
}
