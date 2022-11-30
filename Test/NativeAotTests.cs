namespace NodeApi.Test;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class NativeAotTests
{

  public static IEnumerable<object[]> ListTestCases()
  {
    foreach (var dir in Directory.GetDirectories(TestBuilder.TestCasesDirectory))
    {
      // TODO: Check that directory contains a project file and a JS/TS file?
      var name = Path.GetFileName(dir);
      yield return new[] { name };
    }
  }

  [Theory()]
  [MemberData(nameof(ListTestCases))]
  public async Task TestCase(string name)
  {
    await Task.CompletedTask;
    Assert.NotEmpty(name);

    var configuration = "Debug";
    var moduleFilePath = BuildTestCaseCSharp(
      name, configuration, TestBuilder.GetBuildLogFilePath(name, configuration));
    var mainJsFilePath = BuildTestCaseTypeScript(name);
    RunTestCase(mainJsFilePath, moduleFilePath, TestBuilder.GetRunLogFilePath(name, configuration));
  }

  private static string BuildTestCaseCSharp(
    string testCaseName,
    string configuration,
    string logFilePath)
  {
    var projectFilePath =
      Path.Join(TestBuilder.TestCasesDirectory, testCaseName, testCaseName + ".csproj");
    Assert.True(File.Exists(projectFilePath), "Project file not found: " + projectFilePath);

    var runtimeIdentifier = "win-x64"; // TODO: Get the approriate RID for the current platform.
    var properties = new Dictionary<string, string>
    {
      ["RuntimeIdentifier"] = runtimeIdentifier,
      ["Configuration"] = configuration,
    };

    var buildResult = TestBuilder.BuildProject(
      projectFilePath,
      targets: new[] { "Restore", "Publish" },
      properties,
      returnProperty: "NativeBinary",
      logFilePath: logFilePath,
      verboseLog: false);

    Assert.False(
      string.IsNullOrEmpty(buildResult),
      "Build failed. Check the log for details: " + logFilePath);

    var moduleFilePath = buildResult.Replace(
      Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    moduleFilePath = Path.ChangeExtension(moduleFilePath, ".node");
    Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
    return moduleFilePath;
  }

  private static string BuildTestCaseTypeScript(string testCaseName)
  {
    // TODO: Compile TypeScript code, if the test uses TS.
    // Reference the generated type definitions from the C#?

    var mainJsFilePath =
      Path.Join(TestBuilder.TestCasesDirectory, testCaseName, testCaseName + ".js");
    Assert.True(File.Exists(mainJsFilePath), "JS file not found: " + mainJsFilePath);
    return mainJsFilePath;
  }

  private const string modulePathEnvironmentVariableName = "TEST_NODE_API_MODULE_PATH";

  private static void RunTestCase(
    string mainJsFilePath,
    string moduleFilePath,
    string logFilePath)
  {
    Environment.SetEnvironmentVariable(modulePathEnvironmentVariableName, moduleFilePath);

    // This assumes the `node` executable is on the current PATH.
    var nodeExe = "node";

    var outputWriter = File.CreateText(logFilePath);
    outputWriter.WriteLine($"{modulePathEnvironmentVariableName}={moduleFilePath}");
    outputWriter.WriteLine($"{nodeExe} {mainJsFilePath}");
    outputWriter.WriteLine();
    outputWriter.Flush();
    bool hasErrorOutput = false;

    var startInfo = new ProcessStartInfo(nodeExe, mainJsFilePath)
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };

    var nodeProcess = Process.Start(startInfo)!;
    nodeProcess.OutputDataReceived += (_, e) =>
    {
      if (e.Data != null)
      {
        outputWriter.WriteLine(e.Data);
      }
    };
    nodeProcess.ErrorDataReceived += (_, e) =>
    {
      if (e.Data != null)
      {
        outputWriter.WriteLine(e.Data);
        outputWriter.Flush();
        hasErrorOutput = e.Data.Trim().Length > 0;
      }
    };
    nodeProcess.BeginOutputReadLine();
    nodeProcess.BeginErrorReadLine();

    nodeProcess.WaitForExit();

    if (nodeProcess.ExitCode != 0)
    {
      Assert.Fail("Node process exited with code: " + nodeProcess.ExitCode + ". " +
      "Check the log for details: " + logFilePath);
    }
    else if (hasErrorOutput)
    {
      Assert.Fail("Node process produced error output. Check the log for details: " + logFilePath);
    }
  }
}
