// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Xml.Linq;
using Xunit.Abstractions;
using System.Collections.Generic;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToExcludeAPackageFromPublish : SdkTest
    {
        public GivenThatWeWantToExcludeAPackageFromPublish(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.1", false)]
        [InlineData("netcoreapp2.0", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_does_not_publish_a_PackageReference_with_PrivateAssets_All(string targetFramework, bool shouldIncludeExecutable)
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PublishExcludePackage", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    //  Using different casing for the package ID here, to test the scenario from https://github.com/dotnet/sdk/issues/376
                    itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", "NEWTONSOFT.Json"),
                                                                        new XAttribute("Version", "9.0.1"),
                                                                        new XAttribute("PrivateAssets", "All")));
                });

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            var expectedFiles = new List<string>()
            {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            };

            if (shouldIncludeExecutable)
            {
                expectedFiles.Add("HelloWorld" + EnvironmentInfo.ExecutableExtension);
            }

            publishDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [Theory]
        [InlineData("netcoreapp1.1", false)]
        [InlineData("netcoreapp2.0", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_does_not_publish_a_PackageReference_with_Publish_false(string targetFramework, bool shouldIncludeExecutable)
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PublishPackagePublishFalse", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", "Newtonsoft.Json"),
                                                                        new XAttribute("Version", "9.0.1"),
                                                                        new XAttribute("Publish", "false")));
                });

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            var expectedFiles = new List<string>()
            {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            };

            if (shouldIncludeExecutable)
            {
                expectedFiles.Add("HelloWorld" + EnvironmentInfo.ExecutableExtension);
            }

            publishDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [Theory]
        [InlineData("netcoreapp1.1", false)]
        [InlineData("netcoreapp2.0", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_publishes_a_PackageReference_with_PrivateAssets_All_and_Publish_true(string targetFramework, bool shouldIncludeExecutable)
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PublishPrivateAssets", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", "Newtonsoft.Json"),
                                                                        new XAttribute("Version", "9.0.1"),
                                                                        new XAttribute("PrivateAssets", "All"),
                                                                        new XAttribute("Publish", "true")));
                });

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            var expectedFiles = new List<string>()
            {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json",
                "Newtonsoft.Json.dll",
            };

            if (targetFramework == "netcoreapp1.1")
            {
                expectedFiles.Add("System.Runtime.Serialization.Primitives.dll");
            }

            if (shouldIncludeExecutable)
            {
                expectedFiles.Add("HelloWorld" + EnvironmentInfo.ExecutableExtension);
            }

            publishDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void TransitiveNetStandardPackageReferenceAndPublishFalse()
        {
            var testLibraryProject = new TestProject()
            {
                Name = "TestLibrary",
                TargetFrameworks = "netstandard2.0"
            };

            testLibraryProject.PackageReferences.Add(new TestPackageReference("WindowsAzure.Storage", "9.3.3"));

            var testProject = new TestProject()
            {
                Name = "TestApp",
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "13.0.1", privateAssets: "all"));

            testProject.ReferencedProjects.Add(testLibraryProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute().Should().Pass();
        }

        [Fact]
        public void It_does_not_exclude_packages_depended_on_by_non_privateassets_references()
        {
            var testProject = new TestProject()
            {
                Name = "PrivateAssetsTransitive",
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            //  Both these packages depend on NewtonSoft.Json.  Since only one of the package references specifies PrivateAssets=All,
            //  NewtonSoft.Json should be included in the publish output
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json.Schema", "3.0.13"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.DependencyModel", "3.1.6", privateAssets: "all"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("Newtonsoft.Json.dll");
            publishDirectory.Should().HaveFile("Newtonsoft.Json.Schema.dll");
            publishDirectory.Should().NotHaveFile("Microsoft.Extensions.DependencyModel");
        }
    }
}
