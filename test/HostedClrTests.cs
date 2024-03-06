// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

// Avoid running MSBuild on the same project concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.JavaScript.NodeApi.Test;

public class HostedClrTests
{
    private static readonly Dictionary<string, string?> s_builtTestModules = new();

#if NETFRAMEWORK
    // The .NET Framework host does not yet support multiple instances of a module.
    public static IEnumerable<object[]> TestCases { get; } = ListTestCases((testCaseName) =>
        !testCaseName.StartsWith("projects/") && !testCaseName.Contains("/multi_instance"));
#else
    public static IEnumerable<object[]> TestCases { get; } = ListTestCases((testCaseName) =>
        !testCaseName.StartsWith("projects/"));
#endif

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string moduleName = id.Substring(0, id.IndexOf('/'));
        string testCaseName = id.Substring(id.IndexOf('/') + 1);
        string testCasePath = testCaseName.Replace('/', Path.DirectorySeparatorChar);
        string buildLogFilePath = GetBuildLogFilePath("hosted", moduleName);

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
        string jsFilePath = Path.Combine(TestCasesDirectory, moduleName, testCasePath + ".js");

        string runLogFilePath = GetRunLogFilePath("hosted", moduleName, testCasePath);
        RunNodeTestCase(jsFilePath, runLogFilePath, new Dictionary<string, string>
        {
            [ModulePathEnvironmentVariableName] = moduleFilePath,
            [DotNetVersionEnvironmentVariableName] = GetCurrentFrameworkTarget(),

            // CLR host tracing (very verbose).
            // This will cause the test to always fail because tracing writes to stderr.
            ////["COREHOST_TRACE"] = "1",
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
        };

        BuildProject(
            projectFilePath,
            "Build",
            properties,
            logFilePath: logFilePath,
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
            moduleName + ".dll");
        Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
        return moduleFilePath;
    }

    private static void BuildTestModuleTypeScript(string _ /*testCaseName*/)
    {
        // TODO: Compile TypeScript code, if the test uses TS.
        // Reference the generated type definitions from the C#?
    }
}
