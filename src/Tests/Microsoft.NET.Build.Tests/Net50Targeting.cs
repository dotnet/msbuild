using System;
using System.Collections.Generic;
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

        [Fact(Skip="Need NuGet support for net5.0 TFM")]
        public void Net50TargetFrameworkParsesAsNetCoreAppTargetFrameworkIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "Net5Test",
                TargetFrameworks = "net5.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            buildCommand.Execute()
                .Should()
                .Pass();

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot, testProject.TargetFrameworks, "TargetFrameworkIdentifier");
            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues().Should().BeEquivalentTo(".NETCoreApp");
        }
    }
}
