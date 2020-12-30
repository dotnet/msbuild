// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using Microsoft.Extensions.DependencyModel;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAClickOnceProject : SdkTest
    {
        public GivenThatWeWantToPublishAClickOnceProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyTheory(Skip = "Disabled for now")]
        [InlineData(false)]
        [InlineData(true)]
        public void It_publishes_with_a_publish_profile(bool? publishSingleFile)
        {
            var tfm = "netcoreapp3.1";
            var testProject = new TestProject()
            {
                Name = "ConsoleWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk;Microsoft.NET.Sdk.Publish",
                IsExe = true,
            };
            testProject.PackageReferences.Add(new TestPackageReference("NewtonSoft.Json", "9.0.1"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
<Project>
  <PropertyGroup>
    <PublishUrl>publish\</PublishUrl>
    <Install>true</Install>
    <InstallFrom>Disk</InstallFrom>
    <ApplicationRevision>4</ApplicationRevision>
    <ApplicationVersion>1.2.3.*</ApplicationVersion>
    <PublishProtocol>ClickOnce</PublishProtocol>
    <BootstrapperEnabled>True</BootstrapperEnabled>
    <UpdateEnabled>False</UpdateEnabled>
    <IsWebBootstrapper>false</IsWebBootstrapper>
    <CreateWebPageOnPublish>true</CreateWebPageOnPublish>
    <GenerateManifests>true</GenerateManifests>
    <PublishWizardCompleted>true</PublishWizardCompleted>
    <SelfContained>false</SelfContained>
    {(publishSingleFile.HasValue ? $"<PublishSingleFile>{publishSingleFile}</PublishSingleFile>" : "")}
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            command
                .Execute("/p:PublishProfile=test")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(targetFramework: tfm);

            output.Should().HaveFiles(new[] {
                $"app.Publish\\setup.exe",
                $"app.Publish\\{testProject.Name}.application",
                $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\launcher{Constants.ExeSuffix}",
            });

            if (publishSingleFile ?? true)
            {
                output.Should().HaveFiles(new[] {
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}{Constants.ExeSuffix}",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}{Constants.ExeSuffix}.manifest",
                });
                output.Should().NotHaveFiles(new[] {
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll.manifest",
                });
            }
            else
            {
                output.Should().HaveFiles(new[] {
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll.manifest",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\Newtonsoft.Json.dll",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.deps.json",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.runtimeconfig.json",
                    $"app.Publish\\application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}{Constants.ExeSuffix}",
                });
            }
        }
    }
}
