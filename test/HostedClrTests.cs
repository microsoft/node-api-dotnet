// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;

// Avoid running MSBuild on the same project concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.JavaScript.NodeApi.Test;

public class HostedClrTests
{
    private static readonly Dictionary<string, string?> s_builtTestModules = new();
    private static readonly Lazy<string> s_builtHostModule = new(() => BuildHostModule());

#if NETFRAMEWORK
    // The .NET Framework host does not yet support multiple instances of a module.
    public static IEnumerable<object[]> TestCases { get; } = ListTestCases(
        (testCaseName) => !testCaseName.Contains("/multi_instance"));
#else
    public static IEnumerable<object[]> TestCases { get; } = ListTestCases();
#endif

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string moduleName = id.Substring(0, id.IndexOf('/'));
        string testCaseName = id.Substring(id.IndexOf('/') + 1);
        string testCasePath = testCaseName.Replace('/', Path.DirectorySeparatorChar);

        string hostFilePath = s_builtHostModule.Value;

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

        // Copy the host file to the same directory as the module. Normally nuget + npm
        // packaging should orchestrate getting these files in the right places.
        string hostFilePath2 = Path.Combine(
            Path.GetDirectoryName(moduleFilePath)!, Path.GetFileName(hostFilePath));
        CopyIfNewer(hostFilePath, hostFilePath2);
        if (File.Exists(hostFilePath + ".pdb"))
        {
            CopyIfNewer(hostFilePath + ".pdb", hostFilePath2 + ".pdb");
        }
        CopyIfNewer(
            hostFilePath.Replace(".node", ".runtimeconfig.json"),
            hostFilePath2.Replace(".node", ".runtimeconfig.json"));
        hostFilePath = hostFilePath2;

        // TODO: Support compiling TS files to JS.
        string jsFilePath = Path.Combine(TestCasesDirectory, moduleName, testCasePath + ".js");

        string runLogFilePath = GetRunLogFilePath("hosted", moduleName, testCasePath);
        RunNodeTestCase(jsFilePath, runLogFilePath, new Dictionary<string, string>
        {
            [ModulePathEnvironmentVariableName] = moduleFilePath,
            [HostPathEnvironmentVariableName] = hostFilePath,
            [DotNetVersionEnvironmentVariableName] = GetCurrentFrameworkTarget(),

            // CLR host tracing (very verbose).
            // This will cause the test to always fail because tracing writes to stderr.
            ////["COREHOST_TRACE"] = "1",
        });
    }

    private static string BuildHostModule()
    {
        string projectFilePath = Path.Combine(RepoRootDirectory, "src", "NodeApi", "NodeApi.csproj");

        string logDir = Path.Combine(
            RepoRootDirectory, "out", "obj", Configuration);
        Directory.CreateDirectory(logDir);
        string logFilePath = Path.Combine(logDir, "publish-host.log");

        var properties = new Dictionary<string, string>
        {
            ["TargetFramework"] = "net7.0", // The host is always built with the latest framework.
            ["RuntimeIdentifier"] = GetCurrentPlatformRuntimeIdentifier(),
            ["Configuration"] = Configuration,
        };

        BuildProject(
            projectFilePath,
            "Publish",
            properties,
            logFilePath,
            verboseLog: false);

        string publishDir = Path.Combine(
            RepoRootDirectory,
            "out",
            "bin",
            Configuration,
            "NodeApi",
            "net7.0",
            GetCurrentPlatformRuntimeIdentifier(),
            "publish");
        string moduleFilePath = Path.Combine(publishDir, "Microsoft.JavaScript.NodeApi.node");
        Assert.True(
            File.Exists(moduleFilePath), "Host module file was not built: " + moduleFilePath);
        return moduleFilePath;
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
