// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithSharedProject : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithSharedProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_does_not_assign_link_metadata_to_items_from_shared_project()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithSharedProject")
                .WithSource()
                .Restore(Log, "TestApp");

            var command = new MSBuildCommand(Log, "WriteItems", testAsset.TestRoot, "TestApp");

            command.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(command.GetBaseIntermediateDirectory().FullName, "Debug", "netcoreapp2.0");
            string itemsFile = Path.Combine(intermediateOutputPath, "Items.txt");

            var items = File.ReadAllLines(itemsFile)
                .Select(l => l.Split('\t'))
                .Select(f => (itemType: f[0], fullPath: f[1], link: f[2]))
                .ToList();

            var itemDict = items.GroupBy(i => i.itemType)
                .ToDictionary(g => g.Key, g => g.Select(i => (fullPath: i.fullPath, link: i.link)).ToList());

            //  Compile item in shared project should not have link metadata assigned automatically
            itemDict["Compile"].Should().Contain((fullPath: Path.Combine(testAsset.TestRoot, "SharedProject", "Class1.cs"), link: ""));

            //  Compile item that is included via main project should have link metadata assigned
            itemDict["Compile"].Should().Contain((fullPath: Path.Combine(testAsset.TestRoot, "Linked", "LinkedClass.cs"), link: "LinkedClass.cs"));

            //  Content item from shared project should have Link metadata set relative to the shared project (via the AssignLinkMetadata target)
            itemDict["Content"].Should().Contain((fullPath: Path.Combine(testAsset.TestRoot, "SharedProject", "MyFolder", "TextFile1.txt"),
                                                  link: Path.Combine("MyFolder", "TextFile1.txt")));
        }

        [Fact]
        public void It_copies_items_from_shared_project_to_correct_output_folder()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithSharedProject")
                .WithSource()
                .Restore(Log, "TestApp");

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "TestApp");

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputPath = buildCommand.GetOutputDirectory("netcoreapp2.0");

            outputPath.Should().NotHaveFile("TextFile1.txt");

            outputPath.Sub("MyFolder").Should().HaveFile("TextFile1.txt");
        }
    }
}
