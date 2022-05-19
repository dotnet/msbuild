// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithARuntimeIdentifier : SdkTest
    {
        public GivenThatWeWantToBuildWithARuntimeIdentifier(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_fails_with_solution_level_RID()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource();

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "App.sln");
            buildCommand
                .Execute($"/p:RuntimeIdentifier={ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1134");
        }

        [Fact]
        public void It_succeeds_with_project_level_RID()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "RuntimeIdentifier", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64"));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "App.sln");
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_fails_with_unsupported_RID()
        {
            var testProject = new TestProject()
            {
                Name = "DesignTimePackageDependencies",
                //  Note: The logic is different for .NET Core 3+, there is a different test that covers that (and the error is different too, it's NETSDK1083)
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                //  Note the typo in the RID
                RuntimeIdentifier = "won-x64"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1056");
        }
    }
}
