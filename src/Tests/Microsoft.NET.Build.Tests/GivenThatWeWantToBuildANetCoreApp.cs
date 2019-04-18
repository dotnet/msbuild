// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Tasks;
using NuGet.Versioning;

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
        [InlineData("netcoreapp2.0", null, "2.0.0", "2.0.0")]
        [InlineData("netcoreapp2.1", null, "2.1.0", "2.1.0")]
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
        [InlineData("netcoreapp1.0", false, true, "1.0.5")]
        [InlineData("netcoreapp1.0", true, true, "1.0.15")]
        [InlineData("netcoreapp1.0", false, false, "1.0.5")]
        [InlineData("netcoreapp1.1", false, true, "1.1.2")]
        [InlineData("netcoreapp1.1", true, true, "1.1.12")]
        [InlineData("netcoreapp1.1", false, false, "1.1.2")]
        [InlineData("netcoreapp2.0", false, true, "2.0.0")]
        [InlineData("netcoreapp2.0", true, true, TestContext.LatestRuntimePatchForNetCoreApp2_0)]
        [InlineData("netcoreapp2.0", false, false, "2.0.0")]
        public void It_targets_the_right_framework_depending_on_output_type(string targetFramework, bool selfContained, bool isExe, string expectedFrameworkVersion)
        {
            string testIdentifier = "Framework_targeting_" + targetFramework + "_" + (isExe ? "App_" : "Lib_") + (selfContained ? "SelfContained" : "FrameworkDependent");

            It_targets_the_right_framework(testIdentifier, targetFramework, null, selfContained, isExe, expectedFrameworkVersion, expectedFrameworkVersion);
        }

        [Fact]
        public void The_RuntimeFrameworkVersion_can_float()
        {
            var testProject = new TestProject()
            {
                Name = "RuntimeFrameworkVersionFloat",
                TargetFrameworks = "netcoreapp2.0",
                RuntimeFrameworkVersion = "2.0.*",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(testProject.TargetFrameworks), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");

            //  Test that the resolved version is greater than or equal to the latest runtime patch
            //  we know about, so that when a new runtime patch is released the test doesn't
            //  immediately start failing
            var minimumExpectedVersion = new NuGetVersion(TestContext.LatestRuntimePatchForNetCoreApp2_0);
            netCoreAppLibrary.Version.CompareTo(minimumExpectedVersion).Should().BeGreaterOrEqualTo(0,
                "the version resolved from a RuntimeFrameworkVersion of '{0}' should be at least {1}",
                testProject.RuntimeFrameworkVersion, TestContext.LatestRuntimePatchForNetCoreApp2_0);
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

            NuGetConfigWriter.Write(testAsset.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(extraArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            if (isExe)
            {
                //  Self-contained apps don't write a framework version to the runtimeconfig, so only check this for framework-dependent apps
                if (!selfContained)
                {
                    string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
                    string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
                    JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

                    string actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
                    actualRuntimeFrameworkVersion.Should().Be(expectedRuntimeVersion);
                }

                var runtimeconfigDevFileName = testProject.Name + ".runtimeconfig.dev.json";
                outputDirectory.Should()
                        .HaveFile(runtimeconfigDevFileName);

                string devruntimeConfigContents = File.ReadAllText(Path.Combine(outputDirectory.FullName, runtimeconfigDevFileName));
                JObject devruntimeConfig = JObject.Parse(devruntimeConfigContents);

                var additionalProbingPaths = ((JArray)devruntimeConfig["runtimeOptions"]["additionalProbingPaths"]).Values<string>();
                // can't use Path.Combine on segments with an illegal `|` character
                var expectedPath = $"{Path.Combine(FileConstants.UserProfileFolder, ".dotnet", "store")}{Path.DirectorySeparatorChar}|arch|{Path.DirectorySeparatorChar}|tfm|";
                additionalProbingPaths.Should().Contain(expectedPath);
            }

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netCoreAppLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_handles_mismatched_implicit_package_versions(bool allowMismatch)
        {
            var testProject = new TestProject()
            {
                Name = "MismatchFrameworkTest",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true,
            };
            if (allowMismatch)
            {
                testProject.AdditionalProperties["VerifyMatchingImplicitPackageVersion"] = "false";
            }

            string runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.AdditionalProperties["RuntimeIdentifiers"] = runtimeIdentifier;            

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var result = buildCommand.Execute($"/p:RuntimeIdentifier={runtimeIdentifier}");

            if (allowMismatch)
            {
                result.Should().Pass();
            }
            else
            {

                result.Should().Fail();

                //  Get everything after the {2} in the failure message so this test doesn't need to
                //  depend on the exact version the app would be rolled forward to
                string expectedFailureMessage = Strings.MismatchedPlatformPackageVersion
                    .Substring(Strings.MismatchedPlatformPackageVersion.IndexOf("{2}") + 3);

                result.Should().HaveStdOutContaining(expectedFailureMessage);
            }
        }

        [Fact]
        public void It_restores_only_ridless_tfm()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "netcoreapp1.1", "TargetDefinitions", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "RunResolvePackageDependencies",
            };

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

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp3.0")]
        public void It_runs_the_app_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolder_" + targetFramework, false, false, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp3.0")]
        public void It_runs_a_rid_specific_app_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolderWithRID_" + targetFramework, true, false, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp3.0")]
        public void It_runs_the_app_with_conflicts_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolderConflicts_" + targetFramework, false, true, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp3.0")]
        public void It_runs_a_rid_specific_app_with_conflicts_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolderWithRIDConflicts_" + targetFramework, true, true, targetFramework);
        }

        private void RunAppFromOutputFolder(string testName, bool useRid, bool includeConflicts,
            string targetFramework = "netcoreapp2.0")
        {
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

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(outputFolder, project.Name + ".dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(outputMessage);

        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp3.0")]
        public void It_trims_conflicts_from_the_deps_file(string targetFramework)
        {
            TestProject project = new TestProject()
            {
                Name = "NetCore2App",
                TargetFrameworks = targetFramework,
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

            var testAsset = _testAssetsManager.CreateTestProject(project, identifier: targetFramework)
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
        public void There_are_no_conflicts_when_targeting_netcoreapp_1_1()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp1.1_Conflicts",
                TargetFrameworks = "netcoreapp1.1",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_publishes_package_satellites_correctly(bool crossTarget)
        {
            var testProject = new TestProject()
            {
                Name = "AppUsingPackageWithSatellites",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            if (crossTarget)
            {
                testProject.Name += "_cross";
            }

            testProject.PackageReferences.Add(new TestPackageReference("Humanizer.Core.fr", "2.2.0"));
            testProject.PackageReferences.Add(new TestPackageReference("Humanizer.Core.pt", "2.2.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    if (crossTarget)
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Element(ns + "TargetFramework").Name += "s";
                    }
                })
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute("/v:normal", $"/p:TargetFramework={testProject.TargetFrameworks}")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                ;

            var outputDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().NotHaveFile("Humanizer.resources.dll");
            outputDirectory.Should().HaveFile(Path.Combine("fr", "Humanizer.resources.dll"));
        }

        [Fact]
        public void It_uses_lowercase_form_of_the_target_framework_for_the_output_path()
        {
            var testProject = new TestProject()
            {
                Name = "OutputPathCasing",
                TargetFrameworks = "ignored",
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

        [Fact]
        public void BuildWithTransitiveReferenceToNetCoreAppPackage()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreAppPackageReference",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            var referencedProject = new TestProject()
            {
                Name = "NetStandardProject",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                IsExe = false
            };

            //  The SharpDX package depends on the Microsoft.NETCore.App package
            referencedProject.PackageReferences.Add(new TestPackageReference("SharpDX", "4.0.1"));

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_escapes_resolved_package_assets_paths()
        {
            var testProject = new TestProject()
            {
                Name = "ProjectWithPackageThatNeedsEscapes",
                TargetFrameworks = "net462",
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.SourceFiles["ExampleReader.cs"] = @"
using System;
using System.Threading.Tasks;

namespace ContentFilesExample
{
    internal static class ExampleInternals
    {
        internal static Task<string> GetFileText(string fileName)
        {
            throw new NotImplementedException();
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello World!"");
    }
}";

            // ContentFilesExample is an existing package that demonstrates the problem.
            // It contains assets with paths that have '%2B', which MSBuild will unescape to '+'.
            // Without the change to escape the asset paths, the asset will not be found inside the package.
            testProject.PackageReferences.Add(new TestPackageReference("ContentFilesExample", "1.0.2"));

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
