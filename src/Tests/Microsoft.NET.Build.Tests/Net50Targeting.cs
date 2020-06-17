using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class Net50Targeting : SdkTest
    {
        public Net50Targeting(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void Net50TargetFrameworkParsesAsNetCoreAppTargetFrameworkIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "Net5Test",
                TargetFrameworks = "net5.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, "TargetFrameworkIdentifier");
            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues().Should().BeEquivalentTo(".NETCoreApp");
        }
    }
}
