
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using static NodeApi.Test.TestBuilder;

// Avoid running MSBuild on the same project concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NodeApi.Test;

public class HostedClrTests
{
    private static readonly Dictionary<string, string?> s_builtTestModules = new();
    private static readonly Lazy<string> s_builtHostModule = new(() => BuildHostModule());

    public static IEnumerable<object[]> TestCases { get; } = ListTestCases();

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
                CopyTypeDefinitions(moduleName, moduleFilePath);
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

        string runLogFilePath = GetRunLogFilePath("hosted", moduleName, testCasePath);
        RunNodeTestCase(jsFilePath, runLogFilePath, new Dictionary<string, string>
        {
            [ModulePathEnvironmentVariableName] = moduleFilePath,
            [HostPathEnvironmentVariableName] = hostFilePath,

            // CLR host tracing (very verbose).
            // This will cause the test to always fail because tracing writes to stderr.
            ////["COREHOST_TRACE"] = "1",
        });
    }

    private static string BuildHostModule()
    {
        string projectFilePath = Path.Join(RepoRootDirectory, "Runtime", "NodeApi.Runtime.csproj");

        string logDir = Path.Join(
            RepoRootDirectory, "out", "obj", Configuration);
        Directory.CreateDirectory(logDir);
        string logFilePath = Path.Join(logDir, "publish-host.log");

        string runtimeIdentifier = GetCurrentPlatformRuntimeIdentifier();
        var properties = new Dictionary<string, string>
        {
            ["RuntimeIdentifier"] = runtimeIdentifier,
            ["Configuration"] = Configuration,
        };

        string? buildResult = BuildProject(
          projectFilePath,
          targets: new[] { "Restore", "Publish" },
          properties,
          returnProperty: "PublishDir",
          logFilePath: logFilePath,
          verboseLog: true);

        if (string.IsNullOrEmpty(buildResult))
        {
            Assert.Fail("Host publish failed. Check the log for details: " + logFilePath);
        }

        string publishDir = buildResult.Replace(
            Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string moduleFilePath = Path.Combine(publishDir, "NodeApi.node");
        Assert.True(
            File.Exists(moduleFilePath), "Host module file was not built: " + moduleFilePath);
        return moduleFilePath;
    }

    private static string? BuildTestModuleCSharp(
      string moduleName,
      string logFilePath)
    {
        string projectFilePath = Path.Join(
            TestCasesDirectory, moduleName, moduleName + ".csproj");

        // Auto-generate an empty project file. All project info is inherited from
        // TestCases/Directory.Build.{props,targets}
        File.WriteAllText(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

        string runtimeIdentifier = GetCurrentPlatformRuntimeIdentifier();
        var properties = new Dictionary<string, string>
        {
            ["RuntimeIdentifier"] = runtimeIdentifier,
            ["Configuration"] = Configuration,
        };

        string? buildResult = BuildProject(
          projectFilePath,
          targets: new[] { "Restore", "Build" },
          properties,
          returnProperty: "TargetPath",
          logFilePath: logFilePath,
          verboseLog: false);

        if (string.IsNullOrEmpty(buildResult))
        {
            return null;
        }

        string moduleFilePath = buildResult.Replace(
            Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
        return moduleFilePath;
    }

    private static void BuildTestModuleTypeScript(string _ /*testCaseName*/)
    {
        // TODO: Compile TypeScript code, if the test uses TS.
        // Reference the generated type definitions from the C#?
    }
}
