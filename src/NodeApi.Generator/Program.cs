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
            out string assemblyPath,
            out IList<string> referenceAssemblyPaths,
            out string typeDefinitionsPath))
        {
            Console.WriteLine("Usage: node-api-dotnet-generator [options...]");
            Console.WriteLine();
            Console.WriteLine("  -a --asssembly   Path to input assembly (required)");
            Console.WriteLine("  -r --reference   Path to assembly reference by input " +
                "(optional, multiple)");
            Console.WriteLine("  -t --typedefs    Path to output type definitions file (required)");
            return 1;
        }

        TypeDefinitionsGenerator.GenerateTypeDefinitions(
            assemblyPath, referenceAssemblyPaths, typeDefinitionsPath);

        return 0;
    }

    private static bool ParseArgs(
        string[] args,
        out string assemblyPath,
        out IList<string> referenceAssemblyPaths,
        out string typeDefinitionsPath)
    {
        assemblyPath = string.Empty;
        referenceAssemblyPaths = new List<string>();
        typeDefinitionsPath = string.Empty;

        for (int i = 0; i < args.Length; i++)
        {
            if (i == args.Length - 1)
            {
                return false;
            }

            switch (args[i])
            {
                case "-a": case "--assembly":
                    assemblyPath = args[++i];
                    break;

                case "-r":
                case "--reference":
                    referenceAssemblyPaths.Add(args[++i]);
                    break;

                case "-t":
                case "--typedefs":
                    typeDefinitionsPath = args[++i];
                    break;

                default:
                    Console.Error.WriteLine("Unrecognized argument: " + args[i]);
                    return false;
            }
        }

        if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            referenceAssemblyPaths.Any(
                (r) => !r.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
            !typeDefinitionsPath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
