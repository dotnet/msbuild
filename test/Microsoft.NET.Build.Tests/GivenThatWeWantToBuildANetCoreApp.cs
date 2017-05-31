using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreApp : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        //  TargetFramework, RuntimeFrameworkVersion, ExpectedPackageVersion, ExpectedRuntimeFrameworkVersion
        [InlineData("netcoreapp1.0", null, "1.0.5", "1.0.5")]
        [InlineData("netcoreapp1.0", "1.0.0", "1.0.0", "1.0.0")]
        [InlineData("netcoreapp1.0", "1.0.3", "1.0.3", "1.0.3")]
        [InlineData("netcoreapp1.1", null, "1.1.2", "1.1.2")]
        [InlineData("netcoreapp1.1", "1.1.0", "1.1.0", "1.1.0")]
        [InlineData("netcoreapp1.1.1", null, "1.1.1", "1.1.1")]
        public void It_targets_the_right_shared_framework(string targetFramework, string runtimeFrameworkVersion,
            string expectedPackageVersion, string expectedRuntimeVersion)
        {
            string testIdentifier = "SharedFrameworkTargeting_" + string.Join("_", targetFramework, runtimeFrameworkVersion ?? "null");

            It_targets_the_right_framework(testIdentifier, targetFramework, runtimeFrameworkVersion,
                selfContained: false, isExe: true,
                expectedPackageVersion: expectedPackageVersion, expectedRuntimeVersion: expectedRuntimeVersion);
        }

        //  Test behavior when implicit version differs for framework-dependent and self-contained apps
        [Theory]
        [InlineData(false, true, "1.1.1")]
        [InlineData(true, true, "1.1.2")]
        [InlineData(false, false, "1.1.1")]
        public void It_targets_the_right_framework_depending_on_output_type(bool selfContained, bool isExe, string expectedFrameworkVersion)
        {
            string testIdentifier = "Framework_targeting_" + (isExe ? "App_" : "Lib_") + (selfContained ? "SelfContained" : "FrameworkDependent");

            It_targets_the_right_framework(testIdentifier, "netcoreapp1.1", null, selfContained, isExe, expectedFrameworkVersion, expectedFrameworkVersion,
                "/p:ImplicitRuntimeFrameworkVersionForFrameworkDependentNetCoreApp1_1=1.1.1");
        }

        private void It_targets_the_right_framework(
            string testIdentifier,
            string targetFramework,
            string runtimeFrameworkVersion,
            bool selfContained,
            bool isExe,
            string expectedPackageVersion,
            string expectedRuntimeVersion,
            string extraMSBuildArguments = null)
        {
            string runtimeIdentifier = null;
            if (selfContained)
            {
                runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            }

            var testProject = new TestProject()
            {
                Name = "FrameworkTargetTest",
                TargetFrameworks = targetFramework,
                RuntimeFrameworkVersion = runtimeFrameworkVersion,
                IsSdkProject = true,
                IsExe = isExe,
                RuntimeIdentifier = runtimeIdentifier
            };            

            var extraArgs = extraMSBuildArguments?.Split(' ') ?? Array.Empty<string>();

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testIdentifier)
                .Restore(Log, testProject.Name, extraArgs);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(extraArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            //  Self-contained apps don't write a framework version to the runtimeconfig, so only check this for framework-dependent apps
            if (isExe && !selfContained)
            {
                string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
                string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
                JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

                string actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
                actualRuntimeFrameworkVersion.Should().Be(expectedRuntimeVersion);
            }

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netCoreAppLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }

        [Fact]
        public void It_restores_only_ridless_tfm()
        {
            //  Disable this test when using full Framework MSBuild, until MSBuild is updated 
            //  to provide conditions in NuGet ImportBefore/ImportAfter props/targets
            //  https://github.com/dotnet/sdk/issues/874
            if (UsingFullFrameworkMSBuild)
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "netcoreapp1.1", "TargetDefinitions", GetValuesCommand.ValueType.Item);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            // When RuntimeIdentifier is not specified, the assets file
            // should only contain one target with no RIDs
            var targetDefs = getValuesCommand.GetValues();
            targetDefs.Count.Should().Be(1);
            targetDefs.Should().Contain(".NETCoreApp,Version=v1.1");
        }

        [Fact]
        public void It_runs_the_app_from_the_output_folder()
        {
            RunAppFromOutputFolder("RunFromOutputFolder", false, false);
        }

        [Fact]
        public void It_runs_a_rid_specific_app_from_the_output_folder()
        {
            RunAppFromOutputFolder("RunFromOutputFolderWithRID", true, false);
        }

        [Fact]
        public void It_runs_the_app_with_conflicts_from_the_output_folder()
        {
            RunAppFromOutputFolder("RunFromOutputFolderConflicts", false, true);
        }

        [Fact]
        public void It_runs_a_rid_specific_app_with_conflicts_from_the_output_folder()
        {
            RunAppFromOutputFolder("RunFromOutputFolderWithRIDConflicts", true, true);
        }

        private void RunAppFromOutputFolder(string testName, bool useRid, bool includeConflicts)
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
                //  See https://github.com/dotnet/sdk/issues/1077
                return;
            }

            var targetFramework = "netcoreapp2.0";
            var runtimeIdentifier = useRid ? EnvironmentInfo.GetCompatibleRid(targetFramework) : null;

            TestProject project = new TestProject()
            {
                Name = testName,
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };

            string outputMessage = $"Hello from {project.Name}!";

            project.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        TestConflictResolution();
        Console.WriteLine(""" + outputMessage + @""");
    }
" + ConflictResolutionAssets.ConflictResolutionTestMethod + @"
}
";
            var testAsset = _testAssetsManager.CreateTestProject(project, project.Name)
                .WithProjectChanges(p =>
                {
                    if (includeConflicts)
                    {
                        var ns = p.Root.Name.Namespace;

                        var itemGroup = new XElement(ns + "ItemGroup");
                        p.Root.Add(itemGroup);

                        foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                        {
                            itemGroup.Add(new XElement(ns + "PackageReference",
                                new XAttribute("Include", dependency.Item1),
                                new XAttribute("Version", dependency.Item2)));
                        }
                    }
                })
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(project.TargetFrameworks, runtimeIdentifier: runtimeIdentifier ?? "").FullName;

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(outputFolder, project.Name + ".dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(outputMessage);

        }

        [Fact]
        public void It_trims_conflicts_from_the_deps_file()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
                //  See https://github.com/dotnet/sdk/issues/1077
                return;
            }

            TestProject project = new TestProject()
            {
                Name = "NetCore2App",
                TargetFrameworks = "netcoreapp2.0",
                IsExe = true,
                IsSdkProject = true
            };

            project.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        TestConflictResolution();
        Console.WriteLine(""Hello, World!"");
    }
" + ConflictResolutionAssets.ConflictResolutionTestMethod + @"
}
";

            var testAsset = _testAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                })
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(project.TargetFrameworks).FullName;

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputFolder, $"{project.Name}.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext.Should()
                    .OnlyHaveRuntimeAssemblies("", project.Name)
                    .And
                    .HaveNoDuplicateRuntimeAssemblies("")
                    .And
                    .HaveNoDuplicateNativeAssets(""); ;
            }
        }

        [Fact]
        public void It_uses_lowercase_form_of_the_target_framework_for_the_output_path()
        {
            var testProject = new TestProject()
            {
                Name = "OutputPathCasing",
                TargetFrameworks = "igored",
                IsSdkProject = true,
                IsExe = true
            };

            string[] extraArgs = new[] { "/p:TargetFramework=NETCOREAPP1.1" };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name, extraArgs);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(extraArgs)
                .Should()
                .Pass();

            string outputFolderWithConfiguration = Path.Combine(buildCommand.ProjectRootPath, "bin", "Debug");

            Directory.GetDirectories(outputFolderWithConfiguration)
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo("netcoreapp1.1");

            string intermediateFolderWithConfiguration = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "Debug");

            Directory.GetDirectories(intermediateFolderWithConfiguration)
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo("netcoreapp1.1");
        }
    }
}
