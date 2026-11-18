// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET10_0 || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestBuilder;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

[CollectionDefinition(nameof(EmbeddingPackageTests), DisableParallelization = true)]
[Collection(nameof(EmbeddingPackageTests))]
public sealed class EmbeddingPackageTests : IDisposable
{
    private const string WarningCode = "NODEAPIEMBED001";
    private readonly string _fixture = Path.Combine(TestCasesDirectory, "EmbeddingPackage");
    private readonly string _packages = Path.Combine(RepoRootDirectory, "out", "obj",
        Configuration, "TestCases", "EmbeddingConsumer", "packages");
    private readonly string _version;
    private readonly string _libNodeVersion;

    public EmbeddingPackageTests()
    {
        XElement versions = XElement.Load(Path.Combine(RepoRootDirectory, "out", "pkg", "version.props"));
        _version = versions.Descendants("NodeApiDotNetPackageVersion").Single().Value;
        _libNodeVersion = versions.Descendants("LibNodePackageVersion").Single().Value;
        if (Directory.Exists(_packages)) Directory.Delete(_packages, recursive: true);
        Directory.CreateDirectory(_packages);

        // A net10 run writes this file; net472 must use the repository SDK, not a stale file.
        File.Delete(Path.Combine(_fixture, "global.json"));
    }

    public void Dispose() => File.Delete(Path.Combine(_fixture, "global.json"));

#if NET10_0
    [Fact]
    public void PortableConsumerImportsEsmRegardlessOfPrefer32Bit()
    {
        string output = BuildConsumer("portable");
        AssertConsumerRuns(output);

        // Modern .NET ignores Prefer32Bit, so it must still build without NODEAPIEMBED001.
        output = BuildConsumer("prefer32bit", "AnyCPU", prefer32Bit: true);
        AssertConsumerRuns(output);
    }

    [Fact]
    public void WindowsPackageContainsFrameworkSentinelsAndTransitiveDependencies()
    {
        // Windows-only: other platforms omit net472, and this project has no dynamic skip mechanism.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        string extracted = Path.Combine(_packages, "inspection");
        string package = Path.Combine(RepoRootDirectory, "out", "pkg",
            $"Microsoft.JavaScript.NodeApi.Embedding.{_version}.nupkg");
        System.IO.Compression.ZipFile.ExtractToDirectory(package, extracted);
        XElement metadata = XDocument.Load(Directory.GetFiles(extracted, "*.nuspec").Single())
            .Root!.Elements().Single();
        XNamespace ns = metadata.Name.Namespace;
        Assert.Equal("Embeds Node.js in .NET apps: brings Microsoft.JavaScript.NodeApi and " +
            "Microsoft.JavaScript.LibNode (win/linux/osx, x64/arm64).",
            metadata.Element(ns + "description")!.Value);
        XElement[] groups = metadata.Element(ns + "dependencies")!.Elements().ToArray();
        string[] expectedFrameworks =
            { ".NETFramework4.7.2", ".NETStandard2.0", "net10.0", "net8.0", "net9.0" };
        Assert.Equal(expectedFrameworks,
            groups.Select(g => g.Attribute("targetFramework")!.Value).OrderBy(t => t, StringComparer.Ordinal));
        foreach (XElement group in groups)
        {
            XElement[] dependencies = group.Elements().ToArray();
            Assert.Equal(2, dependencies.Length);
            Assert.All(dependencies, d => Assert.Equal("All", d.Attribute("include")!.Value));
            Assert.Equal(_libNodeVersion, dependencies.Single(d =>
                d.Attribute("id")!.Value == "Microsoft.JavaScript.LibNode").Attribute("version")!.Value);
            Assert.Equal(_version, dependencies.Single(d =>
                d.Attribute("id")!.Value == "Microsoft.JavaScript.NodeApi").Attribute("version")!.Value);
        }

        foreach (string tfm in new[] { "net10.0", "net9.0", "net8.0", "netstandard2.0", "net472" })
        {
            Assert.True(File.Exists(Path.Combine(extracted, "lib", tfm, "_._")), tfm);
        }
        Assert.Empty(Directory.GetFiles(extracted, "*.dll", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.ChangeExtension(package, ".snupkg")));
        Assert.True(File.Exists(Path.Combine(extracted, "README.md")));
        Assert.True(File.Exists(Path.Combine(extracted, "buildTransitive",
            "Microsoft.JavaScript.NodeApi.Embedding.targets")));
    }
#else
    [Fact]
    public void FrameworkX64ConsumerImportsEsm()
    {
        string output = BuildConsumer("x64", "x64");
        Assert.True(File.Exists(Path.Combine(output, "runtimes", "win-x64", "native", "libnode.dll")));
        AssertConsumerRuns(output);
    }

    [Fact]
    public void FrameworkDefaultConsumerRunsAs64BitAnyCpu()
    {
        string output = BuildConsumer("default");
        Assert.Equal("AnyCPU", File.ReadAllText(Path.Combine(output, "platform.txt")).Trim());
        AssertConsumerRuns(output);
    }

    // Both explicit x86 and legacy Prefer32Bit must warn, without running a 32-bit process.
    [Theory]
    [InlineData("x86", false)]
    [InlineData("AnyCPU", true)]
    public void Framework32BitConsumerBuildWarns(string platform, bool prefer32Bit)
    {
        BuildConsumer("warning-" + platform, platform, prefer32Bit, expectWarning: true);
    }
#endif

    private string BuildConsumer(
        string lane, string? platform = null, bool prefer32Bit = false, bool expectWarning = false)
    {
        string framework = GetCurrentFrameworkTarget();
        string fallback = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var properties = new Dictionary<string, string>
        {
            ["Configuration"] = Configuration,
            ["TargetFrameworks"] = framework,
            ["ConsumerLane"] = lane,
            ["RestorePackagesPath"] = _packages,
            ["RestoreFallbackFolders"] = fallback,
            ["RestoreNoHttpCache"] = "true",
            ["RestoreForce"] = "true",
        };
        if (platform != null) properties["PlatformTarget"] = platform;
        if (prefer32Bit) properties["Prefer32Bit"] = "true";
        string log = GetBuildLogFilePath(lane, "EmbeddingConsumer");
        BuildProject(Path.Combine(_fixture, "EmbeddingConsumer.csproj"), "Rebuild", properties, log);
        string buildLog = File.ReadAllText(log);
        Assert.Equal(expectWarning, buildLog.Contains(WarningCode));
        foreach (string id in new[] { "microsoft.javascript.nodeapi", "microsoft.javascript.nodeapi.embedding" })
        {
            Assert.True(File.Exists(Path.Combine(_packages, id, _version, id + ".nuspec")), id);
        }
        // LibNode must come from the already-restored fallback, not another six-RID extraction.
        Assert.Empty(Directory.GetDirectories(_packages, "microsoft.javascript.libnode*"));
        return Path.Combine(RepoRootDirectory, "out", "bin", Configuration,
            "EmbeddingConsumer", lane, framework);
    }

    private static void AssertConsumerRuns(string output)
    {
        string executable = Path.Combine(output, "EmbeddingConsumer.exe");
        ProcessStartInfo start = new()
        {
            FileName = GetCurrentFrameworkTarget() == "net472" ? executable : "dotnet",
            Arguments = GetCurrentFrameworkTarget() == "net472" ? string.Empty :
                "\"" + Path.Combine(output, "EmbeddingConsumer.dll") + "\"",
            WorkingDirectory = output,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000))
        {
            process.Kill();
            Assert.Fail("Consumer did not exit within 60 seconds.");
        }
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, stdout + stderr);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("ESM=embedded ESM works", stdout);
        Assert.Contains("BITS=64", stdout);
        string? nodeVersion = null;
        NodejsEmbeddingTests.RunInNodejsEnvironment(() =>
            nodeVersion = (string)JSValue.Global["process"]["versions"]["node"]);
        Assert.Contains("NODE=" + nodeVersion, stdout);
    }
}
#endif
