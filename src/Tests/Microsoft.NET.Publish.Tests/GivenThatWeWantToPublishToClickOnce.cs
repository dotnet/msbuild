// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAClickOnceProject : SdkTest
    {
        public GivenThatWeWantToPublishAClickOnceProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void PublishClickOnceWithPublishProfile(bool? publishSingleFile)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var rid = EnvironmentInfo.GetCompatibleRid(tfm);

            var testProject = new TestProject()
            {
                Name = "ConsoleWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk;Microsoft.NET.Sdk.Publish",
                IsExe = true,
            };
            testProject.PackageReferences.Add(new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: publishSingleFile.ToString());

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");

            var command = new PublishCommand(testProjectInstance);
            DirectoryInfo outputDirectory = null;
            if (publishSingleFile == true)
            {
                outputDirectory = command.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);
            }
            else
            {
                outputDirectory = command.GetOutputDirectory(targetFramework: tfm);
            }

            Directory.CreateDirectory(publishProfilesDirectory);
            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
<Project>
  <PropertyGroup>
    <PublishUrl>publish\</PublishUrl>
    <PublishDir>{outputDirectory}</PublishDir>
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
    {(publishSingleFile == true ? $"<RuntimeIdentifier>{rid}</RuntimeIdentifier>" : "")}
  </PropertyGroup>
</Project>
");

            command
                .Execute("/p:PublishProfile=test")
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(new[] {
                $"setup.exe",
                $"{testProject.Name}.application",
                $"application files\\{testProject.Name}_1_2_3_4\\launcher{Constants.ExeSuffix}",
            });

            if (publishSingleFile == true)
            {
                outputDirectory.Should().HaveFiles(new[] {
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}{Constants.ExeSuffix}",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll.manifest",
                });
                outputDirectory.Should().NotHaveFiles(new[] {
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll",
                    $"application files\\{testProject.Name}_1_2_3_4\\Newtonsoft.Json.dll",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.deps.json",
                });
            }
            else
            {
                outputDirectory.Should().HaveFiles(new[] {
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.dll.manifest",
                    $"application files\\{testProject.Name}_1_2_3_4\\Newtonsoft.Json.dll",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.deps.json",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}.runtimeconfig.json",
                    $"application files\\{testProject.Name}_1_2_3_4\\{testProject.Name}{Constants.ExeSuffix}",
                });
            }
        }
    }
}
