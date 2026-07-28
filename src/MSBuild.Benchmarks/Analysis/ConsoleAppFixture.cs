// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// Creates and restores a <c>dotnet new console</c> project to use as a realistic evaluation subject.
/// </summary>
/// <remarks>
/// The project must be restored, otherwise the <c>obj/*.nuget.g.props</c> and <c>obj/*.nuget.g.targets</c> imports
/// are missing and the evaluation is not representative of a real build.
/// </remarks>
internal sealed class ConsoleAppFixture : IDisposable
{
    private readonly bool _ownsDirectory;

    private ConsoleAppFixture(string projectFile, string rootDirectory, bool ownsDirectory)
    {
        ProjectFile = projectFile;
        RootDirectory = rootDirectory;
        _ownsDirectory = ownsDirectory;
    }

    public string ProjectFile { get; }

    public string RootDirectory { get; }

    /// <summary>
    /// Wraps an already existing project without taking ownership of its directory.
    /// </summary>
    public static ConsoleAppFixture FromExistingProject(string projectFile)
    {
        string fullPath = Path.GetFullPath(projectFile);
        return new ConsoleAppFixture(fullPath, Path.GetDirectoryName(fullPath)!, ownsDirectory: false);
    }

    /// <summary>
    /// Creates a new console app in a temporary directory and restores it.
    /// </summary>
    /// <param name="dotnetPath">The <c>dotnet</c> host to use. Defaults to <c>DOTNET_HOST_PATH</c> or <c>dotnet</c> on the path.</param>
    public static ConsoleAppFixture Create(string? dotnetPath = null)
    {
        dotnetPath ??= Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

        string root = Path.Combine(Path.GetTempPath(), $"msbuild-eval-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        RunDotnet(dotnetPath, root, "new console -o ConsoleApp");

        string projectDirectory = Path.Combine(root, "ConsoleApp");
        string projectFile = Path.Combine(projectDirectory, "ConsoleApp.csproj");

        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"'dotnet new console' did not produce '{projectFile}'.");
        }

        RunDotnet(dotnetPath, projectDirectory, "restore");

        return new ConsoleAppFixture(projectFile, root, ownsDirectory: true);
    }

    private static void RunDotnet(string dotnetPath, string workingDirectory, string arguments)
    {
        ProcessStartInfo startInfo = new(dotnetPath, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start '{dotnetPath}'.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{dotnetPath} {arguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
    }

    public void Dispose()
    {
        if (!_ownsDirectory)
        {
            return;
        }

        try
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; a leftover temp directory is not worth failing the analysis over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
