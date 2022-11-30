using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace NodeApi.Test;

using static TestBuilder;

public class NativeAotTests
{
  // JS code loads test modules via this environment variable:
  //    require(process.env['TEST_NODE_API_MODULE_PATH'])
  private const string modulePathEnvironmentVariableName = "TEST_NODE_API_MODULE_PATH";

  private static readonly Dictionary<string, string?> builtTestModules = new();

  public static IEnumerable<object[]> ListTestCases()
  {
    foreach (var dir in Directory.GetDirectories(TestCasesDirectory))
    {
      var moduleName = Path.GetFileName(dir);

      foreach (var jsFile in Directory.GetFiles(dir, "*.js")
        .Concat(Directory.GetFiles(dir, "*.ts")))
      {
        var testCaseName = Path.GetFileNameWithoutExtension(jsFile);
        yield return new[] { moduleName + "/" + testCaseName };
      }
    }
  }

  [Theory()]
  [MemberData(nameof(ListTestCases))]
  public void Test(string id)
  {
    var idParts = id.Split('/');
    var moduleName = idParts[0];
    var testCaseName = idParts[1];

    var buildLogFilePath = GetBuildLogFilePath(moduleName);
    if (!builtTestModules.TryGetValue(moduleName, out var moduleFilePath))
    {
      moduleFilePath = BuildTestModuleCSharp(moduleName, buildLogFilePath);

      if (moduleFilePath != null)
      {
        BuildTestModuleTypeScript(moduleName);
      }

      builtTestModules.Add(moduleName, moduleFilePath);
    }

    if (moduleFilePath == null)
    {
      Assert.Fail("Build failed. Check the log for details: " + buildLogFilePath);
    }

    // TODO: Support compiling TS files to JS.
    var jsFilePath = Path.Join(TestCasesDirectory, moduleName, testCaseName + ".js");

    var runLogFilePath = GetRunLogFilePath(moduleName, testCaseName);
    RunTestCase(jsFilePath, moduleFilePath, runLogFilePath);
  }

  private static string? BuildTestModuleCSharp(
    string testCaseName,
    string logFilePath)
  {
    var projectFilePath = Path.Join(TestCasesDirectory, testCaseName, testCaseName + ".csproj");

    // Auto-generate an empty project file. All project info is inherited from
    // TestCases/Directory.Build.{props,targets}
    File.WriteAllText(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

    var runtimeIdentifier = GetCurrentPlatformRuntimeIdentifier();
    var properties = new Dictionary<string, string>
    {
      ["RuntimeIdentifier"] = runtimeIdentifier,
      ["Configuration"] = Configuration,
    };

    var buildResult = BuildProject(
      projectFilePath,
      targets: new[] { "Restore", "Publish" },
      properties,
      returnProperty: "NativeBinary",
      logFilePath: logFilePath,
      verboseLog: false);

    if (string.IsNullOrEmpty(buildResult))
    {
      return null;
    }

    var moduleFilePath = buildResult.Replace(
      Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    moduleFilePath = Path.ChangeExtension(moduleFilePath, ".node");
    Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
    return moduleFilePath;
  }

  private static void BuildTestModuleTypeScript(string testCaseName)
  {
    // TODO: Compile TypeScript code, if the test uses TS.
    // Reference the generated type definitions from the C#?
  }

  private static void RunTestCase(
    string jsFilePath,
    string moduleFilePath,
    string logFilePath)
  {
    Assert.True(File.Exists(jsFilePath), "JS file not found: " + jsFilePath);

    Environment.SetEnvironmentVariable(modulePathEnvironmentVariableName, moduleFilePath);

    // This assumes the `node` executable is on the current PATH.
    var nodeExe = "node";

    var outputWriter = File.CreateText(logFilePath);
    outputWriter.WriteLine($"{modulePathEnvironmentVariableName}={moduleFilePath}");
    outputWriter.WriteLine($"{nodeExe} {jsFilePath}");
    outputWriter.WriteLine();
    outputWriter.Flush();
    bool hasErrorOutput = false;

    var startInfo = new ProcessStartInfo(nodeExe, jsFilePath)
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
