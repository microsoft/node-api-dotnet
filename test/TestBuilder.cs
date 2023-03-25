// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
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

    private static bool s_msbuildInitialized = false;

    private static void InitializeMsbuild()
    {
        if (!s_msbuildInitialized)
        {
            // Find an istalled instance of MSBuild that matches the current runtime major version
            // and is not a preview.
            VisualStudioInstance[] msbuildInstances =
                MSBuildLocator.QueryVisualStudioInstances().ToArray();
            VisualStudioInstance msbuildInstance = msbuildInstances
                .Where((instance) => instance.Version.Major == Environment.Version.Major &&
                    !instance.MSBuildPath.Contains("preview"))
                .OrderByDescending(instance => instance.Version)
                .First();
            MSBuildLocator.RegisterInstance(msbuildInstance);
            s_msbuildInitialized = true;
        }
    }

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
#pragma warning disable IL3000 // Assembly.Location returns an empty string for assemblies embedded in a single-file app
        string? solutionDir = Path.GetDirectoryName(typeof(TestBuilder).Assembly.Location)!;
#pragma warning restore IL3000

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
        string testCasesDir = Path.Join(GetRootDirectory(), "test", "TestCases");

        if (!Directory.Exists(testCasesDir))
        {
            throw new DirectoryNotFoundException("Test cases directory not found.");
        }

        return testCasesDir;
    }

    public static IEnumerable<object[]> ListTestCases(Predicate<string>? filter = null)
    {
        var dirQueue = new Queue<string>();
        dirQueue.Enqueue(TestCasesDirectory);
        while (dirQueue.Count > 0)
        {
            string dir = dirQueue.Dequeue();
            foreach (string subDir in Directory.GetDirectories(dir))
            {
                if (subDir != "common")
                {
                    dirQueue.Enqueue(subDir);
                }
            }

            string moduleName = Path.GetRelativePath(TestCasesDirectory, dir);
            if (string.IsNullOrEmpty(moduleName))
            {
                continue;
            }

            moduleName = moduleName.Replace(Path.DirectorySeparatorChar, '/');

            foreach (string jsFile in Directory.GetFiles(dir, "*.js")
              .Concat(Directory.GetFiles(dir, "*.ts")))
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
        string directoryPath = Path.Join(
            RepoRootDirectory,
            "out",
            "obj",
            Configuration,
            "TestCases",
            moduleName,
            GetCurrentFrameworkTarget());
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    public static string GetBuildLogFilePath(string moduleName)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Join(logDir, "build.log");
    }

    public static string GetRunLogFilePath(string prefix, string moduleName, string testCasePath)
    {
        string logDir = GetModuleIntermediateOutputPath(moduleName);
        return Path.Join(logDir, $"{prefix}-{Path.GetFileName(testCasePath)}.log");
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

    public static string? BuildProject(
      string projectFilePath,
      string[] targets,
      IDictionary<string, string> properties,
      string returnProperty,
      string logFilePath,
      bool verboseLog = false)
    {
        // MSBuild must be explicitly located & initialized before being loaded by the JIT,
        // therefore any use of MSBuild types must be kept in separate methods called by this one.
        InitializeMsbuild();

        return BuildProjectInternal(
            projectFilePath, targets, properties, returnProperty, logFilePath, verboseLog);
    }

    private static string? BuildProjectInternal(
      string projectFilePath,
      string[] targets,
      IDictionary<string, string> properties,
      string returnProperty,
      string logFilePath,
      bool verboseLog = false)
    {
        var logger = new FileLogger
        {
            Parameters = "LOGFILE=" + logFilePath,
            Verbosity = verboseLog ? LoggerVerbosity.Diagnostic : LoggerVerbosity.Normal,
        };

        using var projectCollection = new ProjectCollection();

        Project project = projectCollection.LoadProject(projectFilePath, properties, toolsVersion: null);
        bool buildResult = project.Build(targets, new[] { logger });
        if (!buildResult)
        {
            return null;
        }

        string returnValue = project.GetPropertyValue(returnProperty);
        return returnValue;
    }

    public static void BuildTypeDefinitions(string moduleName, string moduleFilePath)
    {
        string typeDefinitionsFilePath = Path.Join(
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

        StreamWriter outputWriter = new(logFilePath, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Read,
        });
        bool hasErrorOutput = false;

        var startInfo = new ProcessStartInfo(nodeExe, $"--expose-gc {jsFilePath}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(logFilePath)!,
        };

        foreach ((string name, string value) in testEnvironmentVariables)
        {
            startInfo.Environment[name] = value;
            outputWriter.WriteLine($"{name}={value}");
        }

        outputWriter.WriteLine($"{nodeExe} --expose-gc {jsFilePath}");
        outputWriter.WriteLine();
        outputWriter.Flush();

        Process nodeProcess = Process.Start(startInfo)!;
        nodeProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outputWriter)
                {
                    outputWriter.WriteLine(e.Data);
                    outputWriter.Flush();
                }
            }
        };
        nodeProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outputWriter)
                {
                    outputWriter.WriteLine(e.Data);
                    outputWriter.Flush();
                    hasErrorOutput = e.Data.Trim().Length > 0;
                }
            }
        };
        nodeProcess.BeginOutputReadLine();
        nodeProcess.BeginErrorReadLine();

        nodeProcess.WaitForExit();
        outputWriter.Close();

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
}
