// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public sealed class PackageContractTests
{
    private const string TestCommit = "abcdef0123456789abcdef0123456789abcdef01";
    private const string ShortTestCommit = "abcdef0123";
    private readonly ITestOutputHelper _output;

    public PackageContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GeneratedPackagesHaveExpectedShippingContract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string packageOutput = Path.Combine(Path.GetTempPath(), $"{nameof(PackageContractTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageOutput);

        try
        {
            await PackProject(
                repositoryRoot,
                Path.Combine(repositoryRoot, "src", "TaskAnalyzer", "TaskAnalyzer.csproj"),
                packageOutput);
            await PackProject(
                repositoryRoot,
                Path.Combine(repositoryRoot, "src", "Framework", "Microsoft.Build.Framework.csproj"),
                packageOutput,
                metadataOnly: true);

            string analyzerPackagePath = GetSinglePackage(packageOutput, "Microsoft.Build.TaskAuthoring.Analyzer");
            string frameworkPackagePath = GetSinglePackage(packageOutput, "Microsoft.Build.Framework");

            using var analyzerPackage = new PackageArchiveReader(analyzerPackagePath);
            using var frameworkPackage = new PackageArchiveReader(frameworkPackagePath);

            VerifyAnalyzerPackage(repositoryRoot, analyzerPackage);
            VerifyFrameworkPackage(frameworkPackage);
            VerifyMatchingIcons(analyzerPackage, frameworkPackage);
            await VerifyExplicitPackageReferenceActivatesAnalyzer(
                repositoryRoot,
                packageOutput,
                analyzerPackage.NuspecReader.GetIdentity().Version.ToNormalizedString());
        }
        finally
        {
            Directory.Delete(packageOutput, recursive: true);
        }
    }

    private static void VerifyAnalyzerPackage(string repositoryRoot, PackageArchiveReader package)
    {
        var identity = package.NuspecReader.GetIdentity();
        string version = identity.Version.ToNormalizedString();
        string[] files = package.GetFiles().ToArray();

        identity.Id.ShouldBe("Microsoft.Build.TaskAuthoring.Analyzer");
        Regex.IsMatch(version, $@"^0\.1\.0-dev\.\d+\.\d+\.{ShortTestCommit}$").ShouldBeTrue(
            $"Expected a local 0.1.0 development version with commit SHA, but found '{version}'.");
        package.NuspecReader.GetRepositoryMetadata().Commit.ShouldBe(TestCommit);
        package.NuspecReader.GetDescription().ShouldBe(
            "Provides analyzer guidance for MSBuild task authoring and multithreaded task safety.");

        package.NuspecReader.GetReadme().ShouldBe("README.md");
        files.ShouldContain("README.md");
        ReadText(package, "README.md").ShouldBe(
            File.ReadAllText(Path.Combine(repositoryRoot, "src", "TaskAnalyzer", "README.md")));
        ReadText(package, "README.md").ShouldNotContain("](../");

        package.NuspecReader.GetIcon().ShouldBe("MSBuild-NuGet-Icon.png");
        files.ShouldContain("MSBuild-NuGet-Icon.png");
        files.Count(path => path == "analyzers/dotnet/cs/Microsoft.Build.TaskAuthoring.Analyzer.dll").ShouldBe(1);
        files.ShouldNotContain(path => path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));
        package.NuspecReader.GetDependencyGroups().SelectMany(group => group.Packages).ShouldBeEmpty();
    }

    private static void VerifyFrameworkPackage(PackageArchiveReader package)
    {
        string[] files = package.GetFiles().ToArray();

        files.ShouldNotContain(path =>
            path.EndsWith("Microsoft.Build.TaskAuthoring.Analyzer.dll", StringComparison.OrdinalIgnoreCase));
        package.NuspecReader
            .GetDependencyGroups()
            .SelectMany(group => group.Packages)
            .ShouldNotContain(dependency =>
                dependency.Id.Equals("Microsoft.Build.TaskAuthoring.Analyzer", StringComparison.OrdinalIgnoreCase));
    }

    private static void VerifyMatchingIcons(PackageArchiveReader analyzerPackage, PackageArchiveReader frameworkPackage)
    {
        ReadBytes(analyzerPackage, analyzerPackage.NuspecReader.GetIcon())
            .ShouldBe(ReadBytes(frameworkPackage, frameworkPackage.NuspecReader.GetIcon()));
    }

    private async Task VerifyExplicitPackageReferenceActivatesAnalyzer(
        string repositoryRoot,
        string packageOutput,
        string packageVersion)
    {
        string consumerDirectory = Path.Combine(packageOutput, "consumer");
        Directory.CreateDirectory(consumerDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, "Consumer.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Build.TaskAuthoring.Analyzer"
                                  Version="{packageVersion}"
                                  PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, "Task.cs"),
            """
            using System.Reflection;

            namespace Microsoft.Build.Framework
            {
                public interface ITask
                {
                }
            }

            public sealed class Task : Microsoft.Build.Framework.ITask
            {
                public void Execute() => Assembly.Load("Unsafe");
            }
            """);

        string output = await RunDotNet(
            repositoryRoot,
            consumerDirectory,
            "build",
            "Consumer.csproj",
            "--nologo",
            "--disable-build-servers",
            $"-p:RestoreSources={packageOutput}",
            $"-p:RestorePackagesPath={Path.Combine(packageOutput, "packages")}");

        output.ShouldContain("MSBuildTask0004");
    }

    private async Task PackProject(
        string repositoryRoot,
        string projectPath,
        string packageOutput,
        bool metadataOnly = false)
    {
        string[] arguments =
        [
            "pack",
            projectPath,
            "--nologo",
            "--disable-build-servers",
            "--configuration",
            "Debug",
            "--output",
            packageOutput,
            "-p:ContinuousIntegrationBuild=false",
            "-p:OfficialBuild=false",
            $"-p:SourceRevisionId={TestCommit}",
            "-p:EnablePackageValidation=false",
            "-p:UseSharedCompilation=false",
        ];

        if (metadataOnly)
        {
            arguments =
            [
                .. arguments,
                "--no-build",
                "-p:IncludeBuildOutput=false",
                "-p:TargetsForTfmSpecificBuildOutput=",
                "-p:CreateTlb=false",
                "-p:NoWarn=NU5127%3BNU5128",
            ];
        }

        await RunDotNet(repositoryRoot, repositoryRoot, arguments);
    }

    private async Task<string> RunDotNet(
        string repositoryRoot,
        string workingDirectory,
        params string[] arguments)
    {
        string dotnetRoot = Path.Combine(repositoryRoot, ".dotnet");
        string dotnetPath = Path.Combine(
            dotnetRoot,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var startInfo = new ProcessStartInfo(dotnetPath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
        startInfo.Environment[$"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}"] = dotnetRoot;
        startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{dotnetPath}'.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"dotnet {string.Join(' ', arguments)} exceeded five minutes.");
        }

        string output = await standardOutput;
        string error = await standardError;
        _output.WriteLine(output);
        _output.WriteLine(error);

        process.ExitCode.ShouldBe(0, $"dotnet {string.Join(' ', arguments)} failed.");
        return $"{output}{Environment.NewLine}{error}";
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MSBuild.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the MSBuild repository root.");
    }

    private static string GetSinglePackage(string packageOutput, string packageId)
    {
        return Directory
            .GetFiles(packageOutput, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .ShouldHaveSingleItem();
    }

    private static string ReadText(PackageArchiveReader package, string path)
    {
        using Stream stream = package.GetStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(PackageArchiveReader package, string path)
    {
        using Stream stream = package.GetStream(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
