// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

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

        if (!ParseArgs(
            args,
            out List<string> assemblyPaths,
            out List<string> referenceAssemblyPaths,
            out List<string> typeDefinitionsPaths,
            out bool suppressWarnings))
        {
            Console.WriteLine("Usage: node-api-dotnet-generator [options...]");
            Console.WriteLine();
            Console.WriteLine("  -a --asssembly   Path to input assembly (required)");
            Console.WriteLine("  -r --reference   Path to assembly reference by input " +
                "(optional, multiple)");
            Console.WriteLine("  -t --typedefs    Path to output type definitions file (required)");
            Console.WriteLine("  --nowarn         Suppress warnings");
            return 1;
        }

        for (int i = 0; i < assemblyPaths.Count; i++)
        {
            // Reference other supplied assemblies, but not the current one.
            List<string> allReferencePaths = referenceAssemblyPaths
                .Concat(assemblyPaths.Where((_, j) => j != i)).ToList();

            Console.WriteLine($"{assemblyPaths[i]} -> {typeDefinitionsPaths[i]}");

            TypeDefinitionsGenerator.GenerateTypeDefinitions(
                assemblyPaths[i], allReferencePaths, typeDefinitionsPaths[i], suppressWarnings);
        }

        return 0;
    }

    private static bool ParseArgs(
        string[] args,
        out List<string> assemblyPaths,
        out List<string> referenceAssemblyPaths,
        out List<string> typeDefinitionsPaths,
        out bool suppressWarnings)
    {
        assemblyPaths = new List<string>();
        referenceAssemblyPaths = new List<string>();
        typeDefinitionsPaths = new List<string>();
        suppressWarnings = false;

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
                    assemblyPaths.AddRange(args[++i].Split(PathSeparator));
                    break;

                case "-r":
                case "--reference":
                case "--references":
                    referenceAssemblyPaths.AddRange(args[++i].Split(PathSeparator));
                    break;

                case "-t":
                case "--typedef":
                case "--typedefs":
                    typeDefinitionsPaths.AddRange(args[++i].Split(PathSeparator));
                    break;

                case "--nowarn":
                    suppressWarnings = true;
                    break;

                default:
                    Console.Error.WriteLine("Unrecognized argument: " + args[i]);
                    return false;
            }
        }

        if (assemblyPaths.Any(
                (a) => !a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
            referenceAssemblyPaths.Any(
                (r) => !r.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
            typeDefinitionsPaths.Any(
                (t) => !t.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Incorrect file extension.");
            return false;
        }
        else if (assemblyPaths.Count == 0)
        {
            Console.WriteLine("Specify an assembly file path.");
            return false;
        }
        else if (typeDefinitionsPaths.Count == 0)
        {
            Console.WriteLine("Specify a type definitions file path.");
            return false;
        }
        else if (typeDefinitionsPaths.Count != assemblyPaths.Count)
        {
            Console.WriteLine("Specify a type definitions file path for every assembly.");
            return false;
        }

        return true;
    }
}
