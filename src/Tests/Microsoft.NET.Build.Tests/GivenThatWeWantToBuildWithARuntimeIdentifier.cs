// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
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
                .Execute("/p:RuntimeIdentifier=win-x64")
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
                    itemGroup.Add(new XElement(ns + "RuntimeIdentifier", "win-x64"));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "App.sln");
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
