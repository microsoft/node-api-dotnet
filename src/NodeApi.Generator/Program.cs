// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Generator;

// This warning is safe to suppress because this code is not part of the analyzer,
// though it is part of the same assembly.
#pragma warning disable RS1035 // Do not use APIs banned for analyzers

/// <summary>
/// Command-line interface for the Node API TS type-definitions generator tool.
/// </summary>
/// <remarks>
/// This assembly is used as both a library for C# source generation and an executable for TS
/// type-definitions generation.
/// </remarks>
public static class Program
{
    private const char PathSeparator = ';';

    private static readonly List<string> s_assemblyPaths = new();
    private static readonly List<string> s_referenceAssemblyPaths = new();
    private static readonly List<string> s_typeDefinitionsPaths = new();
    private static readonly HashSet<int> s_systemAssemblyIndexes = new();
    private static string? s_systemAssemblyDirectory;
    private static TypeDefinitionsGenerator.ModuleType s_moduleType;
    private static bool s_suppressWarnings;

    public static int Main(string[] args)
    {

#if DEBUG
        if (Environment.GetEnvironmentVariable("DEBUG_NODE_API_GENERATOR") != null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        if (!ParseArgs(args))
        {
            Console.WriteLine("Usage: node-api-dotnet-generator [options...]");
            Console.WriteLine();
            Console.WriteLine("  -a --asssembly  Path to input assembly (required)");
            Console.WriteLine("  -f --framework  Target framework of system assemblies " +
                "(optional)");
            Console.WriteLine("  -r --reference  Path to reference assembly " +
                "(optional, multiple)");
            Console.WriteLine("  -t --typedefs   Path to output type definitions file (required)");
            Console.WriteLine("  -m --module     Generate JS loader module(s) alongside typedefs" +
                "(optional)");
            Console.WriteLine("                  Valid values are 'commonjs' or 'esm'");
            Console.WriteLine("  --nowarn        Suppress warnings");
            return 1;
        }

        for (int i = 0; i < s_assemblyPaths.Count; i++)
        {
            // Reference other supplied assemblies, but not the current one.
            List<string> allReferencePaths = s_referenceAssemblyPaths
                .Concat(s_assemblyPaths.Where((_, j) => j != i)).ToList();

            Console.WriteLine($"{s_assemblyPaths[i]} -> {s_typeDefinitionsPaths[i]}");

            TypeDefinitionsGenerator.GenerateTypeDefinitions(
                s_assemblyPaths[i],
                allReferencePaths,
                s_typeDefinitionsPaths[i],
                s_moduleType,
                isSystemAssembly: s_systemAssemblyIndexes.Contains(i),
                s_systemAssemblyDirectory,
                s_suppressWarnings);

            if (s_moduleType != TypeDefinitionsGenerator.ModuleType.None)
            {
                string pathWithoutExtension = s_typeDefinitionsPaths[i].Substring(
                    0, s_typeDefinitionsPaths[i].Length - 5);
                string loaderModulePath = pathWithoutExtension + ".js";
                Console.WriteLine($"{s_assemblyPaths[i]} -> {loaderModulePath}");
            }
        }

        return 0;
    }

    private static bool ParseArgs(string[] args)
    {
        string? targetFramework = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (i == args.Length - 1 && args[i] != "--nowarn")
            {
                return false;
            }

            void AddItems(List<string> list, string items)
            {
                if (!string.IsNullOrEmpty(items))
                {
                    // Ignore empty items, which might come from concatenating MSBuild item lists.
                    list.AddRange(items.Split(
                        new[] { PathSeparator }, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            switch (args[i])
            {
                case "-a":
                case "--assembly":
                case "--assemblies":
                    AddItems(s_assemblyPaths, args[++i]);
                    break;

                case "-f":
                case "--framework":
                    targetFramework = args[++i];
                    break;

                case "-r":
                case "--reference":
                case "--references":
                    AddItems(s_referenceAssemblyPaths, args[++i]);
                    break;

                case "-t":
                case "--typedef":
                case "--typedefs":
                    AddItems(s_typeDefinitionsPaths, args[++i]);
                    break;

                case "-m":
                case "--module":
                case "--modules":
                    string moduleType = args[++i].ToLowerInvariant();
                    switch (moduleType)
                    {
                        case "es":
                        case "esm":
                        case "mjs":
                            s_moduleType = TypeDefinitionsGenerator.ModuleType.ES;
                            break;
                        case "commonjs":
                        case "cjs":
                            s_moduleType = TypeDefinitionsGenerator.ModuleType.CommonJS;
                            break;
                        default: return false;
                    }
                    break;

                case "--nowarn":
                    s_suppressWarnings = true;
                    break;

                default:
                    Console.Error.WriteLine("Unrecognized argument: " + args[i]);
                    return false;
            }
        }

        ResolveSystemAssemblies(targetFramework);

        bool HasAssemblyExtension(string fileName) =>
            fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        if (s_assemblyPaths.Any((a) => !HasAssemblyExtension(a)) ||
            s_referenceAssemblyPaths.Any((r) => !HasAssemblyExtension(r)) ||
            s_typeDefinitionsPaths.Any(
                (t) => !t.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Incorrect file path or extension.");
            return false;
        }
        else if (s_assemblyPaths.Count == 0)
        {
            Console.WriteLine("Specify an assembly file path.");
            return false;
        }
        else if (s_typeDefinitionsPaths.Count == 0)
        {
            Console.WriteLine("Specify a type definitions file path.");
            return false;
        }
        else if (s_typeDefinitionsPaths.Count != s_assemblyPaths.Count)
        {
            Console.WriteLine("Specify a type definitions file path for every assembly.");
            return false;
        }

        return true;
    }

    private static void ResolveSystemAssemblies(string? targetFramework)
    {
        targetFramework ??= GetCurrentFrameworkTarget();

        if (targetFramework.StartsWith("net4"))
        {
            string refAssemblyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Reference Assemblies",
                "Microsoft",
                "Framework",
                ".NETFramework",
                "v" + string.Join(".", targetFramework.Substring(3).ToArray())); // v4.7.2
            if (Directory.Exists(refAssemblyDirectory))
            {
                s_systemAssemblyDirectory = refAssemblyDirectory;
            }
        }
        else
        {
            string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            if (runtimeDirectory[runtimeDirectory.Length - 1] == Path.DirectorySeparatorChar)
            {
                runtimeDirectory = runtimeDirectory.Substring(
                    0, runtimeDirectory.Length - 1);
            }
            string dotnetRootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(
                Path.GetDirectoryName(runtimeDirectory)!)!)!;

            string refAssemblyDirectory = Path.Combine(
                dotnetRootDirectory,
                "packs",
                "Microsoft.NETCore.App.Ref");
            s_systemAssemblyDirectory = Directory.GetDirectories(refAssemblyDirectory)
                .OrderByDescending((d) => Path.GetFileName(d))
                .Select((d) => Path.Combine(d, "ref", targetFramework))
                .FirstOrDefault(Directory.Exists);
        }

        if (s_systemAssemblyDirectory == null)
        {
            // TODO: Check .NET Framework.
            return;
        }

        for (int i = 0; i < s_assemblyPaths.Count; i++)
        {
            if (!s_assemblyPaths[i].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string systemAssemblyPath = Path.Combine(
                    s_systemAssemblyDirectory, s_assemblyPaths[i] + ".dll");
                if (File.Exists(systemAssemblyPath))
                {
                    s_assemblyPaths[i] = systemAssemblyPath;
                    s_systemAssemblyIndexes.Add(i);
                }
                else
                {
                    Console.WriteLine("System assembly not found at " + systemAssemblyPath);
                }
            }
        }
    }

    public static string GetCurrentFrameworkTarget()
    {
        Version frameworkVersion = Environment.Version;
        return frameworkVersion.Major == 4 ? "net472" :
            $"net{frameworkVersion.Major}.{frameworkVersion.Minor}";
    }
}

#pragma warning restore RS1035
