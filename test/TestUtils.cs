// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Test;

public static class TestUtils
{
    public static string GetRepoRootDirectory()
    {
#if NETFRAMEWORK
        string assemblyLocation = new Uri(typeof(TestUtils).Assembly.CodeBase).LocalPath;
#else
#pragma warning disable IL3000 // Assembly.Location returns an empty string for assemblies embedded in a single-file app
        string assemblyLocation = typeof(TestUtils).Assembly.Location!;
#pragma warning restore IL3000
#endif

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

    public static string GetLibnodePath() => Path.Combine(
        GetRepoRootDirectory(),
        "bin",
        GetCurrentPlatformRuntimeIdentifier(),
        "libnode" + GetSharedLibraryExtension());
}
