// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAWebApp : SdkTest
    {
        public GivenThatWeWantToPublishAWebApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_as_framework_dependent_by_default()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("WebApp")
                .WithSource();

            var args = new[]
            {
                "-p:Configuration=Release"
            };

            var restoreCommand = new RestoreCommand(Log, testAsset.TestRoot);
            restoreCommand
                .Execute(args)
                .Should()
                .Pass();

            var command = new PublishCommand(Log, testAsset.TestRoot);

            command
                .Execute(args)
                .Should()
                .Pass();

            var publishDirectory =
                command.GetOutputDirectory(targetFramework: "netcoreapp2.0", configuration: "Release");

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(new[] {
                "web.config",
                "web.deps.json",
                "web.dll",
                "web.pdb",
                "web.PrecompiledViews.dll",
                "web.PrecompiledViews.pdb",
                "web.runtimeconfig.json",
            });
        }

        [Fact]
        public void It_should_publish_self_contained_for_2x()
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(
                    (filename, project) =>
                    {
                        project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
                    });

            var command = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute("/restore", $"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            output.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            Command.Create(Path.Combine(output.FullName, $"{testProject.Name}{Constants.ExeSuffix}"), new string[] {})
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }


        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_should_publish_framework_dependent_for_2x(string platformLibrary)
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference(platformLibrary));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(
                    (filename, project) =>
                    {
                        project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
                    });

            var command = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute("/restore", $"/p:RuntimeIdentifier={rid}", "/p:SelfContained=false")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
            });
        }
    }
}
