// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class NativeAotTests
{
    private static readonly Dictionary<string, string?> s_builtTestModules = new();

    public static IEnumerable<object[]> TestCases { get; } = ListTestCases((testCaseName) =>
        !testCaseName.Contains("/dynamic_") && !testCaseName.StartsWith("projects/"));

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string moduleName = id.Substring(0, id.IndexOf('/'));
        string testCaseName = id.Substring(id.IndexOf('/') + 1);
        string testCasePath = testCaseName.Replace('/', Path.DirectorySeparatorChar);

        string buildLogFilePath = GetBuildLogFilePath("aot", moduleName);
        if (!s_builtTestModules.TryGetValue(moduleName, out string? moduleFilePath))
        {
            try
            {
                moduleFilePath = BuildTestModuleCSharp(moduleName, buildLogFilePath);
            }
            finally
            {
                // Save the built module path for the other tests that use the same module.
                // Or if the build failed, save null so the next test won't try to build again.
                s_builtTestModules.Add(moduleName, moduleFilePath);
            }

            if (moduleFilePath != null)
            {
                BuildTestModuleTypeScript(moduleName);
            }
        }

        if (moduleFilePath == null)
        {
            Assert.Fail("Build failed. Check the log for details: " + buildLogFilePath);
        }

        // TODO: Support compiling TS files to JS.
        string jsFilePath = Path.Join(TestCasesDirectory, moduleName, testCasePath + ".js");

        string runLogFilePath = GetRunLogFilePath("aot", moduleName, testCasePath);
        RunNodeTestCase(jsFilePath, runLogFilePath, new Dictionary<string, string>
        {
            [ModulePathEnvironmentVariableName] = moduleFilePath,
        });
    }

    private static string? BuildTestModuleCSharp(
      string moduleName,
      string logFilePath)
    {
        string projectFilePath = CreateProjectFile(moduleName);

        var properties = new Dictionary<string, string>
        {
            ["TargetFramework"] = GetCurrentFrameworkTarget(),
            ["RuntimeIdentifier"] = GetCurrentPlatformRuntimeIdentifier(),
            ["Configuration"] = Configuration,
            ["DefineConstants"] = "AOT",
        };

        BuildProject(
            projectFilePath,
            "Publish",
            properties,
            logFilePath,
            verboseLog: false);

        string moduleFilePath = Path.Combine(
            RepoRootDirectory,
            "out",
            "bin",
            Configuration,
            "TestCases",
            moduleName,
            GetCurrentFrameworkTarget(),
            GetCurrentPlatformRuntimeIdentifier(),
            "native",
            moduleName + ".node");
        moduleFilePath = Path.ChangeExtension(moduleFilePath, ".node");
        Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
        return moduleFilePath;
    }

    private static void BuildTestModuleTypeScript(string _ /*testCaseName*/)
    {
        // TODO: Compile TypeScript code, if the test uses TS.
        // Reference the generated type definitions from the C#?
    }
}
