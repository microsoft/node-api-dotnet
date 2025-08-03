// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

/// <summary>
/// Utility methods that assist with building and running test cases.
/// </summary>
internal static class TestBuilder
{
    // JS code locates test modules using these environment variables.
    public const string ModulePathEnvironmentVariableName = "NODE_API_TEST_MODULE_PATH";
    public const string DotNetVersionEnvironmentVariableName = "NODE_API_TEST_TARGET_FRAMEWORK";

    public static string Configuration { get; } =
#if DEBUG
    "Debug";
#else
    "Release";
#endif

    public static string RepoRootDirectory { get; } = GetRepoRootDirectory();

    public static string TestCasesDirectory { get; } = GetTestCasesDirectory();

    private static string GetTestCasesDirectory()
    {
        // This assumes tests are organized in this test/TestCases directory structure.
        string testCasesDir = Path.Combine(GetRepoRootDirectory(), "test", "TestCases");

        if (!Directory.Exists(testCasesDir))
        {
            throw new DirectoryNotFoundException("Test cases directory not found.");
        }

        return testCasesDir;
    }

    public static IEnumerable<object[]> ListTestCases(Predicate<string>? filter = null)
    {
        var moduleQueue = new Queue<string>();
        foreach (string subDir in Directory.GetDirectories(TestCasesDirectory))
        {
            if (!IsExcludedSubDirectory(subDir))
            {
                moduleQueue.Enqueue(Path.GetFileName(subDir));
            }
        }

        while (moduleQueue.Count > 0)
        {
            string moduleName = moduleQueue.Dequeue();
            string modulePath = Path.Combine(
                TestCasesDirectory,
                moduleName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            foreach (string subDir in Directory.GetDirectories(modulePath))
            {
                if (!IsExcludedSubDirectory(subDir))
                {
                    moduleQueue.Enqueue(moduleName + '/' + Path.GetFileName(subDir));
                }
            }

            moduleName = moduleName.Replace(Path.DirectorySeparatorChar, '/');

            foreach (string jsFile in Directory.GetFiles(modulePath, "*.js")
              .Concat(Directory.GetFiles(modulePath, "*.ts")))
            {
                if (jsFile.EndsWith(".d.ts")) continue;
                else if (jsFile.EndsWith(".js") && File.Exists(Path.ChangeExtension(jsFile, ".ts"))) continue;
                string testCaseName = Path.GetFileNameWithoutExtension(jsFile);
                if (filter == null || filter(moduleName + "/" + testCaseName))
                {
                    yield return new[] { moduleName + "/" + testCaseName };
                }
            }
        }
    }

    private static bool IsExcludedSubDirectory(string directoryName)
    {
        string name = Path.GetFileName(directoryName);
        return name switch
        {
            "common" => true,
            "bin" => true,
            "out" => true,
            "node_modules" => true,
            _ => false,
        };
    }

    private static string GetModuleIntermediateOutputPath(string moduleName)
    {
        string directoryPath = Path.Combine(
            RepoRootDirectory,
            string.Join(
                Path.DirectorySeparatorChar.ToString(),
                "out",
                "obj",
                Configuration,
                "TestCases",
                moduleName,
                GetCurrentFrameworkTarget()));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    public static string GetBuildLogFilePath(string prefix, string moduleName)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Combine(logDir, prefix + "-build.log");
    }

    public static string GetRunLogFilePath(string prefix, string moduleName, string testCasePath)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Combine(logDir, $"{prefix}-{Path.GetFileName(testCasePath)}.log");
    }

    public static string CreateProjectFile(string moduleName)
    {
        string projectFilePath = Path.Combine(
            TestCasesDirectory, moduleName, moduleName + ".csproj");

        // Test builds should use the same target framework that the current test is using.
        string targetFramework = "<PropertyGroup>\n" +
            $"  <TargetFrameworks>{GetCurrentFrameworkTarget()}</TargetFrameworks>\n" +
            "</PropertyGroup>\n";

        // Certain test modules need to skip typedef generation.
        string noTypeDefs = "<PropertyGroup>\n" +
            "  <GenerateNodeApiTypeDefinitions>false</GenerateNodeApiTypeDefinitions>\n" +
            "</PropertyGroup>\n";

        // Auto-generate a simple project file. Almost all project info is inherited from
        // TestCases/Directory.Build.{props,targets}.
        File.WriteAllText(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
            targetFramework +
            (moduleName == "napi-dotnet-init" ? noTypeDefs : string.Empty) +
            "</Project>\n");

        return projectFilePath;
    }

    private static bool GetNoBuild()
    {
        string filePath = Path.Combine(
            RepoRootDirectory,
            "out",
            "obj",
            Configuration,
            "NodeApi.Test",
            GetCurrentFrameworkTarget(),
            GetCurrentPlatformRuntimeIdentifier(),
            "no-build.txt");
        return File.Exists(filePath);
    }

    public static void BuildProject(
      string projectFilePath,
      string target,
      IDictionary<string, string> properties,
      string logFilePath,
      bool noRestore = false,
      bool verboseLog = false)
    {
        if (GetNoBuild()) return;

        string workingDirectory = Path.GetDirectoryName(logFilePath)!;
        if (target != "Publish")
        {
            WriteCurrentFrameworkGlobalJson(workingDirectory);
        }

        using StreamWriter logWriter = new(File.Open(
            logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

        List<string> arguments = new()
        {
            target == "Restore" ? "restore" : "build",
            projectFilePath,
            "/t:" + target,
            verboseLog ? "/v:d" : "/v:n",
        };

        if (noRestore)
        {
            arguments.Add("--no-restore");
        }

        foreach (KeyValuePair<string, string> property in properties)
        {
            arguments.Add($"/p:{property.Key}={property.Value}");
        }
        ProcessStartInfo startInfo = new(
            "dotnet",
            string.Join(" ", arguments.Select((a) => a.Contains(' ') ? $"\"{a}\"" : a).ToArray()))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };

        if (Environment.Version.Major == 8)
        {
            // Prevent the launched build from using the same MSBuild SDK that was used to run the test.
            startInfo.Environment["MSBuildSDKsPath"] = "";
        }

        logWriter.WriteLine($"dotnet {startInfo.Arguments}");
        logWriter.WriteLine($"CWD={workingDirectory}");
        logWriter.WriteLine();
        logWriter.Flush();

        Process buildProcess = Process.Start(startInfo)!;
        string? errorOutput = LogOutput(buildProcess, logWriter);

        if (buildProcess.ExitCode != 0)
        {
            string failMessage = "Build process exited with code: " + buildProcess.ExitCode + ". " +
                (errorOutput != null ? "\n" + errorOutput + "\n" : string.Empty) +
                "Full output: " + logFilePath;
            Assert.Fail(failMessage);
        }
        else if (errorOutput != null)
        {
            Assert.Fail($"Build process produced error output:\n{errorOutput}\n" +
                "Full output: " + logFilePath);
        }
    }

    private static void WriteCurrentFrameworkGlobalJson(string workingDirectory)
    {
        Version frameworkVersion = Environment.Version;
        if (frameworkVersion.Major == 4)
        {
            // .NET 4.x is supported at runtime, but not at build time.
            // So the global.json at the repo root will determine the SDK.
            return;
        }

        // Write a global.json file to the working directory to specify an SDK version
        // that matches the current runtime major version. Roll forward to the latest
        // installed SDK within the same major version.
        string sdkVersion = frameworkVersion.Major + "." + frameworkVersion.Minor + ".100";

        if (RuntimeInformation.FrameworkDescription.Contains("-preview."))
        {
            // Append the .NET preview version to the SDK version requested in global.json.
            sdkVersion += RuntimeInformation.FrameworkDescription.Substring(
                RuntimeInformation.FrameworkDescription.IndexOf("-preview."));
        }

        string globalJson = $$"""
            {
                "sdk": {
                    "version": "{{sdkVersion}}",
                    "allowPrerelease": true,
                    "rollForward": "latestFeature"
                }
            }
            """;
        string globalJsonFilePath = Path.Combine(workingDirectory, "global.json");
        File.WriteAllText(globalJsonFilePath, globalJson);
    }

    public static void RunNodeTestCase(
        string jsFilePath,
        string logFilePath,
        IDictionary<string, string> testEnvironmentVariables)
    {
        Assert.True(File.Exists(jsFilePath), "JS file not found: " + jsFilePath);

        // This assumes the `node` executable is on the current PATH.
        string nodeExe = "node";

        using StreamWriter logWriter = new(File.Open(
            logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

        var startInfo = new ProcessStartInfo(nodeExe, $"--expose-gc {jsFilePath}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(logFilePath)!,
        };

        foreach (KeyValuePair<string, string> pair in testEnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
            logWriter.WriteLine($"{pair.Key}={pair.Value}");
        }

        logWriter.WriteLine($"{nodeExe} --expose-gc {jsFilePath}");
        logWriter.WriteLine();
        logWriter.Flush();

        Process nodeProcess = Process.Start(startInfo)!;
        string? errorOutput = LogOutput(nodeProcess, logWriter);

        if (nodeProcess.ExitCode != 0)
        {
            string failMessage = "Node process exited with code: " + nodeProcess.ExitCode + ". " +
                (errorOutput != null ? "\n" + errorOutput + "\n" : string.Empty) +
                "Full output: " + logFilePath;

            logWriter.Close();
            string jsFileName = Path.GetFileName(jsFilePath);
            string[] logLines = File.ReadAllLines(logFilePath);
            for (int i = 0; i < logLines.Length; i++)
            {
                // Scan for a line that looks like a node error or an assertion with filename:#.
                if (logLines[i].StartsWith("node:") ||
                    logLines[i].Contains(jsFileName + ":"))
                {
                    string assertion = string.Join(Environment.NewLine, logLines.Skip(i));
                    failMessage = assertion +
                        Environment.NewLine + Environment.NewLine + failMessage;
                    break;
                }
            }

            Assert.Fail(failMessage);
        }
        else if (errorOutput != null)
        {
            Assert.Fail($"Build process produced error output:\n{errorOutput}\n" +
                "Full output: " + logFilePath);
        }
    }
}
