using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;

namespace NodeApi.Test;

/// <summary>
/// Utility methods that assist with building and running test cases.
/// </summary>
internal static class TestBuilder
{
    private static bool s_msbuildInitialized = false;

    private static void InitializeMsbuild()
    {
        if (!s_msbuildInitialized)
        {
            VisualStudioInstance msbuildInstance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
              instance => instance.Version).First();
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
        string? solutionDir = Path.GetDirectoryName(typeof(NativeAotTests).Assembly.Location)!;

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
        // This assumes tests are organized in this Test/TestCases directory structure.
        string testCasesDir = Path.Join(GetRootDirectory(), "Test", "TestCases");

        if (!Directory.Exists(testCasesDir))
        {
            throw new DirectoryNotFoundException("Test cases directory not found.");
        }

        return testCasesDir;
    }

    public static string GetBuildLogFilePath(string moduleName)
    {
        string projectObjDir = Path.Join(RepoRootDirectory, "out", "obj", Configuration, "TestCases", moduleName);
        Directory.CreateDirectory(projectObjDir);
        return Path.Join(projectObjDir, "build.log");
    }

    public static string GetRunLogFilePath(string moduleName, string testCaseName)
    {
        string projectObjDir = Path.Join(RepoRootDirectory, "out", "obj", Configuration, "TestCases", moduleName);
        Directory.CreateDirectory(projectObjDir);
        return Path.Join(projectObjDir, testCaseName + ".log");
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

        var project = new Project(projectFilePath, properties, toolsVersion: null);
        bool buildResult = project.Build(targets, new[] { logger });
        if (!buildResult)
        {
            return null;
        }

        string returnValue = project.GetPropertyValue(returnProperty);
        return returnValue;
    }
}
