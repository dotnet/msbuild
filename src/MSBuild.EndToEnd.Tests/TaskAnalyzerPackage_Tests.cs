// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.EndToEndTests;

public sealed class TaskAnalyzerPackage_Tests : IDisposable
{
    private const string AnalyzerPackagePath = "analyzers/dotnet/cs/Microsoft.Build.TaskAuthoring.Analyzer.dll";
    private const string FrameworkPackagePrefix = "Microsoft.Build.Framework.";
    private const string PackageExtension = ".nupkg";
    private const int ProcessTimeoutMilliseconds = 180_000;

    private readonly TestEnvironment _env;
    private readonly ITestOutputHelper _output;

    public TaskAnalyzerPackage_Tests(ITestOutputHelper output)
    {
        _output = output;
        _env = TestEnvironment.Create(output);
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public void FrameworkPackageContainsTaskAnalyzerWithoutRoslynDependencies()
    {
        string packagePath = GetFrameworkPackagePath();

        using ZipArchive package = new ZipArchive(File.OpenRead(packagePath), ZipArchiveMode.Read);
        package.GetEntry(AnalyzerPackagePath).ShouldNotBeNull();

        package.Entries
            .Where(entry => entry.FullName.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .ShouldBe([AnalyzerPackagePath], ignoreOrder: true);

        ZipArchiveEntry nuspecEntry = package.GetEntry("Microsoft.Build.Framework.nuspec").ShouldNotBeNull();
        using Stream nuspecStream = nuspecEntry.Open();
        XDocument nuspec = XDocument.Load(nuspecStream);

        nuspec
            .Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => (string?)element.Attribute("id"))
            .Where(packageId => packageId is not null && packageId.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty();
    }

    [Fact]
    public void FrameworkPackageActivatesTaskAnalyzer()
    {
        string packagePath = GetFrameworkPackagePath();
        string packageVersion = GetPackageVersion(packagePath);
        TransientTestFolder projectFolder = _env.CreateFolder(createFolder: true);
        string projectPath = Path.Combine(projectFolder.Path, "TaskProject.csproj");
        string globalConfigPath = Path.Combine(projectFolder.Path, ".globalconfig");

        File.WriteAllText(
            projectPath,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                  <TargetFramework>{{RunnerUtilities.LatestDotNetCoreForMSBuild}}</TargetFramework>
                  <Nullable>disable</Nullable>
                </PropertyGroup>
                <ItemGroup>
                  <PackageReference Include="Microsoft.Build.Framework" Version="{{packageVersion}}" />
                </ItemGroup>
              </Project>
              """);

        File.WriteAllText(
            Path.Combine(projectFolder.Path, "TaskBase.cs"),
            """
            using Microsoft.Build.Framework;

            public abstract class TaskBase : ITask
            {
                public IBuildEngine BuildEngine { get; set; }
                public ITaskHost HostObject { get; set; }
                public abstract bool Execute();
            }
            """);

        File.WriteAllText(
            Path.Combine(projectFolder.Path, "RegularTask.cs"),
            """
            using System;

            public sealed class RegularTask : TaskBase
            {
                public override bool Execute()
                {
                    _ = Environment.CurrentDirectory;
                    return true;
                }
            }
            """);

        File.WriteAllText(
            Path.Combine(projectFolder.Path, "MtTask.cs"),
            """
            using System;
            using Microsoft.Build.Framework;

            public sealed class MtTask : TaskBase, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }

                public override bool Execute()
                {
                    _ = Environment.CurrentDirectory;
                    return true;
                }
            }
            """);

        string nugetConfigPath = CreateNuGetConfig(projectFolder.Path);
        string commonArguments = $"\"{projectPath}\" /nologo /verbosity:minimal";
        string restoreArguments = $"/restore /p:RestoreConfigFile=\"{nugetConfigPath}\"";

#if FEATURE_RUN_EXE_IN_TESTS
        string defaultOutput = RunMSBuild($"{commonArguments} {restoreArguments}", out bool defaultBuildSucceeded);
#else
        string defaultOutput = RunDotNetBuild(
            $"\"{projectPath}\" --nologo --verbosity:minimal {restoreArguments}",
            out bool defaultBuildSucceeded);
#endif
        defaultBuildSucceeded.ShouldBeTrue(defaultOutput);
        defaultOutput.ShouldContain("warning MSBuildTask0002");
        defaultOutput.ShouldContain("MtTask.cs");
        defaultOutput.ShouldNotContain("RegularTask.cs");

        File.WriteAllText(
            globalConfigPath,
            """
            is_global = true
            msbuild_task_analyzer.scope = all
            """);

        string migrationOutput = RunMSBuild($"{commonArguments} /t:Rebuild", out bool migrationBuildSucceeded);
        migrationBuildSucceeded.ShouldBeTrue(migrationOutput);
        migrationOutput.ShouldContain("warning MSBuildTask0002");
        migrationOutput.ShouldContain("MtTask.cs");
        migrationOutput.ShouldContain("RegularTask.cs");

        string suppressedOutput = RunMSBuild(
            $"{commonArguments} /t:Rebuild /p:NoWarn=MSBuildTask0002",
            out bool suppressedBuildSucceeded);
        suppressedBuildSucceeded.ShouldBeTrue(suppressedOutput);
        suppressedOutput.ShouldNotContain("MSBuildTask0002");

        File.Delete(globalConfigPath);

        string warningsAsErrorsOutput = RunMSBuild(
            $"{commonArguments} /t:Rebuild /p:TreatWarningsAsErrors=true",
            out bool warningsAsErrorsBuildSucceeded);
        warningsAsErrorsBuildSucceeded.ShouldBeFalse();
        warningsAsErrorsOutput.ShouldContain("error MSBuildTask0002");
        warningsAsErrorsOutput.ShouldContain("MtTask.cs");
        warningsAsErrorsOutput.ShouldNotContain("RegularTask.cs");

        string disabledOutput = RunMSBuild(
            $"{commonArguments} /t:Rebuild /p:RunAnalyzers=false",
            out bool disabledBuildSucceeded);
        disabledBuildSucceeded.ShouldBeTrue(disabledOutput);
        disabledOutput.ShouldNotContain("MSBuildTask0002");
    }

    private string CreateNuGetConfig(string projectDirectory)
    {
        string sourceConfigPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TaskAnalyzerPackage", "NuGet.Config");
        string destinationConfigPath = Path.Combine(projectDirectory, "NuGet.Config");
        XDocument config = XDocument.Load(sourceConfigPath);

        XElement packageSources = config.Root!.Element("packageSources").ShouldNotBeNull();
        packageSources.Add(
            new XElement(
                "add",
                new XAttribute("key", "local-msbuild"),
                new XAttribute("value", RunnerUtilities.ArtifactsLocationAttribute.ArtifactsLocation)));

        XElement packageSourceMapping = config.Root.Element("packageSourceMapping").ShouldNotBeNull();
        packageSourceMapping.Add(
            new XElement(
                "packageSource",
                new XAttribute("key", "local-msbuild"),
                new XElement("package", new XAttribute("pattern", "Microsoft.Build.*")),
                new XElement("package", new XAttribute("pattern", "Microsoft.NET.StringTools"))));

        config.Save(destinationConfigPath);
        return destinationConfigPath;
    }

    private string GetFrameworkPackagePath()
    {
        string[] packages = Directory.GetFiles(
            RunnerUtilities.ArtifactsLocationAttribute.ArtifactsLocation,
            $"{FrameworkPackagePrefix}*{PackageExtension}",
            SearchOption.TopDirectoryOnly);

        packages.ShouldNotBeEmpty();
        return packages.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static string GetPackageVersion(string packagePath)
    {
        string packageFileName = Path.GetFileName(packagePath);
        return packageFileName.Substring(
            FrameworkPackagePrefix.Length,
            packageFileName.Length - FrameworkPackagePrefix.Length - PackageExtension.Length);
    }

#if !FEATURE_RUN_EXE_IN_TESTS
    private string RunDotNetBuild(string arguments, out bool succeeded)
    {
        string output = RunnerUtilities.RunProcessAndGetOutput(
            RunnerUtilities.BootstrapDotnetHostPath,
            $"build {arguments}",
            out succeeded,
            outputHelper: _output,
            timeoutMilliseconds: ProcessTimeoutMilliseconds,
            environmentVariables: RunnerUtilities.GetBootstrapMSBuildEnvironmentVariables());

        _output.WriteLine(output);
        return output;
    }
#endif

    private string RunMSBuild(string arguments, out bool succeeded)
    {
        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            arguments,
            out succeeded,
            outputHelper: _output,
            timeoutMilliseconds: ProcessTimeoutMilliseconds);

        _output.WriteLine(output);
        return output;
    }
}
