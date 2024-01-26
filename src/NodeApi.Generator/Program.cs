// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.JavaScript.NodeApi.Generator;

// This warning is safe to suppress because this code is not part of the analyzer,
// though it is part of the same assembly.
#pragma warning disable RS1035 // Do not use file I/O APIs banned for analyzers

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
    private static readonly List<string> s_referenceAssemblyDirectories = new();
    private static readonly List<string> s_referenceAssemblyPaths = new();
    private static readonly List<string> s_typeDefinitionsPaths = new();
    private static readonly HashSet<int> s_systemAssemblyIndexes = new();
    private static TypeDefinitionsGenerator.ModuleType s_moduleType;
    private static bool s_suppressWarnings;

    public static int Main(string[] args)
    {
        DebugHelper.AttachDebugger("NODE_API_DEBUG_GENERATOR");

        if (!ParseArgs(args))
        {
            Console.WriteLine("""
                Usage: node-api-dotnet-generator [options...]
                  -a --asssembly  Path to input assembly (required)
                  -f --framework  Target framework of system assemblies (optional)
                  -p --pack       Targeting pack (optional, multiple)
                  -r --reference  Path to reference assembly (optional, multiple)
                  -t --typedefs   Path to output type definitions file (required)
                  -m --module     Generate JS loader module(s) alongside typedefs (optional)
                                  Valid values are 'commonjs' or 'esm'
                  --nowarn        Suppress warnings
                  -? -h --help    Show this help message
                  @<file>         Read response file for more options
                """);
            return 1;
        }

        for (int i = 0; i < s_assemblyPaths.Count; i++)
        {
            if (Path.GetFileName(s_assemblyPaths[i]).StartsWith(
                typeof(JSValue).Namespace + ".", StringComparison.OrdinalIgnoreCase))
            {
                // Never generate type definitions for node-api-dotnet interop assemblies.
                continue;
            }

            if (s_assemblyPaths.Take(i).Any(
                (a) => string.Equals(a, s_assemblyPaths[i], StringComparison.OrdinalIgnoreCase)))
            {
                // Skip duplicate references.
                continue;
            }

            // Reference other supplied assemblies, but not the current one.
            List<string> allReferencePaths = s_referenceAssemblyPaths
                .Concat(s_assemblyPaths.Where((_, j) => j != i)).ToList();

            Console.WriteLine($"{s_assemblyPaths[i]} -> {s_typeDefinitionsPaths[i]}");

            TypeDefinitionsGenerator.GenerateTypeDefinitions(
                s_assemblyPaths[i],
                allReferencePaths,
                s_referenceAssemblyDirectories,
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
        if (!MergeArgsFromResponseFiles(ref args))
        {
            return false;
        }

        string? targetFramework = null;
        List<string> targetingPacks = new();

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

                case "-p":
                case "--pack":
                case "--packs":
                    AddItems(targetingPacks, args[++i]);
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

                case "-?":
                case "-h":
                case "--help":
                    return false;

                default:
                    Console.Error.WriteLine("Unrecognized argument: " + args[i]);
                    return false;
            }
        }

        ResolveSystemAssemblies(targetFramework, targetingPacks);

        bool HasAssemblyExtension(string fileName) =>
            fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        string? invalidAssemblyPath = s_assemblyPaths.Concat(s_referenceAssemblyPaths)
            .FirstOrDefault((a) => !HasAssemblyExtension(a));
        if (invalidAssemblyPath != null)
        {
            Console.WriteLine(
                "Incorrect assembly file extension: " + Path.GetFileName(invalidAssemblyPath));
            return false;
        }

        string? invalidTypedefPath = s_typeDefinitionsPaths.FirstOrDefault(
            (t) => !t.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase));
        if (invalidTypedefPath != null)
        {
            Console.WriteLine(
                "Incorrect typedef file extension: " + Path.GetFileName(invalidTypedefPath));
            return false;
        }

        if (s_assemblyPaths.Count == 0)
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

    /// <summary>
    /// Reads a response file indicated by an `@` prefix, in the same format as csc:
    /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/miscellaneous#responsefiles
    /// </summary>
    private static bool MergeArgsFromResponseFiles(ref string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith('-') && args[i] != "--nowarn")
            {
                // Skip over argument values.
                i++;
                continue;
            }

            if (args[i].StartsWith('@'))
            {
                string responseFilePath = args[i].Substring(1);
                if (!File.Exists(responseFilePath))
                {
                    Console.Error.WriteLine("Response file not found: " + responseFilePath);
                    return false;
                }

                // Read response file lines, ignoring blank lines and comments.
                string[] responseFileLines = File.ReadAllLines(responseFilePath)
                    .Select((line) => line.Trim())
                    .Where((line) => !string.IsNullOrEmpty(line) && !line.StartsWith('#'))
                    .ToArray();

                // Split lines into arguments, handling quotes.
                string[] responseFileArgs = responseFileLines.SelectMany(SplitWithQuotes).ToArray();

                // Insert response file args into the args array.
                args = args.Take(i)
                    .Concat(responseFileArgs)
                    .Concat(args.Skip(i + 1)).ToArray();
                i += responseFileArgs.Length;
            }
        }

        return true;
    }

    private static IEnumerable<string> SplitWithQuotes(string line)
    {
        StringBuilder s = new();
        bool inQuotes = false;
        bool foundQuotes = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                foundQuotes = true;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (s.Length > 0 || foundQuotes)
                {
                    yield return s.ToString();
                    foundQuotes = false;
                    s.Clear();
                }
            }
            else
            {
                s.Append(c);
            }
        }

        if (s.Length > 0 || foundQuotes)
        {
            yield return s.ToString();
        }
    }

    private static void ResolveSystemAssemblies(
        string? targetFramework,
        List<string> targetingPacks)
    {
        if (targetFramework == null)
        {
            targetFramework = GetCurrentFrameworkTarget();
        }
        else if (targetFramework.Contains('-'))
        {
            // Strip off a platform suffix from a target framework like "net6.0-windows".
            targetFramework = targetFramework.Substring(0, targetFramework.IndexOf('-'));
        }

        if (targetFramework.StartsWith("net4"))
        {
            if (targetingPacks.Count > 0)
            {
                Console.WriteLine("Ignoring target packs for .NET Framework target");
            }

            string refAssemblyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Reference Assemblies",
                "Microsoft",
                "Framework",
                ".NETFramework",
                "v" + string.Join(".", targetFramework.Substring(3).ToArray())); // v4.7.2
            if (Directory.Exists(refAssemblyDirectory))
            {
                s_referenceAssemblyDirectories.Add(refAssemblyDirectory);
            }
        }
        else
        {
            if (targetingPacks.Count == 0)
            {
                // If no targeting packs were specified, use the default targeting pack for .NET.
                targetingPacks.Add("Microsoft.NETCore.App");
            }

            string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            if (runtimeDirectory[runtimeDirectory.Length - 1] == Path.DirectorySeparatorChar)
            {
                runtimeDirectory = runtimeDirectory.Substring(
                    0, runtimeDirectory.Length - 1);
            }
            string dotnetRootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(
                Path.GetDirectoryName(runtimeDirectory)!)!)!;

            foreach (string targetPack in targetingPacks)
            {
                string targetPackDirectory = Path.Combine(
                    dotnetRootDirectory,
                    "packs",
                    targetPack + ".Ref");
                if (Directory.Exists(targetPackDirectory))
                {
                    string? refAssemblyDirectory = Directory.GetDirectories(targetPackDirectory)
                        .OrderByDescending((d) => Path.GetFileName(d))
                        .Select((d) => Path.Combine(d, "ref", targetFramework))
                        .FirstOrDefault(Directory.Exists);
                    if (refAssemblyDirectory != null)
                    {
                        s_referenceAssemblyDirectories.Add(refAssemblyDirectory);
                    }
                }
            }

            // Reverse the order of reference assembly directories so that reference assemblies
            // specified later in the list are searched first (they override earlier directories).
            s_referenceAssemblyDirectories.Reverse();
        }

        for (int i = 0; i < s_assemblyPaths.Count; i++)
        {
            if (!s_assemblyPaths[i].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string? systemAssemblyPath = null;
                foreach (string referenceAssemblyDirectory in s_referenceAssemblyDirectories)
                {
                    string potentialSystemAssemblyPath = Path.Combine(
                        referenceAssemblyDirectory, s_assemblyPaths[i] + ".dll");
                    if (File.Exists(potentialSystemAssemblyPath))
                    {
                        systemAssemblyPath = potentialSystemAssemblyPath;
                        break;
                    }
                }

                if (systemAssemblyPath != null)
                {
                    s_assemblyPaths[i] = systemAssemblyPath;
                    s_systemAssemblyIndexes.Add(i);
                }
                else
                {
                    Console.WriteLine(
                        $"Assembly '{s_assemblyPaths[i]}' was not found in " +
                        "reference assembly directories:");
                    foreach (string referenceAssemblyDirectory in s_referenceAssemblyDirectories)
                    {
                        Console.WriteLine("    " + referenceAssemblyDirectory);
                    }
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
