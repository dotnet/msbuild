// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.EndToEndTests;

[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class TaskAnalyzerTestPathsAttribute(string analyzerPath) : Attribute
{
    public string AnalyzerPath { get; } = analyzerPath;
}

public sealed class TaskAnalyzerConfiguration_Tests : IDisposable
{
    private const string FrameworkPackagePrefix = "Microsoft.Build.Framework.";
    private const string PackageExtension = ".nupkg";
    private const int ProcessTimeoutMilliseconds = 180_000;

    private readonly TestEnvironment _env;
    private readonly ITestOutputHelper _output;
    private readonly string _analyzerPath;

    public TaskAnalyzerConfiguration_Tests(ITestOutputHelper output)
    {
        _output = output;
        _env = TestEnvironment.Create(output);
        _analyzerPath = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<TaskAnalyzerTestPathsAttribute>()
            .ShouldNotBeNull()
            .AnalyzerPath;
    }

    public void Dispose() => _env.Dispose();

    [Theory]
    [InlineData(null, null, false, true)]
    [InlineData("true", "all", true, true)]
    [InlineData("false", "all", false, false)]
    [InlineData("invalid", "invalid", false, true)]
    public void ProjectPropertiesControlAnalyzer(
        string? enabled,
        string? scope,
        bool expectRegularTaskDiagnostic,
        bool expectMtTaskDiagnostic)
    {
        TestProject project = CreateTestProject(enabled, scope);

        string output = Build(project.ProjectPath, "/restore", out bool succeeded);

        succeeded.ShouldBeTrue(output);
        AssertTaskDiagnostic(output, "RegularTask.cs", expectRegularTaskDiagnostic);
        AssertTaskDiagnostic(output, "MtTask.cs", expectMtTaskDiagnostic);
        output.ShouldNotContain("SafeTask.cs(");
    }

    [Fact]
    public void GlobalConfigEnablesMigrationMode()
    {
        TestProject project = CreateTestProject();
        File.WriteAllText(
            Path.Combine(project.Directory, ".globalconfig"),
            """
            is_global = true
            msbuild_task_analyzer.scope = all
            """);

        string output = Build(project.ProjectPath, "/restore", out bool succeeded);

        succeeded.ShouldBeTrue(output);
        AssertTaskDiagnostic(output, "RegularTask.cs", expected: true);
        AssertTaskDiagnostic(output, "MtTask.cs", expected: true);
    }

    [Fact]
    public void DiagnosticIdCanBeSuppressed()
    {
        TestProject project = CreateTestProject(scope: "all");

        string output = Build(project.ProjectPath, "/restore /p:NoWarn=MSBuildTask0002", out bool succeeded);

        succeeded.ShouldBeTrue(output);
        output.ShouldNotContain("MSBuildTask0002");
    }

    [Fact]
    public void EditorConfigCanSuppressDiagnosticId()
    {
        TestProject project = CreateTestProject(scope: "all");
        File.WriteAllText(
            Path.Combine(project.Directory, ".editorconfig"),
            """
            root = true

            [*.cs]
            dotnet_diagnostic.MSBuildTask0002.severity = none
            """);

        string output = Build(project.ProjectPath, "/restore", out bool succeeded);

        succeeded.ShouldBeTrue(output);
        output.ShouldNotContain("MSBuildTask0002");
    }

    [Fact]
    public void TreatWarningsAsErrorsAppliesToAnalyzerWarnings()
    {
        TestProject project = CreateTestProject();

        string output = Build(project.ProjectPath, "/restore /p:TreatWarningsAsErrors=true", out bool succeeded);

        succeeded.ShouldBeFalse();
        output.ShouldContain("error MSBuildTask0002");
        output.ShouldContain("MtTask.cs");
        output.ShouldNotContain("RegularTask.cs");
    }

    [Fact]
    public void ConfigurationWorksForSolutionBuild()
    {
        TestProject project = CreateTestProject(scope: "all");
        string solutionPath = Path.Combine(project.Directory, "TaskAnalyzer.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="TaskProject.csproj" />
            </Solution>
            """);

        string output = Build(solutionPath, "/restore", out bool succeeded);

        succeeded.ShouldBeTrue(output);
        AssertTaskDiagnostic(output, "RegularTask.cs", expected: true);
        AssertTaskDiagnostic(output, "MtTask.cs", expected: true);
    }

    private TestProject CreateTestProject(string? enabled = null, string? scope = null)
    {
        string frameworkPackagePath = GetFrameworkPackagePath();
        string frameworkVersion = GetPackageVersion(frameworkPackagePath);
        TransientTestFolder projectFolder = _env.CreateFolder(createFolder: true);
        string projectPath = Path.Combine(projectFolder.Path, "TaskProject.csproj");

        File.WriteAllText(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{RunnerUtilities.LatestDotNetCoreForMSBuild}}</TargetFramework>
                <Nullable>disable</Nullable>
                <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
                {{CreateProperty("MSBuildTaskAnalyzerEnabled", enabled)}}
                {{CreateProperty("MSBuildTaskAnalyzerScope", scope)}}
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.Build.Framework" Version="{{frameworkVersion}}" />
                <Analyzer Include="{{_analyzerPath}}" />
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

        File.WriteAllText(
            Path.Combine(projectFolder.Path, "SafeTask.cs"),
            """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.Build.Framework;

            public sealed class SafeTask : TaskBase, IMultiThreadableTask
            {
                public SafeTask(TaskEnvironment taskEnvironment)
                {
                    TaskEnvironment = taskEnvironment;
                }

                public TaskEnvironment TaskEnvironment { get; set; }
                public string Source { get; set; }
                public string Destination { get; set; }
                public string ToolPath { get; set; }
                public string Arguments { get; set; }

                public override bool Execute()
                {
                    string setting = TaskEnvironment.GetEnvironmentVariable("MY_SETTING");
                    TaskEnvironment.SetEnvironmentVariable("MY_RESULT", setting);

                    AbsolutePath source = TaskEnvironment.GetAbsolutePath(Source);
                    AbsolutePath destination = TaskEnvironment.GetAbsolutePath(Destination);
                    if (File.Exists(source))
                    {
                        File.Copy(source, destination);
                    }

                    ProcessStartInfo startInfo = TaskEnvironment.GetProcessStartInfo();
                    startInfo.FileName = ToolPath;
                    startInfo.Arguments = Arguments;
                    using Process process = Process.Start(startInfo);
                    process?.WaitForExit();
                    return true;
                }
            }
            """);

        CreateNuGetConfig(projectFolder.Path);
        return new TestProject(projectFolder.Path, projectPath);
    }

    private void CreateNuGetConfig(string projectDirectory)
    {
        string sourceConfigPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TaskAnalyzerConfiguration", "NuGet.Config");
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

        config.Save(Path.Combine(projectDirectory, "NuGet.Config"));
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

    private string Build(string projectOrSolutionPath, string additionalArguments, out bool succeeded)
    {
        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{projectOrSolutionPath}\" /nologo /verbosity:minimal /nodeReuse:false {additionalArguments}",
            out succeeded,
            outputHelper: _output,
            timeoutMilliseconds: ProcessTimeoutMilliseconds);

        _output.WriteLine(output);
        return output;
    }

    private static void AssertTaskDiagnostic(string output, string sourceFile, bool expected)
    {
        string diagnostic = $"{sourceFile}(";
        if (expected)
        {
            output.ShouldContain(diagnostic);
        }
        else
        {
            output.ShouldNotContain(diagnostic);
        }
    }

    private static string CreateProperty(string name, string? value) =>
        value is null ? string.Empty : $"<{name}>{value}</{name}>";

    private readonly record struct TestProject(string Directory, string ProjectPath);
}
