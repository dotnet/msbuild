using FluentAssertions;
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

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreApp : SdkTest
    {
        [Theory]
        //  TargetFramework, RuntimeFrameworkVersion, NETCoreAppPackageVersion, ExpectedPackageVersion, ExpectedRuntimeFrameworkVersion
        [InlineData("netcoreapp1.0", null, null, "1.1.0", "1.0.0")]
        [InlineData("netcoreapp1.1", null, null, "1.1.0", "1.1.0")]
        [InlineData("netcoreapp1.0", null, "1.0.3", "1.0.3", "1.0.0")]
        [InlineData("netcoreapp1.0", "1.0.1", null, "1.0.1", "1.0.1")]
        [InlineData("netcoreapp1.0", "1.0.1", "1.0.3", "1.0.3", "1.0.1")]
        public void It_targets_the_right_shared_framework(string targetFramework, string runtimeFrameworkVersion, string netCoreAppPackageVersion, 
            string expectedPackageVersion, string expectedRuntimeVersion)
        {
            var testProject = new TestProject()
            {
                Name = "SharedFrameworkTest",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            string testIdentifier = string.Join("_", targetFramework, runtimeFrameworkVersion ?? "null", netCoreAppPackageVersion ?? "null");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, nameof(It_targets_the_right_shared_framework), testIdentifier);

            testAsset = testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);

                if (runtimeFrameworkVersion != null)
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeFrameworkVersion", runtimeFrameworkVersion));
                }

                if (netCoreAppPackageVersion != null)
                {
                    propertyGroup.Add(new XElement(ns + "NetCoreAppImplicitPackageVersion", netCoreAppPackageVersion));
                }
            });

            testAsset = testAsset.Restore(testProject.Name);

            var buildCommand = new BuildCommand(Stage0MSBuild, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

            string actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
            actualRuntimeFrameworkVersion.Should().Be(expectedRuntimeVersion);

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netCoreAppLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }
    }
}
