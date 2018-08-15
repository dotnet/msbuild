// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToControlGeneratedAssemblyInfo : SdkTest
    {
        public GivenThatWeWantToControlGeneratedAssemblyInfo(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("AssemblyInformationVersionAttribute")]
        [InlineData("AssemblyFileVersionAttribute")]
        [InlineData("AssemblyVersionAttribute")]
        [InlineData("AssemblyCompanyAttribute")]
        [InlineData("AssemblyConfigurationAttribute")]
        [InlineData("AssemblyCopyrightAttribute")]
        [InlineData("AssemblyDescriptionAttribute")]
        [InlineData("AssemblyTitleAttribute")]
        [InlineData("NeutralResourcesLanguageAttribute")]
        [InlineData("All")]
        public void It_respects_opt_outs(string attributeToOptOut)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: Path.DirectorySeparatorChar + attributeToOptOut)
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute(
                    "/p:Version=1.2.3-beta",
                    "/p:FileVersion=4.5.6.7",
                    "/p:AssemblyVersion=8.9.10.11",
                    "/p:Company=TestCompany",
                    "/p:Configuration=Release",
                    "/p:Copyright=TestCopyright",
                    "/p:Description=TestDescription",
                    "/p:Product=TestProduct",
                    "/p:AssemblyTitle=TestTitle",
                    "/p:NeutralLanguage=fr",
                    attributeToOptOut == "All" ?
                        "/p:GenerateAssemblyInfo=false" :
                        $"/p:Generate{attributeToOptOut}=false"
                    )
                .Should()
                .Pass();

            var expectedInfo = new SortedDictionary<string, string>
            {
                { "AssemblyInformationalVersionAttribute", "1.2.3-beta" },
                { "AssemblyFileVersionAttribute", "4.5.6.7" },
                { "AssemblyVersionAttribute", "8.9.10.11" },
                { "AssemblyCompanyAttribute", "TestCompany" },
                { "AssemblyConfigurationAttribute", "Release" },
                { "AssemblyCopyrightAttribute", "TestCopyright" },
                { "AssemblyDescriptionAttribute", "TestDescription" },
                { "AssemblyProductAttribute", "TestProduct" },
                { "AssemblyTitleAttribute", "TestTitle" },
                { "NeutralResourcesLanguageAttribute", "fr" },
            };

            if (attributeToOptOut == "All")
            {
                expectedInfo.Clear();
            }
            else
            {
                expectedInfo.Remove(attributeToOptOut);
            }

            expectedInfo.Add("TargetFrameworkAttribute", ".NETCoreApp,Version=v1.1");

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netcoreapp1.1", "Release").FullName, "HelloWorld.dll");
            var actualInfo = AssemblyInfo.Get(assemblyPath);

            actualInfo.Should().Equal(expectedInfo);
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_initialize_source_control_target_not_available()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            testAsset.Restore(Log, testProject.Name);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().ShouldBeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_source_revision_id_not_set()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", ""))));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));
                });

            testAsset.Restore(Log, testProject.Name);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().ShouldBeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_disabled()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true"),
                            new XElement("IncludeSourceRevisionInInformationalVersion", "false")));
                });

            testAsset.Restore(Log, testProject.Name);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().ShouldBeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_includes_source_revision_id_if_available__version_without_plus()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "_SetSourceRevisionId"),
                            new XAttribute("BeforeTargets", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation")));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));
                });

            testAsset.Restore(Log, testProject.Name);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().ShouldBeEquivalentTo(new[] { "1.0.0+xyz" });
        }

        [Fact]
        public void It_includes_source_revision_id_if_available__version_with_plus()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "_SetSourceRevisionId"),
                            new XAttribute("BeforeTargets", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation")));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true"),
                            new XElement("InformationalVersion", "1.2.3+abc")));
                });

            testAsset.Restore(Log, testProject.Name);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().ShouldBeEquivalentTo(new[] { "1.2.3+abc.xyz" });
        }

        [WindowsOnlyTheory]
        [InlineData("netcoreapp1.1")]
        [InlineData("net45")]
        public void It_respects_version_prefix(string targetFramework)
        {
            if (targetFramework == "net45")
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .Restore(Log, "", $"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}");

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute($"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}", $"/p:VersionPrefix=1.2.3")
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");
            var info = AssemblyInfo.Get(assemblyPath);

            info["AssemblyVersionAttribute"].Should().Be("1.2.3.0");
            info["AssemblyFileVersionAttribute"].Should().Be("1.2.3.0");
            info["AssemblyInformationalVersionAttribute"].Should().Be("1.2.3");
        }

        [WindowsOnlyTheory]
        [InlineData("netcoreapp1.1")]
        [InlineData("net45")]
        public void It_respects_version_changes_on_incremental_build(string targetFramework)
        {
            if (targetFramework == "net45")
            {
                return;
            }

            // Given a project that has already been built
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .Restore(Log, "", $"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}");
            BuildProject(versionPrefix: "1.2.3");

            // When the same project is built again using a different VersionPrefix property
            var incrementalBuildCommand = BuildProject(versionPrefix: "1.2.4");

            // Then the version of the built assembly shall match the provided VersionPrefix
            var assemblyPath = Path.Combine(incrementalBuildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");
            var info = AssemblyInfo.Get(assemblyPath);
            info["AssemblyVersionAttribute"].Should().Be("1.2.4.0");

            BuildCommand BuildProject(string versionPrefix)
            {
                var command = new BuildCommand(Log, testAsset.TestRoot);
                command.Execute($"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}", $"/p:VersionPrefix={versionPrefix}")
                       .Should()
                       .Pass();
                return command;
            }
        }

        [Fact]
        public void It_respects_custom_assembly_attribute_items_on_incremental_build()
        {
            var targetFramework = "netstandard1.5";
            var testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink", identifier: targetFramework)
                .WithSource()
                .Restore(Log, "TestLibrary");

            var firstBuildCommand = BuildProject(buildNumber: "1");
            var assemblyPath = Path.Combine(firstBuildCommand.GetOutputDirectory(targetFramework).FullName, "TestLibrary.dll");
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("BuildNumber:1");

            var firstWriteTime = File.GetLastWriteTimeUtc(assemblyPath);

            // When rebuilding with the same value
            BuildProject(buildNumber: "1");

            // the build should no-op.
            File.GetLastWriteTimeUtc(assemblyPath).Should().Be(firstWriteTime);

            // When the same project is built again using a different build number
            BuildProject(buildNumber: "2");

            // the file should change
            File.GetLastWriteTimeUtc(assemblyPath).Should().NotBe(firstWriteTime);

            // and the custom assembly should be generated with the updated value.
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("BuildNumber:2");

            BuildCommand BuildProject(string buildNumber)
            {
                var command = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, "TestLibrary"));
                command.Execute($"/p:BuildNumber={buildNumber}")
                       .Should()
                       .Pass();
                return command;
            }
        }
    }
}
