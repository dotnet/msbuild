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

        [Theory]
        [InlineData(".NETCoreApp")]
        [InlineData(".NETStandard")]
        public void TheMaximumVersionsAreSupported(string targetFrameworkIdentifier)
        {
            var project = new TestProject
            {
                Name = "packagethatwillgomissing",
                TargetFrameworks = targetFrameworkIdentifier ==  ".NETCoreApp" ? "netcoreapp3.0" : "netstandard2.1",
                IsSdkProject = true,
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
    }
}
