using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NodeApi;

[Generator]
public class ModuleGenerator : ISourceGenerator
{
	private const string ModuleInitializerClassName = "Module";
	private const string ModuleInitializeMethodName = "Initialize";
	private const string ModuleRegisterFunctionName = "napi_register_module_v1";

	public void Initialize(GeneratorInitializationContext context)
	{
#if DEBUG
		// Note source generators re not covered by normal debugging,
		// because the generator runs at build time, not at application run-time.
		// Un-comment the line below to enable debugging at build time.

		////System.Diagnostics.Debugger.Launch();
#endif
	}

	public void Execute(GeneratorExecutionContext context)
	{
		var compilation = context.Compilation;
		var moduleType = GetModuleType(compilation);
		if (moduleType != null)
		{
			var initializerSource = GenerateModuleInitializer(moduleType);
			context.AddSource($"{nameof(NodeApi)}.{ModuleInitializerClassName}", initializerSource);

			// Also write the generated code to a file under obj/ for diagnostics.
			// Depends on <CompilerVisibleProperty Include="BaseIntermediateOutputPath" />
			if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
				"build_property.BaseIntermediateOutputPath", out var intermediateOutputPath))
			{
				var generatedSourcePath = Path.Combine(
					intermediateOutputPath,
					$"{nameof(NodeApi)}.{ModuleInitializerClassName}.cs");
				File.WriteAllText(generatedSourcePath, initializerSource.ToString());
			}
		}
	}

	private ITypeSymbol? GetModuleType(Compilation compilation)
	{
		ITypeSymbol? moduleType = null;

		foreach (var type in compilation.Assembly.TypeNames
			.SelectMany((n) => compilation.GetSymbolsWithName(n, SymbolFilter.Type))
			.OfType<ITypeSymbol>())
		{
			if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == nameof(ModuleAttribute)))
			{
				if (moduleType != null)
				{
					throw new InvalidOperationException(
						"Multiple types have Node API module attributes.");
				}

				moduleType = type;
			}
		}

		return moduleType;
	}

	private SourceText GenerateModuleInitializer(ITypeSymbol moduleType)
	{
		var s = new SourceBuilder();

		s += "using System;";
		s += "using System.Collections.Generic;";
		s += "using System.Runtime.InteropServices;";

		if (moduleType.ContainingNamespace != null)
		{
			s += $"using {moduleType.ContainingNamespace};";
		}

		s++;
		s += "namespace NodeApi.Generated;";
		s++;
		s += $"public static class {ModuleInitializerClassName}";
		s += "{";

		s += $"public static {moduleType.Name}? Instance {{ get; private set; }}";
		s++;

		s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
		s += $"public static nint {ModuleInitializeMethodName}(nint env, nint value)";
		s += "{";
		s += "var exports = new NodeApi.Object(env, value);";
		s++;

		ExportModuleMembers(s, moduleType);

		s++;
		s += $"Instance = new {moduleType.Name}();";

		s++;
		s += "return exports;";
		s += "}";

		s += "}";

		return s;
	}

	private void ExportModuleMembers(SourceBuilder s, ITypeSymbol moduleType)
	{
		// TODO: Also generate .d.ts?

		s += "var properties = new List<NodeApi.Object.PropertyDescriptor>();";

		foreach (var member in moduleType.GetMembers()
			.Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic))
		{
			if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
			{
				ExportModuleMethod(s, method);
			}
			else if (member is IPropertySymbol property)
			{
				ExportModuleProperty(s, property);
			}
		}

		s += "exports.DefineProperties(properties.ToArray());";
	}

	private void ExportModuleMethod(SourceBuilder s, IMethodSymbol method)
	{
		s += "properties.Add(new NodeApi.Object.PropertyDescriptor";
		s += "{";
		s += $"Name = \"{ToCamelCase(method.Name)}\",";
		s += $"Method = (_) => {{ Instance!.{method.Name}(); return default; }},"; // TODO: Handle parameters
		s += "Attributes = PropertyAttributes.Writable | ";
		s += s.Indent + "PropertyAttributes.Configurable,";
		s += "});";
	}

	private void ExportModuleProperty(SourceBuilder s, IPropertySymbol property)
	{
		s += "properties.Add(new NodeApi.Object.PropertyDescriptor";
		s += "{";
		s += $"Name = \"{ToCamelCase(property.Name)}\",";

		if (property.GetMethod?.DeclaredAccessibility == Accessibility.Public)
		{
			s += $"Getter = (_) => {{ return default; }},"; // TODO: Hook up getter
		}

		if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
		{
			s += $"Setter = (_) => {{ return default; }},"; // TODO: Hook up setter
		}

		s += "Attributes = PropertyAttributes.Writable | ";
		s += s.Indent + "PropertyAttributes.Enumerable | PropertyAttributes.Configurable,";
		s += "});";
	}

	private static string ToCamelCase(string name)
	{
		return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
	}
}
