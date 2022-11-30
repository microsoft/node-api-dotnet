using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
  private static bool msbuildInitialized = false;

  private static void InitializeMsbuild()
  {
    if (!msbuildInitialized)
    {
      var msbuildInstance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
        instance => instance.Version).First();
      MSBuildLocator.RegisterInstance(msbuildInstance);
      msbuildInitialized = true;
    }
  }

  public static string RepoRootDirectory { get; } = GetRootDirectory();

  public static string TestCasesDirectory { get; } = GetTestCasesDirectory();

  private static string GetRootDirectory()
  {
    var solutionDir = Path.GetDirectoryName(typeof(NativeAotTests).Assembly.Location)!;

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
    var testCasesDir = Path.Join(GetRootDirectory(), "Test", "TestCases");

    if (!Directory.Exists(testCasesDir))
    {
      throw new DirectoryNotFoundException("Test cases directory not found.");
    }

    return testCasesDir;
  }

  public static string GetBuildLogFilePath(string projectName, string configuration)
  {
    var projectObjDir = Path.Join(RepoRootDirectory, "out", "obj", configuration, projectName);
    Directory.CreateDirectory(projectObjDir);
    return Path.Join(projectObjDir, "build.log");
  }

  public static string GetRunLogFilePath(string projectName, string configuration)
  {
    var projectObjDir = Path.Join(RepoRootDirectory, "out", "obj", configuration, projectName);
    Directory.CreateDirectory(projectObjDir);
    return Path.Join(projectObjDir, "run.log");
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
    var buildResult = project.Build(targets, new[] { logger });
    if (!buildResult)
    {
      return null;
    }

    var returnValue = project.GetPropertyValue(returnProperty);
    return returnValue;
  }
}
