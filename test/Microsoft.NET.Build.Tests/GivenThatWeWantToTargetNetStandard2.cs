using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToTargetNetStandard2 : SdkTest
    {
        [Fact]
        public void It_builds_a_netstandard2_library_successfully()
        {
            TestProject project = new TestProject()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(project)
                .Restore(project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Stage0MSBuild, projectFolder);

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


            var testAsset = _testAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    //  Note: if you want to see how this fails when conflicts are not resolved, set the DisableHandlePackageFileConflicts property to true, like this:
                    //  p.Root.Element(ns + "PropertyGroup").Add(new XElement(ns + "DisableHandlePackageFileConflicts", "True"));

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "PackageReference",
                        new XAttribute("Include", "System.Xml.XDocument"),
                        new XAttribute("Version", "4.3.0")));

                })
                .Restore(project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Stage0MSBuild, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }
    }
}
