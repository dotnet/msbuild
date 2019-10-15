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
    public class GiventThatWeManifestSupportedFrameworks : SdkTest
    {
        public GiventThatWeManifestSupportedFrameworks(ITestOutputHelper log) : base(log)
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
                TargetFrameworks = targetFrameworkIdentifier ==  ".NETCoreApp" ? "netcoreapp2.0" : "netstandard2.0",
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

            supportedFrameworks.Should().Contain($"{targetFrameworkIdentifier},Version=v{maximumVersion}");
        }
    }
}
