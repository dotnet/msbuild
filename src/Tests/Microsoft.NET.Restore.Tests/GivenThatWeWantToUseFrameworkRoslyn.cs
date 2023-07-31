// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToUseFrameworkRoslyn : SdkTest
    {
        public GivenThatWeWantToUseFrameworkRoslyn(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_restores_Microsoft_Net_Compilers_Toolset_Framework_when_requested()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };

            // Add an explicit version for the test. This will normally come from the installer.
            project.AdditionalProperties.Add("_NetFrameworkHostedCompilersVersion", "4.7.0-2.23260.7");
            project.AdditionalProperties.Add("BuildWithNetFrameworkHostedCompiler", "true");

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute().Should().Pass();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Contains("Microsoft.Net.Compilers.Toolset.Framework", File.ReadAllText(projectAssetsJsonPath));
            }
            else
            {
                Assert.DoesNotContain("Microsoft.Net.Compilers.Toolset.Framework", File.ReadAllText(projectAssetsJsonPath));
            }
        }

        [Fact]
        public void It_throws_a_warning_when_adding_the_PackageReference_directly()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };

            project.PackageReferences.Add(new TestPackageReference("Microsoft.Net.Compilers.Toolset.Framework", "4.7.0-2.23260.7"));

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            var result = restoreCommand.Execute();
            result.Should().Pass();
            result.Should().HaveStdOutContaining("NETSDK1205");
        }
    }
}
