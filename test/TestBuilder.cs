// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Generator;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

/// <summary>
/// Utility methods that assist with building and running test cases.
/// </summary>
internal static class TestBuilder
{
    // JS code loads test modules via these environment variables:
    //     const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
    //     const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];
    //     const test = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);
    // (A real module would choose between one or the other, so its require code would be simpler.)
    public const string ModulePathEnvironmentVariableName = "TEST_DOTNET_MODULE_PATH";
    public const string HostPathEnvironmentVariableName = "TEST_DOTNET_HOST_PATH";

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
            if (subDir != "common")
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
        return $"net{frameworkVersion.Major}.{frameworkVersion.Minor}";
    }

    private static bool GetNoBuild()
    {
        string filePath = Path.Join(
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

        StreamWriter logWriter = new(logFilePath, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Read,
        });

        ProcessStartInfo startInfo = new("dotnet");
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectFilePath);
        startInfo.ArgumentList.Add("/t:" + target);
        startInfo.ArgumentList.Add(verboseLog ? "/v:d" : "/v:n");
        foreach (KeyValuePair<string, string> property in properties)
        {
            startInfo.ArgumentList.Add($"/p:{property.Key}={property.Value}");
        }

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.WorkingDirectory = Path.GetDirectoryName(logFilePath)!;

        logWriter.WriteLine($"dotnet {string.Join(" ", startInfo.ArgumentList)}");
        logWriter.WriteLine();
        logWriter.Flush();

        Process buildProcess = Process.Start(startInfo)!;

        bool hasErrorOutput = LogOutput(buildProcess, logWriter);

        if (buildProcess.ExitCode != 0)
        {
            string failMessage = "Build process exited with code: " + buildProcess.ExitCode + ". " +
                "Check the log for details: " + logFilePath;
            Assert.Fail(failMessage);
        }
        else if (hasErrorOutput)
        {
            Assert.Fail("Build process produced error output. " +
                "Check the log for details: " + logFilePath);
        }
    }

    public static void BuildTypeDefinitions(string moduleName, string moduleFilePath)
    {
        string typeDefinitionsFilePath = Path.Combine(
            TestCasesDirectory, moduleName, moduleName + ".d.ts");
        TypeDefinitionsGenerator.GenerateTypeDefinitions(
            moduleFilePath, referenceAssemblyPaths: Array.Empty<string>(), typeDefinitionsFilePath);
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
            logFilePath, FileMode.Create, FileAccess.Write,  FileShare.Read));

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
        bool hasErrorOutput = LogOutput(nodeProcess, logWriter);

        if (nodeProcess.ExitCode != 0)
        {
            string failMessage = "Node process exited with code: " + nodeProcess.ExitCode + ". " +
                "Check the log for details: " + logFilePath;

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
        else if (hasErrorOutput)
        {
            Assert.Fail("Node process produced error output. " +
                "Check the log for details: " + logFilePath);
        }
    }

    private static bool LogOutput(
        Process process,
        StreamWriter logWriter)
    {
        bool hasErrorOutput = false;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (logWriter)
                {
                    logWriter.WriteLine(e.Data);
                    logWriter.Flush();
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (logWriter)
                {
                    logWriter.WriteLine(e.Data);
                    logWriter.Flush();
                    hasErrorOutput = e.Data.Trim().Length > 0;
                }
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
        logWriter.Close();
        return hasErrorOutput;
    }
}
