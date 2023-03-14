// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Generator;

/// <summary>
/// Command-line interface for the Node API TS type-definitions generator tool.
/// </summary>
/// <remarks>
/// This assembly is used as both a library for C# source generation and an executable for TS
/// type-definitions generation.
/// </remarks>
internal static class Program
{
    public static int Main(string[] args)
    {
        // TODO: Implement command-line parsing with named arguments and options.

        if (args.Length != 2 ||
            !args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            !args[1].EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                $"Usage: NodeApi.Generator.exe InputAssembly.dll output-definitions.d.ts");
            return 1;
        }

        string assemblyFilePath = args[0];
        string typeDefinitionsFilePath = args[1];

        TypeDefinitionsGenerator.GenerateTypeDefinitions(assemblyFilePath, typeDefinitionsFilePath);

        return 0;
    }
}
