// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi.Generator;

// An analyzer bug results in incorrect reports of CA1822 against methods in this class.
#pragma warning disable CA1822 // Mark members as static

/// <summary>
/// Base class for source generators for C# APIs exported to JS.
/// Contains shared definitions and utility methods.
/// </summary>
public abstract class SourceGenerator
{
    protected const string DiagnosticPrefix = "NAPI";
    private const string DiagnosticCategory = "NodeApi";

    private static readonly Regex s_paragraphBreakRegex = new(@" *\<para */\> *");

    protected const char NonBreakingSpace = (char)0xA0;

    public enum DiagnosticId
    {
        NoExports = 1000,
        GeneratorError = 1001,
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

    public static string GetFullName(ISymbol symbol)
    {
        string ns = GetNamespace(symbol);
        string name = (symbol as INamedTypeSymbol)?.OriginalDefinition?.Name ?? symbol.Name;

        while (symbol.ContainingSymbol is INamedTypeSymbol containingType)
        {
            name = $"{containingType.OriginalDefinition.Name}.{name}";
            symbol = containingType;
        }

        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public static string ToCamelCase(string name)
    {
        StringBuilder sb = new(name);
        sb[0] = char.ToLowerInvariant(sb[0]);
        return sb.ToString();
    }

    public void ReportException(Exception ex)
    {
        // The compiler diagnostic will only show up to the first \r or \n.
        // So concatenate the first few lines of the stack trace with no newlines.
        string message = string.Concat(new[] { ": ", ex.Message }.Concat(
            (ex.StackTrace ?? string.Empty).Replace("\r", "").Split('\n').Take(10)));
        ReportError(DiagnosticId.GeneratorError, ex.GetType().Name, message);
    }

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

    public void ReportError(
        DiagnosticId id,
        string title,
        string? description = null)
    {
        ReportDiagnostic(
            DiagnosticSeverity.Error,
            id,
            null,
            title,
            description);
    }

    public void ReportWarning(
        DiagnosticId id,
        ISymbol? symbol,
        string title,
        string? description = null)
    {
        ReportDiagnostic(
            DiagnosticSeverity.Warning,
            id,
            symbol?.Locations.Single(),
            title,
            description);
    }

    public void ReportWarning(
        DiagnosticId id,
        string title,
        string? description = null)
    {
        ReportDiagnostic(
            DiagnosticSeverity.Warning,
            id,
            null,
            title,
            description);
    }

    protected void ReportDiagnostic(
        DiagnosticSeverity severity,
        DiagnosticId id,
        Location? location,
        string title,
        string? description = null)
    {
        ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: DiagnosticPrefix + (int)id,
                title,
                messageFormat: title +
                    (!string.IsNullOrEmpty(description) ? " " + description : string.Empty),
                DiagnosticCategory,
                severity,
                isEnabledByDefault: true,
                description),
            location));
    }

    public abstract void ReportDiagnostic(Diagnostic diagnostic);

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
                    if (i < 0)
                    {
                        break;
                    }
                }

                yield return comment.Substring(0, i).TrimEnd().Replace(NonBreakingSpace, ' ');
                comment = comment.Substring(i + 1);
            }

            yield return comment.TrimEnd().Replace(NonBreakingSpace, ' ');
        }
    }
}
