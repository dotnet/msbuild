// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        private string _testRoot;

        public GivenThatWeWantToPackAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        private string SetupNuGetPackage(bool multiTarget, [CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", callingMethod + multiTarget)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                })
                .WithTargetFrameworkOrFrameworks("netcoreapp2.1", multiTarget)
                .Restore(Log);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Pass();

            return packCommand.GetNuGetPackage();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_packs_successfully(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var tmpfilePath = Path.Combine(_testRoot, "temp", Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/{anyTfm}/any/DotnetToolSettings.xml", tmpfilePath, null);
                XElement command = XDocument.Load(copiedFile)
                                      .Element("DotNetCliTool")
                                      .Element("Commands")
                                      .Element("Command");

                command.Attribute("Name")
                        .Value
                        .Should().Be("consoledemo", "it should contain command name that is same as the msbuild well known properties $(TargetName)");

                command.Attribute("EntryPoint")
                        .Value
                        .Should().Be("consoledemo.dll", "it should contain entry point dll that is same as the msbuild well known properties $(TargetFileName)");

            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_removes_all_package_dependencies(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageDependencies()
                    .Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_runtimeconfig_for_each_tfm(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/consoledemo.runtimeconfig.json");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_contain_apphost_exe(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().NotContain($"tools/{framework.GetShortFolderName()}/any/consoledemo{extension}");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_DotnetToolSettingsXml_for_each_tfm(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/DotnetToolSettings.xml");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_contain_lib(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader.GetLibItems().Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_folder_structure_tfm_any(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().Contain(
                        f => f.Items.
                            Contains($"tools/{f.TargetFramework.GetShortFolderName()}/any/consoledemo.dll"));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_packagetype_dotnettool(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageTypes().Should().ContainSingle(t => t.Name == "DotnetTool");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_dependencies_dll(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Newtonsoft.Json.dll");
                }
            }
        }
    }
}
