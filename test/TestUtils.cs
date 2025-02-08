// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.Test;

public static class TestUtils
{
    public static string GetAssemblyLocation()
    {
#if NETFRAMEWORK
        return new Uri(typeof(TestUtils).Assembly.CodeBase).LocalPath;
#else
        // Assembly.Location returns an empty string for assemblies embedded in a single-file app
        return typeof(TestUtils).Assembly.Location;
#endif
    }

    public static string GetRepoRootDirectory()
    {
        string assemblyLocation = GetAssemblyLocation();
        string? solutionDir = string.IsNullOrEmpty(assemblyLocation) ?
            Environment.CurrentDirectory : Path.GetDirectoryName(assemblyLocation);

        // This assumes there is only a .SLN file at the root of the repo.
        while (Directory.GetFiles(solutionDir!, "*.sln").Length == 0)
        {
            solutionDir = Path.GetDirectoryName(solutionDir);

            if (string.IsNullOrEmpty(solutionDir))
            {
                throw new DirectoryNotFoundException("Solution directory not found.");
            }
        }

        return solutionDir!;
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

    public static string GetSharedLibraryExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ".dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ".dylib";
        else return ".so";
    }

    public static string GetLibnodePath() =>
        Path.Combine(
            Path.GetDirectoryName(GetAssemblyLocation()) ?? string.Empty,
            "libnode" + GetSharedLibraryExtension());

    public static string? LogOutput(
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
