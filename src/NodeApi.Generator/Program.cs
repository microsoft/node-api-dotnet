// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Generator;

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
    private static TypeDefinitionsGenerator.ModuleType s_moduleType;
    private static bool s_suppressWarnings;

    public static int Main(string[] args)
    {

#if DEBUG
#pragma warning disable RS1035 // The symbol 'Environment' is banned for use by analyzers.
        if (Environment.GetEnvironmentVariable("DEBUG_NODE_API_GENERATOR") != null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#pragma warning restore RS1035
#endif

        if (!ParseArgs(args))
        {
            Console.WriteLine("Usage: node-api-dotnet-generator [options...]");
            Console.WriteLine();
            Console.WriteLine("  -a --asssembly  Path to input assembly (required)");
            Console.WriteLine("  -r --reference  Path to assembly reference by input " +
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
        for (int i = 0; i < args.Length; i++)
        {
            if (i == args.Length - 1 && args[i] != "--nowarn")
            {
                return false;
            }

            switch (args[i])
            {
                case "-a":
                case "--assembly":
                case "--assemblies":
                    s_assemblyPaths.AddRange(args[++i].Split(PathSeparator));
                    break;

                case "-r":
                case "--reference":
                case "--references":
                    s_referenceAssemblyPaths.AddRange(args[++i].Split(PathSeparator));
                    break;

                case "-t":
                case "--typedef":
                case "--typedefs":
                    s_typeDefinitionsPaths.AddRange(args[++i].Split(PathSeparator));
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

        ResolveSystemAssemblies();

        if (s_assemblyPaths.Any(
                (a) => !a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
            s_referenceAssemblyPaths.Any(
                (r) => !r.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
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

    private static void ResolveSystemAssemblies()
    {
        string systemAssemblyDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        if (systemAssemblyDirectory[systemAssemblyDirectory.Length - 1] ==
            Path.DirectorySeparatorChar)
        {
            systemAssemblyDirectory = systemAssemblyDirectory.Substring(
                0, systemAssemblyDirectory.Length - 1);
        }

        string runtimeVersionDir = Path.GetFileName(systemAssemblyDirectory);
        string dotnetRootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(
            Path.GetDirectoryName(systemAssemblyDirectory)!)!)!;
        string tfm = GetCurrentFrameworkTarget();

        string refAssemblyDirectory = Path.Combine(
            dotnetRootDirectory,
            "packs",
            "Microsoft.NETCore.App.Ref",
            runtimeVersionDir,
            "ref",
            tfm);

        for (int i = 0; i < s_assemblyPaths.Count; i++)
        {
            if (string.IsNullOrEmpty(s_assemblyPaths[i]) &&
                s_typeDefinitionsPaths.Count == s_assemblyPaths.Count)
            {
                // Skip empty items, which might come from concatenating MSBuild item lists.
                s_assemblyPaths.RemoveAt(i);
                s_typeDefinitionsPaths.RemoveAt(i);
                i--;
                continue;
            }

            if (!s_assemblyPaths[i].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string systemAssemblyPath = Path.Combine(
                    refAssemblyDirectory, s_assemblyPaths[i] + ".dll");
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
                if (File.Exists(systemAssemblyPath))
#pragma warning restore RS1035
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
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        Version frameworkVersion = Environment.Version;
#pragma warning restore RS1035
        return frameworkVersion.Major == 4 ? "net472" :
            $"net{frameworkVersion.Major}.{frameworkVersion.Minor}";
    }
}
