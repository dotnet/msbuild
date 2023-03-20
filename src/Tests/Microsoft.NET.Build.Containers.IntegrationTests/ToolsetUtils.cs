// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class ToolsetUtils
{
    /// <summary>
    /// Returns the path with installed dotnet.
    /// Order of processing: 
    /// - DOTNET_ROOT environment variable
    /// - DOTNET_INSTALL_DIR environment variable
    /// - resolving dotnet executable path.
    /// </summary>
    /// <exception cref="InvalidOperationException">on unexpected environment result, i.e. common environment variables are not set.</exception>
    internal static string GetDotNetPath()
    {
        string? dotnetRootFromEnvironment = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        string? dotnetInstallDirFromEnvironment = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");

        if (!string.IsNullOrEmpty(dotnetRootFromEnvironment) && Directory.Exists(dotnetRootFromEnvironment))
        {
            return dotnetRootFromEnvironment;
        }
        else if (!string.IsNullOrEmpty(dotnetInstallDirFromEnvironment) && Directory.Exists(dotnetInstallDirFromEnvironment))
        {
            return dotnetInstallDirFromEnvironment;
        }
        string dotnetExePath = ResolveCommand("dotnet");
        string dotnetRoot = Path.GetDirectoryName(dotnetExePath) ?? throw new InvalidOperationException("dotnet executable is in the root?");
        if (Directory.Exists(dotnetInstallDirFromEnvironment))
        {
            Assert.False(true, "'dotnet' was not found.");
        }
        return dotnetRoot;
    }

    /// <summary>
    /// Gets path to RuntimeIdentifierGraph.json file.
    /// </summary>
    /// <returns></returns>
    internal static string GetRuntimeGraphFilePath()
    {
        string dotnetRoot = GetDotNetPath();
        DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

        var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

        return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
    }

    private static string ResolveCommand(string command)
    {
        char pathSplitChar;
        string[] extensions = new string[] { string.Empty };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string? pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? throw new InvalidOperationException("PATHEXT is not set.");
            pathSplitChar = ';';
            extensions = extensions
                .Concat(pathExt.Split(pathSplitChar))
                .ToArray();
        }
        else
        {
            pathSplitChar = ':';
        }

        string? path = Environment.GetEnvironmentVariable("PATH") ?? throw new InvalidOperationException("PATH is not set.");

        var paths = path.Split(pathSplitChar);
        string? result = extensions.SelectMany(ext => paths.Select(p => Path.Combine(p, command + ext)))
            .FirstOrDefault(File.Exists);

        return result ?? throw new InvalidOperationException("Could not resolve path to " + command);
    }
}
