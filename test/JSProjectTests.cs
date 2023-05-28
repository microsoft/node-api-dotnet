// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Test project JS references the System.Console assembly, which doesn't exist in .NET Framework 4.
#if !NETFRAMEWORK

#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class JSProjectTests
{
    public static IEnumerable<object[]> TestCases { get; } = ListTestCases(
        (testCaseName) => testCaseName.StartsWith("projects/"));

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string projectName = id.Split('/')[1];
        string moduleName = id.Split('/')[2];

        CleanTestProject(projectName);

        string buildLogFilePath = GetBuildLogFilePath(projectName, "projects");
        BuildTestProjectReferences(projectName, buildLogFilePath);

        string compileLogFilePath = GetBuildLogFilePath(projectName + "-ts", "projects");
        BuildTestProjectTypeScript(projectName, compileLogFilePath);

        string jsFilePath = Path.Combine(ProjectDir(projectName), moduleName + ".js");
        if (!File.Exists(jsFilePath))
        {
            jsFilePath = Path.Combine(ProjectDir(projectName), "out", moduleName + ".js");
        }

        string runLogFilePath = GetRunLogFilePath(projectName, "projects", moduleName);

        RunNodeTestCase(jsFilePath, runLogFilePath, new Dictionary<string, string>());
    }

    private static string ProjectDir(string projectName)
        => Path.Combine(TestCasesDirectory, "projects", projectName);

    private static void CleanTestProject(string projectName)
    {
        // Clean files produced by the dotnet build.
        string projectBinDir = Path.Combine(ProjectDir(projectName), "bin");
        if (Directory.Exists(projectBinDir)) Directory.Delete(projectBinDir, recursive: true);

        // Clean files produced by the TS compile.
        string projectOutDir = Path.Combine(ProjectDir(projectName), "out");
        if (Directory.Exists(projectOutDir)) Directory.Delete(projectOutDir, recursive: true);
    }

    private static void BuildTestProjectReferences(string projectName, string logFilePath)
    {
        var properties = new Dictionary<string, string>
        {
            ["TargetFramework"] = GetCurrentFrameworkTarget(),
        };

        string projectFilePath = Path.Combine(
            TestCasesDirectory, "projects", projectName, projectName + ".csproj");

        BuildProject(
            projectFilePath,
            "Build",
            properties,
            logFilePath,
            verboseLog: false);
    }

    private static void BuildTestProjectTypeScript(string projectName, string logFilePath)
    {
        // This assumes the `npm` / `node` executables are on the current PATH.

        StreamWriter logWriter = new(File.Open(
            logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

        string exe = "npm";
        string args = "install";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Cannot use shell-execute while redirecting stdout stream.
            exe = "cmd";
            args = "/c npm install";
        }

        var npmStartInfo = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = ProjectDir(projectName),
        };

        Process npmProcess = Process.Start(npmStartInfo)!;
        string? errorOutput = LogOutput(npmProcess, logWriter);

        if (npmProcess.ExitCode != 0)
        {
            string failMessage = "npm install exited with code: " + npmProcess.ExitCode + ". " +
                (errorOutput != null ? "\n" + errorOutput + "\n" : string.Empty) +
                "Full output: " + logFilePath;
            Assert.Fail(failMessage);
        }
        else if (errorOutput != null)
        {
            Assert.Fail($"npm install produced error output:\n{errorOutput}\n" +
                "Full output: " + logFilePath);
        }

        var nodeStartInfo = new ProcessStartInfo("node", $"node_modules/typescript/bin/tsc")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = ProjectDir(projectName),
        };

        Process nodeProcess = Process.Start(nodeStartInfo)!;
        errorOutput = LogOutput(nodeProcess, logWriter);

        if (nodeProcess.ExitCode != 0)
        {
            string failMessage = "TS compile exited with code: " + nodeProcess.ExitCode + ". " +
                (errorOutput != null ? "\n" + errorOutput + "\n" : string.Empty) +
                "Full output: " + logFilePath;
            Assert.Fail(failMessage);
        }
        else if (errorOutput != null)
        {
            Assert.Fail($"TS compile produced error output:\n{errorOutput}\n" +
                "Full output: " + logFilePath);
        }
    }
}

#endif // NET7_0_OR_GREATER
