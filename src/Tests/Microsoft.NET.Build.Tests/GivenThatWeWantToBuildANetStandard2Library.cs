// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetStandard2Library : SdkTest
    {
        public GivenThatWeWantToBuildANetStandard2Library(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netstandard2.0")]
        [InlineData("netstandard2.1")]
        public void It_builds_a_netstandard2_library_successfully(string targetFramework)
        {
            TestProject project = new TestProject()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(project, identifier: targetFramework)
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }

        [Fact]
        public void It_resolves_assembly_conflicts()
        {
            TestProject project = new TestProject()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            project.SourceFiles[project.Name + ".cs"] = $@"
using System;
public static class {project.Name}
{{
    {ConflictResolutionAssets.ConflictResolutionTestMethod}
}}";

            var testAsset = _testAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                })
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }
    }
}
