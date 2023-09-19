// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.JavaScript.NodeApi.Generator;

internal class SourceBuilder : SourceText
{
    private readonly StringBuilder _text;
    private string _currentIndent = string.Empty;
    private int _extraIndentLevel;

    public SourceBuilder(string indent = "\t")
    {
        _text = new StringBuilder();
        Indent = indent;
    }

    public override Encoding? Encoding => Encoding.UTF8;

    public override int Length => _text.Length;

    public override char this[int position] => _text[position];

    public override void CopyTo(
        int sourceIndex, char[] destination, int destinationIndex, int count)
    {
        _text.CopyTo(sourceIndex, destination, destinationIndex, count);
    }

    public override string ToString() => _text.ToString();

    public string Indent { get; }

    public void IncreaseIndent()
    {
        _currentIndent += Indent;
    }

    public void DecreaseIndent()
    {
        if (_currentIndent.Length == 0)
        {
            throw new InvalidOperationException("Imbalanced unindent.");
        }

        _currentIndent = _currentIndent.Substring(0, _currentIndent.Length - Indent.Length);
    }

    public void Insert(int index, string text)
    {
        _text.Insert(index, text);
    }

    private void AppendLine(string line)
    {
        if (line.Contains('\n'))
        {
            foreach (string singleLine in line.Split('\n'))
            {
                AppendLine(singleLine.TrimEnd());
            }
            ResetExtraIndent();
            return;
        }

        if (line.StartsWith('}'))
        {
            DecreaseIndent();
        }
        else if (line.StartsWith('{') || line.StartsWith(')'))
        {
            ResetExtraIndent();
        }

        if (line.Length > 0)
        {
            line = _currentIndent + line;
        }

        _text.AppendLine(line);

        if (line.EndsWith('{'))
        {
            IncreaseIndent();
        }
        else if (line.EndsWith('(') || line.EndsWith('?') || line.EndsWith("=>"))
        {
            // The "extra" indent persists until the end of the set of lines appended together
            // (before the split) or until a line ending with a semicolon."
            IncreaseExtraIndent();
        }
        else if (line.EndsWith(';'))
        {
            ResetExtraIndent();
        }
    }

    private void IncreaseExtraIndent()
    {
        _extraIndentLevel++;
        IncreaseIndent();
    }

    private void ResetExtraIndent()
    {
        while (_extraIndentLevel > 0)
        {
            DecreaseIndent();
            _extraIndentLevel--;
        }
    }

    public static SourceBuilder operator +(SourceBuilder s, string line)
    {
        s.AppendLine(line);
        return s;
    }

    public static SourceBuilder operator ++(SourceBuilder s)
    {
        s.AppendLine(string.Empty);
        return s;
    }
}
