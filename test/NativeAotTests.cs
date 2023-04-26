// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET7_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;

namespace Microsoft.JavaScript.NodeApi.Test;

using static TestUtils;

public class NativeAotTests
{
    private static readonly Dictionary<string, string?> s_builtTestModules = new();

    public static IEnumerable<object[]> TestCases { get; } = ListTestCases(
        (testCaseName) => !testCaseName.Contains("/dynamic_"));

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string moduleName = id.Substring(0, id.IndexOf('/'));
        string testCaseName = id.Substring(id.IndexOf('/') + 1);
        string testCasePath = testCaseName.Replace('/', Path.DirectorySeparatorChar);

        string buildLogFilePath = GetBuildLogFilePath(moduleName);
        if (!s_builtTestModules.TryGetValue(moduleName, out string? moduleFilePath))
        {
            moduleFilePath = BuildTestModuleCSharp(moduleName, buildLogFilePath);

            if (moduleFilePath != null)
            {
                BuildTestModuleTypeScript(moduleName);
            }

            s_builtTestModules.Add(moduleName, moduleFilePath);
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

#endif // NET7_0_OR_GREATER
