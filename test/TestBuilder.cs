// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

/// <summary>
/// Utility methods that assist with building and running test cases.
/// </summary>
internal static class TestBuilder
{
    // JS code locates test modules using these environment variables.
    public const string ModulePathEnvironmentVariableName = "TEST_DOTNET_MODULE_PATH";
    public const string HostPathEnvironmentVariableName = "TEST_DOTNET_HOST_PATH";
    public const string DotNetVersionEnvironmentVariableName = "TEST_DOTNET_VERSION";

    public static string Configuration { get; } =
#if DEBUG
    "Debug";
#else
    "Release";
#endif

    public static string RepoRootDirectory { get; } = GetRootDirectory();

    public static string TestCasesDirectory { get; } = GetTestCasesDirectory();

    private static string GetRootDirectory()
    {
        string? solutionDir = Path.GetDirectoryName(
#if NETFRAMEWORK
            new Uri(typeof(TestBuilder).Assembly.CodeBase).LocalPath)!;
#else
#pragma warning disable IL3000 // Assembly.Location returns an empty string for assemblies embedded in a single-file app
            typeof(TestBuilder).Assembly.Location)!;
#pragma warning restore IL3000
#endif

        // This assumes there is only a .SLN file at the root of the repo.
        while (Directory.GetFiles(solutionDir, "*.sln").Length == 0)
        {
            solutionDir = Path.GetDirectoryName(solutionDir);

            if (string.IsNullOrEmpty(solutionDir))
            {
                throw new DirectoryNotFoundException("Solution directory not found.");
            }
        }

        return solutionDir;
    }

    private static string GetTestCasesDirectory()
    {
        // This assumes tests are organized in this test/TestCases directory structure.
        string testCasesDir = Path.Combine(GetRootDirectory(), "test", "TestCases");

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
            if (Path.GetFileName(subDir) != "common")
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
                string subDirName = Path.GetFileName(subDir);
                if (subDirName != "common")
                {
                    moduleQueue.Enqueue(moduleName + '/' + subDirName);
                }
            }

            moduleName = moduleName.Replace(Path.DirectorySeparatorChar, '/');

            foreach (string jsFile in Directory.GetFiles(modulePath, "*.js")
              .Concat(Directory.GetFiles(modulePath, "*.ts")))
            {
                if (jsFile.EndsWith(".d.ts")) continue;
                string testCaseName = Path.GetFileNameWithoutExtension(jsFile);
                if (filter == null || filter(moduleName + "/" + testCaseName))
                {
                    yield return new[] { moduleName + "/" + testCaseName };
                }
            }
        }
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

    public static string GetBuildLogFilePath(string moduleName)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Combine(logDir, "build.log");
    }

    public static string GetRunLogFilePath(string prefix, string moduleName, string testCasePath)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Combine(logDir, $"{prefix}-{Path.GetFileName(testCasePath)}.log");
    }

    public static string GetCurrentPlatformRuntimeIdentifier()
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
          RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
          RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
          throw new PlatformNotSupportedException(
            "Platform not supported: " + Environment.OSVersion.Platform);

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
              "CPU architecture not supported: " + RuntimeInformation.ProcessArchitecture),
        };

        return $"{os}-{arch}";
    }

    public static string GetCurrentFrameworkTarget()
    {
        Version frameworkVersion = Environment.Version;
        return frameworkVersion.Major == 4 ? "net472" :
            $"net{frameworkVersion.Major}.{frameworkVersion.Minor}";
    }

    public static string CreateProjectFile(string moduleName)
    {
        string projectFilePath = Path.Combine(
            TestCasesDirectory, moduleName, moduleName + ".csproj");

        string noTypeDefs = "<PropertyGroup>\n" +
            "<GenerateNodeApiTypeDefinitions>false</GenerateNodeApiTypeDefinitions>\n" +
            "</PropertyGroup>\n";

        // Auto-generate an empty project file. All project info is inherited from
        // TestCases/Directory.Build.{props,targets}, except for certain test modules
        // that need to skip typedef generation.
        File.WriteAllText(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
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
      bool verboseLog = false)
    {
        if (GetNoBuild()) return;

        StreamWriter logWriter = new(File.Open(
            logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

        List<string> arguments = new()
        {
            "build",
            projectFilePath,
            "/t:" + target,
            verboseLog ? "/v:d" : "/v:n",
        };
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
            WorkingDirectory = Path.GetDirectoryName(logFilePath)!,
        };

        logWriter.WriteLine($"dotnet {startInfo.Arguments}");
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

    public static void RunNodeTestCase(
        string jsFilePath,
        string logFilePath,
        IDictionary<string, string> testEnvironmentVariables)
    {
        Assert.True(File.Exists(jsFilePath), "JS file not found: " + jsFilePath);

        // This assumes the `node` executable is on the current PATH.
        string nodeExe = "node";

        StreamWriter logWriter = new(File.Open(
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

    private static string? LogOutput(
        Process process,
        StreamWriter logWriter)
    {
        StringBuilder errorOutput = new();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                try
                {
                    logWriter.WriteLine(e.Data);
                    logWriter.Flush();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                try
                {
                    logWriter.WriteLine(e.Data);
                    logWriter.Flush();
                }
                catch (ObjectDisposedException)
                {
                }
                errorOutput.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Process.WaitForExit() may hang when redirecting output because it actually waits for the
        // stdout/stderr streams to be closed, which may not happen because `dotnet build` passes
        // the handles to additional child processes, which may be kept running by the build server.
        // https://github.com/dotnet/runtime/issues/29232
        while (!process.HasExited)
        {
            Thread.Sleep(100);
        }

        logWriter.Close();
        return errorOutput.Length > 0 ? errorOutput.ToString() : null;
    }

    public static void CopyIfNewer(string sourceFilePath, string targetFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("File not found: " + sourceFilePath, sourceFilePath);
        }

        // GetLastWriteTimeUtc returns MinValue if the target file doesn't exist.
        DateTime sourceTime = File.GetLastWriteTimeUtc(sourceFilePath);
        DateTime targetTime = File.GetLastWriteTimeUtc(targetFilePath);
        if (sourceTime > targetTime)
        {
            File.Copy(sourceFilePath, targetFilePath, overwrite: true);
        }
    }
}
