// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithExplicitConfig : SdkTest
    {

        public GivenThatWeWantToPackAToolProjectWithExplicitConfig(ITestOutputHelper log) : base(log)
        {

        }

        [Fact]
        public void It_finds_the_entry_point_dll_and_put_in_setting_file()
        {
            const string explicitEntryPoint = "explicit_entry_point.dll";
            TestAsset helloWorldAsset = _testAssetsManager
                                        .CopyTestAsset("PortableTool", "PackPortableToolToolEntryPoint")
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                            propertyGroup.Add(new XElement("ToolEntryPoint", explicitEntryPoint));
                                        });

            helloWorldAsset.Restore(Log);
            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute();

            var nugetPackage = packCommand.GetNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var tmpfilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/{anyTfm}/any/DotnetToolSettings.xml", tmpfilePath, null);

                XDocument.Load(copiedFile)
                        .Element("DotNetCliTool")
                        .Element("Commands")
                        .Element("Command")
                        .Attribute("EntryPoint")
                        .Value
                        .Should().Be(explicitEntryPoint);
            }
        }


        [Fact]
        public void It_finds_commandName_and_put_in_setting_file()
        {
            const string explicitCommandName = "explicit_command_name";
            TestAsset helloWorldAsset = _testAssetsManager
                                        .CopyTestAsset("PortableTool", "PackPortableToolToolCommandName")
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                            propertyGroup.Add(new XElement("ToolCommandName", explicitCommandName));
                                        })
                                        .Restore(Log);
            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute();

            var nugetPackage = packCommand.GetNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var tmpfilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/{anyTfm}/any/DotnetToolSettings.xml", tmpfilePath, null);

                XDocument.Load(copiedFile)
                        .Element("DotNetCliTool")
                        .Element("Commands")
                        .Element("Command")
                        .Attribute("Name")
                        .Value
                        .Should().Be(explicitCommandName);
            }
        }
    }
}
