// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPackAProjectTool : SdkTest
    {
        public GivenThatWeWantToPackAProjectTool(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_packs_project_tools_targeting_netcoreapp2_2()
        {
            TestProject toolProject = new TestProject()
            {
                Name = "TestTool",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var asset = _testAssetsManager
                       .CreateTestProject(toolProject, toolProject.Name)
                       .Restore(Log, toolProject.Name);

            var packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, toolProject.Name));
            packCommand.Execute().Should().Pass();
        }

        [Fact]
        public void It_fails_to_pack_project_tools_targeting_netcoreapp3_0()
        {
            TestProject toolProject = new TestProject()
            {
                Name = "TestTool",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var asset = _testAssetsManager
                       .CreateTestProject(toolProject, toolProject.Name)
                       .Restore(Log, toolProject.Name);

            var result = new PackCommand(Log, Path.Combine(asset.TestRoot, toolProject.Name)).Execute();
            result
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.ProjectToolOnlySupportTFMLowerThanNetcoreapp22);

        }
    }
}
