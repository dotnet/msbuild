// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToSetPropertiesInDirectoryBuildProps : SdkTest
    {
        public GivenThatWeWantToSetPropertiesInDirectoryBuildProps(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void The_default_configuration_can_be_set_to_release()
        {
            TestProject project = new()
            {
                Name = "DirectoryBuildPropsTest",
                TargetFrameworks = "netstandard1.4",
            };

            var testAsset = _testAssetsManager.CreateTestProject(project);

            string directoryBuildPropsPath = Path.Combine(testAsset.Path, "Directory.Build.props");

            var directoryBuildPropsContent = @"
<Project>
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Release</Configuration>
  </PropertyGroup>
</Project>
";

            File.WriteAllText(directoryBuildPropsPath, directoryBuildPropsContent);

            var restoreCommand = testAsset.GetRestoreCommand(Log, project.Name);

            restoreCommand
                .Execute()
                .Should()
                .Pass();

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string GetPropertyValue(string propertyName)
            {
                var getValuesCommand = new GetValuesCommand(Log, projectFolder,
                    project.TargetFrameworks, propertyName, GetValuesCommand.ValueType.Property)
                {
                    Configuration = "Release"
                };

                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();

                var values = getValuesCommand.GetValues();
                values.Count.Should().Be(1);
                return values[0];
            }

            GetPropertyValue("Configuration").Should().Be("Release");
            GetPropertyValue("Optimize").Should().Be("true");
        }

    }
}
