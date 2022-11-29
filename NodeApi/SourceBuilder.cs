using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace NodeApi;

internal class SourceBuilder : SourceText
{
	private readonly StringBuilder s;
	private string currentIndent = string.Empty;

	public SourceBuilder(string indent = "\t")
	{
		this.s = new StringBuilder();
		Indent = indent;
	}

	public override Encoding? Encoding => Encoding.UTF8;

	public override int Length => this.s.Length;

	public override char this[int position] => this.s[position];

	public override void CopyTo(
		int sourceIndex, char[] destination, int destinationIndex, int count)
	{
		this.s.CopyTo(sourceIndex, destination, destinationIndex, count);
	}

	public override string ToString() => this.s.ToString();

	public string Indent { get; }

	public void IncreaseIndent()
	{
		this.currentIndent += Indent;
	}

	public void DecreaseIndent()
	{
		if (this.currentIndent.Length == 0)
		{
			throw new InvalidOperationException("Imbalanced unindent.");
		}

		this.currentIndent = this.currentIndent.Substring(0, this.currentIndent.Length - Indent.Length);
	}

	private void AppendLine(string line)
	{
		if (line.StartsWith("}"))
		{
			DecreaseIndent();
		}

		if (line.Length > 0)
		{
			line = currentIndent + line;
		}

		this.s.AppendLine(line);

		if (line.EndsWith("{"))
		{
			IncreaseIndent();
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
