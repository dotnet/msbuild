using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class DesignTimeBuildTests : SdkTest
    {
        public DesignTimeBuildTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("TestLibrary", null)]
        [InlineData("TestApp", null)]
        [InlineData("TestApp", "netcoreapp2.1")]
        [InlineData("TestApp", ToolsetInfo.CurrentTargetFramework)]
        public void The_design_time_build_succeeds_before_nuget_restore(string relativeProjectPath, string targetFramework)
        {
            var args = GetDesignTimeMSBuildArgs();
            if (args == null)
            {
                //  Design-time targets couldn't be found
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: relativeProjectPath + "_" + targetFramework ?? string.Empty)
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    //  Add dummy target called by design-time build which may not yet be defined in the version
                    //  of Visual Studio we are testing with.
                    p.Root.Add(new XElement(ns + "Target",
                                    new XAttribute("Name", "CollectFrameworkReferences")));
                })
                .WithTargetFramework(targetFramework, relativeProjectPath);


            var projectDirectory = Path.Combine(testAsset.TestRoot, relativeProjectPath);

            var command = new MSBuildCommand(Log, "ResolveAssemblyReferencesDesignTime", projectDirectory);
            command.WorkingDirectory = projectDirectory;
            var result = command.Execute(args);

            result.Should().Pass();
        }

        [Fact]
        public void DesignTimeBuildSucceedsAfterTargetFrameworkIsChanged()
        {
            TestDesignTimeBuildAfterChange(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Element(ns + "PropertyGroup")
                    .Element(ns + "TargetFramework")
                    .SetValue(ToolsetInfo.CurrentTargetFramework);
            });
        }

        [Fact]
        public void DesignTimeBuildSucceedsAfterRuntimeIdentifierIsChanged()
        {
            TestDesignTimeBuildAfterChange(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RuntimeIdentifier", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64"));
            });
        }

        //  Regression test for https://github.com/dotnet/sdk/issues/13513
        [Fact]
        public void DesignTimeBuildSucceedsWhenTargetingNetCore21WithRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "DesignTimePackageDependencies",
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new MSBuildCommand(testAsset, "ResolvePackageDependenciesDesignTime")
                .Execute()
                .Should()
                .Pass();
       }

        [Theory]
        [InlineData("netcoreapp3.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows7.0")]
        public void DesignTimePackageDependenciesAreResolved(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "DesignTimePackageDependencies",
                TargetFrameworks = targetFramework,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion(), privateAssets: "All"));
            testProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var getValuesCommand = new GetValuesCommand(testAsset, "_PackageDependenciesDesignTime", GetValuesCommand.ValueType.Item);
            getValuesCommand.DependsOnTargets = "ResolvePackageDependenciesDesignTime";

            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues()
                .Should()
                .BeEquivalentTo($"Newtonsoft.Json/{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}", "Humanizer/2.8.26");
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows7.0")]
        public void PackageErrorsAreSet(string targetFramework)
        {
            var designTimeArgs = GetDesignTimeMSBuildArgs();
            if (designTimeArgs == null)
            {
                //  Design-time targets couldn't be found
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DesignTimePackageDependencies",
                TargetFrameworks = targetFramework,
            };

            //  Downgrade will cause an error
            testProject.AdditionalProperties["ContinueOnError"] = "ErrorAndContinue";

            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0"));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Fail();

            var getValuesCommand = new GetValuesCommand(testAsset, "_PackageDependenciesDesignTime", GetValuesCommand.ValueType.Item);
            getValuesCommand.ShouldRestore = false;
            getValuesCommand.DependsOnTargets = "ResolvePackageDependenciesDesignTime";
            getValuesCommand.MetadataNames = new List<string>() { "DiagnosticLevel" };

            getValuesCommand
                .WithWorkingDirectory(testAsset.TestRoot)
                .Execute(designTimeArgs)
                .Should()
                .Fail();

            var valuesWithMetadata = getValuesCommand.GetValuesWithMetadata();
            var nugetPackagingMetadata = valuesWithMetadata.Single(kvp => kvp.value.Equals("NuGet.Packaging/3.5.0")).metadata;
            nugetPackagingMetadata["DiagnosticLevel"].Should().Be("Error");

        }

        private void TestDesignTimeBuildAfterChange(Action<XDocument> projectChange, [CallerMemberName] string callingMethod = "")
        {
            var designTimeArgs = GetDesignTimeMSBuildArgs();
            if (designTimeArgs == null)
            {
                //  Design-time targets couldn't be found
                return;
            }

            var testProject = new TestProject()
            {
                Name = "App",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            //  Add some package references to test more code paths (such as in ResolvePackageAssets)
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion(), privateAssets: "All"));
            testProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod: callingMethod)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    //  Add dummy target called by design-time build which may not yet be defined in the version
                    //  of Visual Studio we are testing with.
                    p.Root.Add(new XElement(ns + "Target",
                                    new XAttribute("Name", "CollectFrameworkReferences")));
                });

            string projectFolder = Path.Combine(testAsset.TestRoot, testProject.Name);

            var buildCommand = new MSBuildCommand(Log, null, projectFolder);
            buildCommand.WorkingDirectory = projectFolder;

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string projectFilePath = Path.Combine(projectFolder, testProject.Name + ".csproj");

            var project = XDocument.Load(projectFilePath);

            projectChange(project);

            project.Save(projectFilePath);

            buildCommand
                .ExecuteWithoutRestore(designTimeArgs)
                .Should()
                .Pass();

            buildCommand
                .Execute()
                .Should()
                .Pass();

            buildCommand
                .ExecuteWithoutRestore(designTimeArgs)
                .Should()
                .Pass();

        }

        private static string[] GetDesignTimeMSBuildArgs()
        {
            //  This test needs the design-time targets, which come with Visual Studio.  So we will use the VSINSTALLDIR
            //  environment variable to find the install path to Visual Studio and the design-time targets under it.
            //  This will be set when running from a developer command prompt.  Unfortunately, unless VS is launched
            //  from a developer command prompt, it won't be set when running tests from VS.  So in that case the
            //  test will simply be skipped.
            string vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");

            if (vsInstallDir == null)
            {
                return null;
            }

            string csharpDesignTimeTargets = Path.Combine(vsInstallDir, @"MSBuild\Microsoft\VisualStudio\Managed\Microsoft.CSharp.DesignTime.targets");

            var args = new[]
            {
                "/p:DesignTimeBuild=true",
                "/p:SkipCompilerExecution=true",
                "/p:ProvideCommandLineArgs=true",
                $"/p:CSharpDesignTimeTargetsPath={csharpDesignTimeTargets}",
                "/t:CollectResolvedSDKReferencesDesignTime;CollectPackageReferences;ResolveComReferencesDesignTime",
                "/t:ResolveProjectReferencesDesignTime;BuiltProjectOutputGroup;CollectFrameworkReferences",
                "/t:CollectUpToDateCheckBuiltDesignTime;CollectPackageDownloads;ResolveAssemblyReferencesDesignTime",
                "/t:CollectAnalyzersDesignTime;CollectSDKReferencesDesignTime;CollectUpToDateCheckInputDesignTime",
                "/t:CollectUpToDateCheckOutputDesignTime;ResolvePackageDependenciesDesignTime;CompileDesignTime",
                "/t:CollectResolvedCompilationReferencesDesignTime;ResolveFrameworkReferences",
                //  Set targeting pack folder to nonexistant folder so the project won't use installed targeting packs
                "/p:NetCoreTargetingPackRoot=" + Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            };

            return args;
        }
    }
}
