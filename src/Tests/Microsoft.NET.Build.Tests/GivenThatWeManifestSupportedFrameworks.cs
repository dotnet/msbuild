using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeManifestSupportedFrameworks : SdkTest
    {
        public GivenThatWeManifestSupportedFrameworks(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0")]
        [InlineData(".NETCoreApp")]
        [InlineData(".NETStandard")]
        public void TheMaximumVersionsAreSupported(string targetFrameworkIdentifier)
        {
            var project = new TestProject
            {
                Name = "packagethatwillgomissing",
                TargetFrameworks = targetFrameworkIdentifier ==  ".NETCoreApp" ? ToolsetInfo.CurrentTargetFramework : "netstandard2.1",
            };

            TestAsset asset = _testAssetsManager
                .CreateTestProject(project, identifier: targetFrameworkIdentifier);

            string testDirectory = Path.Combine(asset.TestRoot, project.Name);

            var getMaximumVersion = new GetValuesCommand(
                Log, 
                testDirectory, 
                project.TargetFrameworks, 
                targetFrameworkIdentifier.Substring(1) + "MaximumVersion",
                GetValuesCommand.ValueType.Property);

            var getSupportedFrameworks = new GetValuesCommand(
                Log,
                testDirectory,
                project.TargetFrameworks,
                "SupportedTargetFramework",
                GetValuesCommand.ValueType.Item);

            getMaximumVersion.DependsOnTargets = "";
            getMaximumVersion.Execute().Should().Pass();

            getSupportedFrameworks.DependsOnTargets = "";
            getSupportedFrameworks.Execute().Should().Pass();

            string maximumVersion = getMaximumVersion.GetValues().Single();
            List<string> supportedFrameworks = getSupportedFrameworks.GetValues();

            string expectedTFM = $"{targetFrameworkIdentifier},Version=v{maximumVersion}";

            supportedFrameworks.Should().Contain(expectedTFM,
                because: $"Microsoft.NET.SupportedTargetFrameworks.props should include an entry for {expectedTFM}");
        }

        [Fact]
        public void TheSupportedTargetFrameworkListIsComposed()
        {
            var project = new TestProject
            {
                Name = "SupportedTargetFrameworkLists",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            TestAsset asset = _testAssetsManager.CreateTestProject(project);

            string testDirectory = Path.Combine(asset.TestRoot, project.Name);

            var supportedNetCoreAppTFs = GetItems(
                testDirectory,
                project.TargetFrameworks,
                "SupportedNETCoreAppTargetFramework");

            supportedNetCoreAppTFs.Should().NotBeEmpty();

            var supportedNetStandardTFs = GetItems(
                testDirectory,
                project.TargetFrameworks,
                "SupportedNETStandardTargetFramework");

            supportedNetStandardTFs.Should().NotBeEmpty();

            var supportedNetFrameworkTFs = GetItems(
                testDirectory,
                project.TargetFrameworks,
                "SupportedNETFrameworkTargetFramework");

            supportedNetFrameworkTFs.Should().NotBeEmpty();

            var supportedTFs = GetItems(
                testDirectory,
                project.TargetFrameworks,
                "SupportedTargetFramework");

            supportedNetCoreAppTFs
                .Union(supportedNetStandardTFs)
                .Union(supportedNetFrameworkTFs)
                .Should()
                .Equal(supportedTFs);
        }

        private List<string> GetItems(string testDirectory, string tfm, string itemName)
        {
            var command = new GetValuesCommand(
                Log,
                testDirectory,
                tfm,
                itemName,
                GetValuesCommand.ValueType.Item);

            command.DependsOnTargets = "";
            command.Execute().Should().Pass();

            return command.GetValues();
        }
    }
}
