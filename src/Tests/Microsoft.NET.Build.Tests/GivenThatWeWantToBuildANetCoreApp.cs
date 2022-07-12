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
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreApp : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreApp(ITestOutputHelper log) : base(log)
        {
        }

        private BuildCommand GetBuildCommand([CallerMemberName] string callingMethod = "")
        {
            var testAsset = _testAssetsManager
               .CopyTestAsset("HelloWorldWithSubDirs", callingMethod)
               .WithSource();

            return new BuildCommand(testAsset);
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
        [InlineData("netcoreapp1.0", true, true, "1.0.16")]
        [InlineData("netcoreapp1.0", false, false, "1.0.5")]
        [InlineData("netcoreapp1.1", false, true, "1.1.2")]
        [InlineData("netcoreapp1.1", true, true, "1.1.13")]
        [InlineData("netcoreapp1.1", false, false, "1.1.2")]
        [InlineData("netcoreapp2.0", false, true, "2.0.0")]
        [InlineData("netcoreapp2.0", true, true, TestContext.LatestRuntimePatchForNetCoreApp2_0)]
        [InlineData("netcoreapp2.0", false, false, "2.0.0")]
        public void It_targets_the_right_framework_depending_on_output_type(string targetFramework, bool selfContained, bool isExe, string expectedFrameworkVersion)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

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
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

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
                IsExe = isExe,
                RuntimeIdentifier = runtimeIdentifier
            };

            var extraArgs = extraMSBuildArguments?.Split(' ') ?? Array.Empty<string>();

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testIdentifier);

            NuGetConfigWriter.Write(testAsset.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            var buildCommand = new BuildCommand(testAsset);

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
                IsExe = true,
            };

            if (!EnvironmentInfo.SupportsTargetFramework(testProject.TargetFrameworks))
            {
                return;
            }

            if (allowMismatch)
            {
                testProject.AdditionalProperties["VerifyMatchingImplicitPackageVersion"] = "false";
            }

            string runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.AdditionalProperties["RuntimeIdentifiers"] = runtimeIdentifier;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: allowMismatch.ToString())
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand.ExecuteWithoutRestore($"/p:RuntimeIdentifier={runtimeIdentifier}");

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
                .WithSource();

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                ToolsetInfo.CurrentTargetFramework, "TargetDefinitions", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "RunResolvePackageDependencies",
                Properties = { { "EmitLegacyAssetsFileItems", "true" } }
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            // When RuntimeIdentifier is not specified, the assets file
            // should only contain one target with no RIDs
            var targetDefs = getValuesCommand.GetValues();
            targetDefs.Count.Should().Be(1);
            targetDefs.Should().Contain(ToolsetInfo.CurrentTargetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp2.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_runs_the_app_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolder_" + targetFramework, false, false, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_runs_a_rid_specific_app_from_the_output_folder(string targetFramework)
        {
            RunAppFromOutputFolder("RunFromOutputFolderWithRID_" + targetFramework, true, false, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_runs_the_app_with_conflicts_from_the_output_folder(string targetFramework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            RunAppFromOutputFolder("RunFromOutputFolderConflicts_" + targetFramework, false, true, targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_runs_a_rid_specific_app_with_conflicts_from_the_output_folder(string targetFramework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            RunAppFromOutputFolder("RunFromOutputFolderWithRIDConflicts_" + targetFramework, true, true, targetFramework);
        }

        private void RunAppFromOutputFolder(string testName, bool useRid, bool includeConflicts,
            string targetFramework = "netcoreapp2.0")
        {
            var runtimeIdentifier = useRid ? EnvironmentInfo.GetCompatibleRid(targetFramework) : null;

            TestProject project = new TestProject()
            {
                Name = testName,
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
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(project.TargetFrameworks, runtimeIdentifier: runtimeIdentifier ?? "").FullName;

            new DotnetCommand(Log, Path.Combine(outputFolder, project.Name + ".dll"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(outputMessage);

        }

        [Theory]
        [InlineData("netcoreapp2.0", true)]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("net5.0", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void It_stops_generating_runtimeconfig_dev_json_after_net6(string targetFramework, bool shouldGenerateRuntimeConfigDevJson)
        {
            TestProject proj = new TestProject()
            {
                Name = "NetCoreApp",
                ProjectSdk = "Microsoft.NET.Sdk",
                IsExe = true,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };

            var buildCommand = new BuildCommand(_testAssetsManager.CreateTestProject(proj, identifier: targetFramework));

            var runtimeconfigFile = Path.Combine(
                buildCommand.GetOutputDirectory(targetFramework).FullName,
                $"{proj.Name}.runtimeconfig.dev.json");

            buildCommand.Execute().StdOut
                        .Should()
                        .NotContain("NETSDK1048");

            File.Exists(runtimeconfigFile).Should().Be(shouldGenerateRuntimeConfigDevJson);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_stops_generating_runtimeconfig_dev_json_after_net6_allow_property_override(string targetFramework)
        {
            TestProject proj = new TestProject()
            {
                Name = "NetCoreApp",
                ProjectSdk = "Microsoft.NET.Sdk",
                IsExe = true,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };

            var buildCommand = new BuildCommand(_testAssetsManager.CreateTestProject(proj, identifier: targetFramework));
            var runtimeconfigFile = Path.Combine(
                buildCommand.GetOutputDirectory(targetFramework).FullName,
                $"{proj.Name}.runtimeconfig.dev.json");

            // GenerateRuntimeConfigDevFile overrides default behavior
            buildCommand.Execute("/p:GenerateRuntimeConfigDevFile=true").StdOut
                        .Should()
                        .NotContain("NETSDK1048"); ;
            File.Exists(runtimeconfigFile).Should().BeTrue();

            buildCommand.Execute("/p:GenerateRuntimeConfigDevFile=false").StdOut
                        .Should()
                        .NotContain("NETSDK1048"); ;
            File.Exists(runtimeconfigFile).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_trims_conflicts_from_the_deps_file(string targetFramework)
        {
            TestProject project = new TestProject()
            {
                Name = "NetCore2App",
                TargetFrameworks = targetFramework,
                IsExe = true,
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

                });

            var buildCommand = new BuildCommand(testAsset);

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_generates_rid_fallback_graph(bool isSelfContained)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            TestProject project = new TestProject()
            {
                Name = "NetCore2App",
                TargetFrameworks = targetFramework,
                IsExe = true,
                RuntimeIdentifier = runtimeIdentifier
            };

            var testAsset = _testAssetsManager.CreateTestProject(project, identifier: isSelfContained.ToString());

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute($"/p:SelfContained={isSelfContained}")
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(project.TargetFrameworks, runtimeIdentifier: runtimeIdentifier).FullName;

            using var depsJsonFileStream = File.OpenRead(Path.Combine(outputFolder, $"{project.Name}.deps.json"));
            var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
            var runtimeFallbackGraph = dependencyContext.RuntimeGraph;
            if (isSelfContained)
            {
                runtimeFallbackGraph.Should().NotBeEmpty();
                runtimeFallbackGraph
                    .Any(runtimeFallback => !runtimeFallback.Runtime.Equals(runtimeIdentifier) && !runtimeFallback.Fallbacks.Contains(runtimeIdentifier))
                    .Should()
                    .BeFalse();
            }
            else
            {
                runtimeFallbackGraph.Should().BeEmpty();
            }
        }

        [Fact]
        public void There_are_no_conflicts_when_targeting_netcoreapp_1_1()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp1.1_Conflicts",
                TargetFrameworks = "netcoreapp1.1",
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

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
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
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
                });

            var publishCommand = new PublishCommand(testAsset);
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
                IsExe = true
            };

            string[] extraArgs = new[] { $"/p:TargetFramework={ToolsetInfo.CurrentTargetFramework.ToUpper()}" };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute(extraArgs)
                .Should()
                .Pass();

            string outputFolderWithConfiguration = Path.Combine(buildCommand.ProjectRootPath, "bin", "Debug");

            Directory.GetDirectories(outputFolderWithConfiguration)
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo(ToolsetInfo.CurrentTargetFramework);

            string intermediateFolderWithConfiguration = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "Debug");

            Directory.GetDirectories(intermediateFolderWithConfiguration)
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo(ToolsetInfo.CurrentTargetFramework);
        }

        [Fact]
        public void BuildWithTransitiveReferenceToNetCoreAppPackage()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreAppPackageReference",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var referencedProject = new TestProject()
            {
                Name = "NetStandardProject",
                TargetFrameworks = "netstandard2.0",
                IsExe = false
            };

            //  The SharpDX package depends on the Microsoft.NETCore.App package
            referencedProject.PackageReferences.Add(new TestPackageReference("SharpDX", "4.0.1"));

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

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
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/3044")]
        public void ReferenceLegacyContracts()
        {
            var testProject = new TestProject()
            {
                Name = "ReferencesLegacyContracts",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework)
            };

            //  Dependencies on contracts from different 1.x "bands" can cause downgrades when building
            //  with a RuntimeIdentifier.
            testProject.PackageReferences.Add(new TestPackageReference("System.IO.FileSystem", "4.0.1"));
            testProject.PackageReferences.Add(new TestPackageReference("System.Reflection", "4.3.0"));


            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void ItHasNoPackageReferences()
        {
            var testProject = new TestProject()
            {
                Name = "NoPackageReferences",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            string testDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

            var getPackageReferences = new GetValuesCommand(
               Log,
               testDirectory,
               testProject.TargetFrameworks,
               "PackageReference",
               GetValuesCommand.ValueType.Item);

            getPackageReferences.Execute().Should().Pass();

            List<string> packageReferences = getPackageReferences.GetValues();

            packageReferences
                .Should()
                .BeEmpty();
        }

        [WindowsOnlyFact]
        public void It_builds_with_unicode_characters_in_path()
        {
            var testProject = new TestProject()
            {
                Name = "Prj_すおヸょー",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_regenerates_files_if_self_contained_changes()
        {
            const string TFM = ToolsetInfo.CurrentTargetFramework;

            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(TFM);

            var testProject = new TestProject()
            {
                Name = "GenerateFilesTest",
                TargetFrameworks = TFM,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputPath = buildCommand.GetOutputDirectory(targetFramework: TFM, runtimeIdentifier: runtimeIdentifier).FullName;
            var depsFilePath = Path.Combine(outputPath, $"{testProject.Name}.deps.json");
            var runtimeConfigPath = Path.Combine(outputPath, $"{testProject.Name}.runtimeconfig.json");

            var depsFileLastWriteTime = File.GetLastWriteTimeUtc(depsFilePath);
            var runtimeConfigLastWriteTime = File.GetLastWriteTimeUtc(runtimeConfigPath);

            WaitForUtcNowToAdvance();

            buildCommand
                .Execute("/p:SelfContained=false")
                .Should()
                .Pass();

            depsFileLastWriteTime.Should().NotBe(File.GetLastWriteTimeUtc(depsFilePath));
            runtimeConfigLastWriteTime.Should().NotBe(File.GetLastWriteTimeUtc(runtimeConfigPath));
        }

        [Fact]
        public void It_passes_when_building_single_file_app_without_rid()
        {
            GetBuildCommand()
                .Execute("/p:PublishSingleFile=true")
                .Should()
                .Pass();
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            GetBuildCommand()
                .Execute("/p:PublishSingleFile=true", "/p:SelfContained=false", "/p:UseAppHost=false")
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_builds_the_project_successfully_with_only_reference_assembly_set(bool produceOnlyReferenceAssembly)
        {
            var testProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["ProduceOnlyReferenceAssembly"] = produceOnlyReferenceAssembly.ToString();

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: produceOnlyReferenceAssembly.ToString());

            var buildCommand = new BuildCommand(testProjectInstance);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputPath = buildCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework).FullName;
            if (produceOnlyReferenceAssembly == true)
            {
                var refPath = Path.Combine(outputPath, "ref");
                Directory.Exists(refPath)
                    .Should()
                    .BeFalse();
            }
            else
            {
                // Reference assembly should be produced in obj
                var refPath = Path.Combine(
                    buildCommand.GetIntermediateDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework).FullName,
                    "ref",
                    "MainProject.dll");
                File.Exists(refPath)
                    .Should()
                    .BeTrue();
            }
        }
    }
}
