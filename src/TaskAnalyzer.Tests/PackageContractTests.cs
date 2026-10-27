// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NuGet.Packaging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public sealed class PackageContractTests
{
    private const string TestCommit = "abcdef0123456789abcdef0123456789abcdef01";
    private readonly ITestOutputHelper _output;

    public PackageContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GeneratedPackagesHaveExpectedShippingContract()
    {
        string repositoryRoot = TaskAnalyzerTestsConstants.RepoRoot;
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

            analyzerPackage.NuspecReader.GetIdentity().Version.ShouldBe(
                frameworkPackage.NuspecReader.GetIdentity().Version,
                "TaskAnalyzer should use the repository-wide package version.");
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
        string[] files = package.GetFiles().ToArray();

        identity.Id.ShouldBe("Microsoft.Build.TaskAuthoring.Analyzer");
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
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
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
                public void Execute()
                {
                    Assembly.Load("Unsafe");
                    Helper.Run();
                }
            }

            public static class Helper
            {
                public static void Run()
                {
                    Assembly.Load("Unsafe");
                    System.Environment.Exit(1);
                }
            }
            """);

        string output = await RunDotNet(
            repositoryRoot,
            consumerDirectory,
            "build",
            "Consumer.csproj",
            "--nologo",
            "--disable-build-servers",
            "-p:TreatWarningsAsErrors=true",
            $"-p:RestoreSources={packageOutput}",
            $"-p:RestorePackagesPath={Path.Combine(packageOutput, "packages")}");

        output.ShouldNotContain("warning MSBuildTask");
        output.ShouldNotContain("error MSBuildTask");
        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, ".editorconfig"),
            """
            root = true

            [*.cs]
            dotnet_diagnostic.MSBuildTask0004.severity = warning
            dotnet_diagnostic.MSBuildTask0005.severity = warning
            """);

        output = await RunDotNet(
            repositoryRoot,
            consumerDirectory,
            "build",
            "Consumer.csproj",
            "--nologo",
            "--disable-build-servers",
            "--no-restore",
            "-t:Rebuild",
            "-p:TreatWarningsAsErrors=true");

        output.ShouldNotContain("warning MSBuildTask");
        output.ShouldNotContain("error MSBuildTask");

        await File.AppendAllTextAsync(
            Path.Combine(consumerDirectory, ".editorconfig"),
            "\nmsbuild_task_analyzer.run_mt_analyzers_on_all_tasks = true\n");

        output = await RunDotNet(
            repositoryRoot,
            consumerDirectory,
            "build",
            "Consumer.csproj",
            "--nologo",
            "--disable-build-servers",
            "--no-restore",
            "-t:Rebuild");

        output.ShouldContain("warning MSBuildTask0004");
        output.ShouldContain("warning MSBuildTask0005");

        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.GetEnvironmentVariable(\"KEY\")", "", [], "warning", 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Reflection.Assembly.Load(\"Unsafe\")", "", [], "warning", 0);
        output = await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Reflection.Assembly.Load(\"Unsafe\")", "dotnet_diagnostic.MSBuildTask0004.severity = error",
            [], "warning", 1);
        output.ShouldContain("error MSBuildTask0004");
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.GetEnvironmentVariable(\"KEY\")", "",
            ["-p:TreatWarningsAsErrors=true", "-p:WarningsNotAsErrors=MSBuildTask0002"], "error", 1);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.GetEnvironmentVariable(\"KEY\")", "dotnet_diagnostic.MSBuildTask0002.severity = none",
            ["-p:TreatWarningsAsErrors=true", "-p:WarningsNotAsErrors=MSBuildTask0005"], "warning", 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.Exit(1)", "dotnet_diagnostic.MSBuildTask0001.severity = warning",
            ["-p:NoWarn=MSBuildTask0001"], "warning", 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.Exit(1)", "dotnet_diagnostic.MSBuildTask0001.severity = warning",
            ["-p:WarningsAsErrors=MSBuildTask0001"], "warning", 1);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.GetEnvironmentVariable(\"KEY\")", "",
            ["-p:NoWarn=MSBuildTask0005"], null, 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.Exit(1)", "dotnet_diagnostic.MSBuildTask0005.severity = none",
            [], null, 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.Exit(1)", "dotnet_diagnostic.MSBuildTask0005.severity = error",
            [], "error", 1);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Reflection.Assembly.Load(\"Unsafe\")", "dotnet_analyzer_diagnostic.severity = warning",
            [], "warning", 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Reflection.Assembly.Load(\"Unsafe\")",
            """
            dotnet_analyzer_diagnostic.severity = error
            dotnet_analyzer_diagnostic.category-MSBuild.TaskAuthoring.severity = warning
            """,
            [], "warning", 0);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Reflection.Assembly.Load(\"Unsafe\")",
            """
            dotnet_analyzer_diagnostic.severity = warning
            dotnet_diagnostic.MSBuildTask0004.severity = suggestion
            """,
            [], "warning", 0);

        await File.WriteAllTextAsync(Path.Combine(consumerDirectory, ".globalconfig"), """
            is_global = true
            dotnet_diagnostic.MSBuildTask0005.severity = error
            """);
        await VerifyTransitiveSeverity(repositoryRoot, consumerDirectory,
            "System.Environment.Exit(1)", "dotnet_diagnostic.MSBuildTask0005.severity = default",
            [], "warning", 0);
    }

    private async Task<string> VerifyTransitiveSeverity(
        string repositoryRoot,
        string consumerDirectory,
        string unsafeCall,
        string configuration,
        string[] compilerArguments,
        string? expectedSeverity,
        int expectedExitCode)
    {
        await File.WriteAllTextAsync(Path.Combine(consumerDirectory, "Task.cs"), $$"""
            namespace Microsoft.Build.Framework
            {
                public interface ITask { bool Execute(); }
                public interface IMultiThreadableTask : ITask { }
            }
            public class Task : Microsoft.Build.Framework.IMultiThreadableTask
            {
                public bool Execute()
                {
                    {{unsafeCall}};
                    Helper.Run();
                    return true;
                }
            }
            public static class Helper
            {
                public static void Run() => {{unsafeCall}};
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(consumerDirectory, ".editorconfig"), $$"""
            root = true

            [*.cs]
            {{configuration}}
            """);
        string output = await RunDotNet(repositoryRoot, consumerDirectory, expectedExitCode,
            ["build", "Consumer.csproj", "--nologo", "--disable-build-servers", "--no-restore", "-t:Rebuild", .. compilerArguments]);

        if (expectedSeverity is null)
        {
            output.ShouldNotContain("warning MSBuildTask0005");
            output.ShouldNotContain("error MSBuildTask0005");
        }
        else
        {
            output.ShouldContain($"{expectedSeverity} MSBuildTask0005");
        }

        return output;
    }

    private async Task PackProject(
        string repositoryRoot,
        string projectPath,
        string packageOutput,
        bool metadataOnly = false)
    {
        if (metadataOnly)
        {
            await RunDotNet(
                repositoryRoot,
                repositoryRoot,
                "restore",
                projectPath,
                "--nologo",
                "--disable-build-servers");
        }

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

    private Task<string> RunDotNet(
        string repositoryRoot,
        string workingDirectory,
        params string[] arguments) => RunDotNet(repositoryRoot, workingDirectory, 0, arguments);

    private async Task<string> RunDotNet(
        string repositoryRoot,
        string workingDirectory,
        int expectedExitCode,
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

        // The two-stage CI build launches tests with paths from the stage 1 bootstrap.
        // Let this dotnet invocation recompute its MSBuild paths from the repository-local SDK.
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBuildExtensionsPath");
        startInfo.Environment.Remove("MSBuildExtensionsPath32");
        startInfo.Environment.Remove("MSBuildExtensionsPath64");
        startInfo.Environment.Remove("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR");
        startInfo.Environment.Remove("DOTNET_ROOT_X64");
        startInfo.Environment.Remove("DOTNET_ROOT_X86");
        startInfo.Environment.Remove("DOTNET_ROOT_ARM64");
        startInfo.Environment.Remove("DOTNET_ROOT_ARM");

        startInfo.Environment["DOTNET_HOST_PATH"] = dotnetPath;
        startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
        startInfo.Environment[$"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}"] = dotnetRoot;
        startInfo.Environment["DOTNET_INSTALL_DIR"] = dotnetRoot;
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

        process.ExitCode.ShouldBe(expectedExitCode, $"dotnet {string.Join(' ', arguments)} returned an unexpected exit code.");
        return $"{output}{Environment.NewLine}{error}";
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
