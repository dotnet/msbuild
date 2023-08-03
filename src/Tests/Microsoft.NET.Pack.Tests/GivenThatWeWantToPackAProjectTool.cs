// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                Name = "TestToolNetCore22",
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var asset = _testAssetsManager
                       .CreateTestProject(toolProject, toolProject.Name);

            var packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, toolProject.Name));
            packCommand.Execute().Should().Pass();
        }

        [Fact]
        public void It_fails_to_pack_project_tools_targeting_netcoreapp3_0()
        {
            TestProject toolProject = new TestProject()
            {
                Name = "TestTool",
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var asset = _testAssetsManager
                       .CreateTestProject(toolProject, toolProject.Name);

            var result = new PackCommand(Log, Path.Combine(asset.TestRoot, toolProject.Name)).Execute();
            result
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.ProjectToolOnlySupportTFMLowerThanNetcoreapp22);

        }
    }
}
