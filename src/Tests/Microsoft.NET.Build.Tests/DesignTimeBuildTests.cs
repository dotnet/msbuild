using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
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
        [InlineData("TestApp", "netcoreapp3.0")]
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
                    .SetValue("netcoreapp2.1");
            });
        }

        [Fact]
        public void DesignTimeBuildSucceedsAfterRuntimeIdentifierIsChanged()
        {
            TestDesignTimeBuildAfterChange(project =>
            {
                var ns = project.Root.Name.Namespace;
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RuntimeIdentifier", "win-x64"));
            });
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
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            //  Add some package references to test more code paths (such as in ResolvePackageAssets)
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "12.0.2", privateAssets: "All"));
            testProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.6.2"));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
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
                "/p:NetCoreTargetingPackRoot=" + Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            };

            return args;
        }
    }
}
