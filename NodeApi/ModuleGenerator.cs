using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi;

[Generator]
public class ModuleGenerator : ISourceGenerator
{
	private const string DiagnosticPrefix = "NAPI";
	private const string DiagnosticCategory = "NodeApi";

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
		var moduleType = GetModuleType(context);
		if (moduleType != null)
		{
			var initializerSource = GenerateModuleInitializer(context, moduleType);
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

	private ITypeSymbol? GetModuleType(GeneratorExecutionContext context)
	{
		ITypeSymbol? moduleType = null;

		foreach (var type in context.Compilation.Assembly.TypeNames
			.SelectMany((n) => context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
			.OfType<ITypeSymbol>())
		{
			if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == nameof(ModuleAttribute)))
			{
				if (moduleType != null)
				{
					var title = "Multiple types have Node API module attributes.";
					var descriptor = new DiagnosticDescriptor(
						id: DiagnosticPrefix + "1000",
						title,
						messageFormat: title + " Only a single class can represent the module exports.",
						DiagnosticCategory,
						DiagnosticSeverity.Error,
						isEnabledByDefault: true);
					context.ReportDiagnostic(
						Diagnostic.Create(descriptor, type.Locations.Single()));
					return null;
				}

				moduleType = type;
			}
		}

		return moduleType;
	}

	private SourceText GenerateModuleInitializer(
		GeneratorExecutionContext context,
		ITypeSymbol moduleType)
	{
		var s = new SourceBuilder();

		s += "using System.Collections.Generic;";
		s += "using System.Runtime.InteropServices;";
		s += "using static NodeApi.PropertyAttributes;";

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
		s += "Env.Current = new Env(env);";
		s += "var exports = new Object(value);";
		s++;

		s += "try";
		s += "{";

		ExportModuleMembers(context, s, moduleType);

		s += "}";
		s += "catch (System.Exception ex)";
		s += "{";
		s += "System.Console.Error.WriteLine($\"Failed to export module: {ex}\");";
		s += "}";

		s++;
		s += $"Instance = new {moduleType.Name}();";

		s++;
		s += "return exports;";
		s += "}";

		s += "}";

		return s;
	}

	private void ExportModuleMembers(
		GeneratorExecutionContext context,
		SourceBuilder s,
		ITypeSymbol moduleType)
	{
		// TODO: Also generate .d.ts?

		s += "var moduleProperties = new List<PropertyDescriptor>();";

		foreach (var member in moduleType.GetMembers()
			.Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic))
		{
			if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
			{
				ExportModuleMethod(context, s, method);
			}
			else if (member is IPropertySymbol property)
			{
				if (property.Type.Name == nameof(Type) && property.IsReadOnly)
				{
					ExportModuleClass(context, s, property);
				}
				else
				{
					ExportModuleProperty(context, s, property);
				}
			}
		}

		s += "exports.DefineProperties(moduleProperties.ToArray());";
	}

	private void ExportModuleMethod(
		GeneratorExecutionContext context,
		SourceBuilder s,
		IMethodSymbol method)
	{
		ValidateExportedMethod(context, method);

		s += "moduleProperties.Add(new PropertyDescriptor(";
		s += $"{s.Indent}\"{ToCamelCase(method.Name)}\",";

		if (method.ReturnType.Name == "Void")
		{
			s += $"{s.Indent}method: (_, args) => {{ Instance!.{method.Name}(args); return default; }}));";
		}
		else
		{
			s += $"{s.Indent}method: (_, args) => {{ return Instance!.{method.Name}(args); }}));";
		}
	}

	private void ExportModuleProperty(
		GeneratorExecutionContext context,
		SourceBuilder s,
		IPropertySymbol property)
	{
		ValidateExportedProperty(context, property);

		s += "moduleProperties.Add(new PropertyDescriptor(";
		s += $"{s.Indent}\"{ToCamelCase(property.Name)}\",";

		if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
		{
			s += $"{s.Indent}getter: (_) => Instance!.{property.Name},";
			s += $"{s.Indent}setter: (_, value) => Instance!.{property.Name} = value));";
		}
		else
		{
			s += $"{s.Indent}getter: (_) => Instance!.{property.Name}));";
		}
	}

	private void ExportModuleClass(
		GeneratorExecutionContext context,
		SourceBuilder s,
		IPropertySymbol property)
	{
		// TODO: Allow the typeof() expression to include a namespace.
		var expectedSource = $"typeof({property.Name})";

		var location = property.GetMethod!.Locations.Single();
		var sourceSpan = location.SourceSpan;
		var sourceText = location.SourceTree!.ToString();
		var getterSource = sourceText.Substring(sourceSpan.Start, sourceSpan.Length);
		if (getterSource != expectedSource)
		{
			ReportError(
				context,
				1004,
				"Exported class has unsupported getter code.",
				$"Getter for property {property.Name} must return {expectedSource}.",
				property.Locations.Single());
			return;
		}

		var classType = context.Compilation.GetSymbolsWithName(property.Name, SymbolFilter.Type)
			.Cast<ITypeSymbol>().Single();

		// TODO: Check that the class has a public constructor that takes a Value[] parameter.

		var classPrefix = ToCamelCase(classType.Name);
		ExportClassMembers(context, s, classType, $"{classPrefix}Properties");

		s += $"var {classPrefix}Class = Object.DefineClass(";
		s += $"{s.Indent}\"{classType.Name}\",";
		s += $"{s.Indent}constructor: (args) => new {classType.Name}(args),";
		s += $"{s.Indent}properties: {classPrefix}Properties.ToArray());";

		s += "moduleProperties.Add(new PropertyDescriptor(";
		s += $"{s.Indent}\"{property.Name}\",";
		s += $"{s.Indent}getter: (_) => {classPrefix}Class.Value));";
	}

	private void ExportClassMembers(
		GeneratorExecutionContext context,
		SourceBuilder s,
		ITypeSymbol classType,
		string classPropertiesName)
	{
		// TODO: Also generate .d.ts?

		s += $"var {classPropertiesName} = new List<PropertyDescriptor<{classType.Name}>>();";

		foreach (var member in classType.GetMembers()
			.Where((m) => m.DeclaredAccessibility == Accessibility.Public))
		{
			if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
			{
				ExportClassMethod(context, s, method, classPropertiesName);
			}
			else if (member is IPropertySymbol property)
			{
				ExportClassProperty(context, s, property, classPropertiesName);
			}
		}
	}

	private void ExportClassMethod(
		GeneratorExecutionContext context,
		SourceBuilder s,
		IMethodSymbol method,
		string classPropertiesName)
	{
		ValidateExportedMethod(context, method);

		var className = method.ContainingType.Name;
		s += $"{classPropertiesName}.Add(new PropertyDescriptor<{className}>(";
		s += $"{s.Indent}\"{ToCamelCase(method.Name)}\",";

		if (method.IsStatic)
		{
			if (method.ReturnType.Name == "Void")
			{
				s += $"{s.Indent}method: (_, _, args) => {{ {className}.{method.Name}(args); return default; }},";
			}
			else
			{
				s += $"{s.Indent}method: (_, _, args) => {{ return {className}.{method.Name}(args); }},";
			}

			s += $"{s.Indent}Writable | Configurable | Static));";
		}
		else
		{
			if (method.ReturnType.Name == "Void")
			{
				s += $"{s.Indent}method: (instance, _, args) => {{ instance!.{method.Name}(args); return default; }}));";
			}
			else
			{
				s += $"{s.Indent}method: (instance, _, args) => {{ return instance!.{method.Name}(args); }}));";
			}
		}
	}

	private void ExportClassProperty(
		GeneratorExecutionContext context,
		SourceBuilder s,
		IPropertySymbol property,
		string classPropertiesName)
	{
		ValidateExportedProperty(context, property);

		var className = property.ContainingType.Name;
		s += $"{classPropertiesName}.Add(new PropertyDescriptor<{className}>(";
		s += $"{s.Indent}\"{ToCamelCase(property.Name)}\",";

		if (property.IsStatic)
		{
			if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
			{
				s += $"{s.Indent}getter: (_, _) => {className}.{property.Name},";
				s += $"{s.Indent}setter: (_, _, value) => {className}.{property.Name} = value,";
			}
			else
			{
				s += $"{s.Indent}getter: (_, _) => {className}.{property.Name},";
			}

			s += $"{s.Indent}Enumerable | Writable | Configurable | Static));";
		}
		else
		{
			if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
			{
				s += $"{s.Indent}getter: (instance, _) => instance!.{property.Name},";
				s += $"{s.Indent}setter: (instance, _, value) => instance!.{property.Name} = value));";
			}
			else
			{
				s += $"{s.Indent}getter: (instance, _) => instance!.{property.Name}));";
			}
		}
	}

	private static void ValidateExportedMethod(
		GeneratorExecutionContext context,
		IMethodSymbol method)
	{
		// TODO: Marshal other parameter and return types.
		if (method.Parameters.Length != 1 ||
			!(method.Parameters[0].Type is IArrayTypeSymbol arrayType) ||
			arrayType.ElementType.Name != nameof(Value))
		{
			ReportError(
				context,
				1001,
				"Exported method has unsupported parameters.",
				"Exported methods must have a single parameter of type " +
					$"{typeof(Value).Namespace}.{nameof(Value)}[].",
				method.Locations.Single());
			return;
		}

		if (method.ReturnType.Name != "Void" && method.ReturnType.Name != nameof(Value))
		{
			ReportError(
				context,
				1002,
				"Exported method has unsupported return type.",
				"Exported methods must have return type " +
					$"{typeof(Value).Namespace}.{nameof(Value)} or void.",
				method.Locations.Single());
			return;
		}
	}

	private static void ValidateExportedProperty(
		GeneratorExecutionContext context,
		IPropertySymbol property)
	{
		if (property.Type.Name != nameof(Value))
		{
			ReportError(
				context,
				1003,
				"Exported property has unsupported type.",
				"Exported properties must have type " +
					$"{typeof(Value).Namespace}.{nameof(Value)}.",
				property.Locations.Single());
			return;
		}
	}

	private static string ToCamelCase(string name)
	{
		return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
	}

	private static void ReportError(
		GeneratorExecutionContext context,
		int id,
		string title,
		string description,
		Location location) =>
		ReportDiagnostic(context, DiagnosticSeverity.Error, id, title, description, location);

	private static void ReportDiagnostic(
		GeneratorExecutionContext context,
		DiagnosticSeverity severity,
		int id,
		string title,
		string description,
		Location location)
	{
			var descriptor = new DiagnosticDescriptor(
				id: DiagnosticPrefix + id,
				title,
				messageFormat: title +
					(!string.IsNullOrEmpty(description) ? " " + description : string.Empty),
				DiagnosticCategory,
				DiagnosticSeverity.Error,
				isEnabledByDefault: true);
			context.ReportDiagnostic(
				Diagnostic.Create(descriptor, location));
	}
}
