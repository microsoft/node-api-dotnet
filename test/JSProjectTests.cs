// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

[CollectionDefinition(nameof(JSProjectTests), DisableParallelization = true)]
[Collection(nameof(JSProjectTests))]
public class JSProjectTests
{
    private const string DefaultFrameworkTarget = "net8.0";

    public static IEnumerable<object[]> TestCases { get; } = ListTestCases(
        (testCaseName) => testCaseName.StartsWith("projects/") &&
            IsCurrentTargetFramework(Path.GetFileName(testCaseName)));

    private static bool IsCurrentTargetFramework(string target)
    {
        string currentFrameworkTarget = GetCurrentFrameworkTarget();
        return target == "default" ? currentFrameworkTarget == DefaultFrameworkTarget :
            target == currentFrameworkTarget;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(string id)
    {
        string projectName = id.Split('/')[1];
        string moduleName = id.Split('/')[2];

        CleanTestProject(projectName);

        string buildLogFilePath = GetBuildLogFilePath(projectName, "projects");
        BuildTestProjectReferences(projectName, buildLogFilePath);

        string compileLogFilePath = GetBuildLogFilePath(
            projectName + "-" + moduleName, "projects");
        string tsConfigFile = "tsconfig." + Path.GetFileNameWithoutExtension(moduleName) + ".json";
        BuildTestProjectTypeScript(projectName,
            compileLogFilePath,
            File.Exists(Path.Combine(ProjectDir(projectName), tsConfigFile)) ?
                tsConfigFile : null);

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

    private static void BuildTestProjectTypeScript(
        string projectName,
        string logFilePath,
        string? tsConfigFile = null)
    {
        // This assumes the `npm` / `node` executables are on the current PATH.

        using StreamWriter logWriter = new(File.Open(
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

        logWriter.WriteLine("npm install");

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

        string nodeArgs = "node_modules/typescript/bin/tsc";
        if (!string.IsNullOrEmpty(tsConfigFile))
        {
            nodeArgs += " -p " + tsConfigFile;
        }
        var nodeStartInfo = new ProcessStartInfo("node", nodeArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = ProjectDir(projectName),
        };

        logWriter.WriteLine();
        logWriter.WriteLine("tsc");

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
