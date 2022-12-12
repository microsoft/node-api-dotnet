using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

using static NodeApi.Test.TestBuilder;

namespace NodeApi.Test;
public class NativeAotTests
{
    // JS code loads test modules via this environment variable:
    //    require(process.env['TEST_NODE_API_MODULE_PATH'])
    private const string ModulePathEnvironmentVariableName = "TEST_NODE_API_MODULE_PATH";

    private static readonly Dictionary<string, string?> s_builtTestModules = new();

    public static IEnumerable<object[]> ListTestCases()
    {
        foreach (string dir in Directory.GetDirectories(TestCasesDirectory))
        {
            string moduleName = Path.GetFileName(dir);

            foreach (string? jsFile in Directory.GetFiles(dir, "*.js")
              .Concat(Directory.GetFiles(dir, "*.ts")))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(jsFile);
                yield return new[] { moduleName + "/" + testCaseName };
            }
        }
    }

    [Theory()]
    [MemberData(nameof(ListTestCases))]
    public void Test(string id)
    {
        string[] idParts = id.Split('/');
        string moduleName = idParts[0];
        string testCaseName = idParts[1];

        string buildLogFilePath = GetBuildLogFilePath(moduleName);
        if (!s_builtTestModules.TryGetValue(moduleName, out string? moduleFilePath))
        {
            moduleFilePath = BuildTestModuleCSharp(moduleName, buildLogFilePath);

            if (moduleFilePath != null)
            {
                BuildTestModuleTypeScript(moduleName);
            }

            s_builtTestModules.Add(moduleName, moduleFilePath);
        }

        if (moduleFilePath == null)
        {
            Assert.Fail("Build failed. Check the log for details: " + buildLogFilePath);
        }

        // TODO: Support compiling TS files to JS.
        string jsFilePath = Path.Join(TestCasesDirectory, moduleName, testCaseName + ".js");

        string runLogFilePath = GetRunLogFilePath(moduleName, testCaseName);
        RunTestCase(jsFilePath, moduleFilePath, runLogFilePath);
    }

    private static string? BuildTestModuleCSharp(
      string testCaseName,
      string logFilePath)
    {
        string projectFilePath = Path.Join(TestCasesDirectory, testCaseName, testCaseName + ".csproj");

        // Auto-generate an empty project file. All project info is inherited from
        // TestCases/Directory.Build.{props,targets}
        File.WriteAllText(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

        string runtimeIdentifier = GetCurrentPlatformRuntimeIdentifier();
        var properties = new Dictionary<string, string>
        {
            ["RuntimeIdentifier"] = runtimeIdentifier,
            ["Configuration"] = Configuration,
        };

        string? buildResult = BuildProject(
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

        string moduleFilePath = buildResult.Replace(
          Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        moduleFilePath = Path.ChangeExtension(moduleFilePath, ".node");
        Assert.True(File.Exists(moduleFilePath), "Module file was not built: " + moduleFilePath);
        return moduleFilePath;
    }

    private static void BuildTestModuleTypeScript(string _ /*testCaseName*/)
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

        Environment.SetEnvironmentVariable(ModulePathEnvironmentVariableName, moduleFilePath);

        // This assumes the `node` executable is on the current PATH.
        string nodeExe = "node";

        StreamWriter outputWriter = File.CreateText(logFilePath);
        outputWriter.WriteLine($"{ModulePathEnvironmentVariableName}={moduleFilePath}");
        outputWriter.WriteLine($"{nodeExe} --expose-gc {jsFilePath}");
        outputWriter.WriteLine();
        outputWriter.Flush();
        bool hasErrorOutput = false;

        var startInfo = new ProcessStartInfo(nodeExe, $"--expose-gc {jsFilePath}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process nodeProcess = Process.Start(startInfo)!;
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
