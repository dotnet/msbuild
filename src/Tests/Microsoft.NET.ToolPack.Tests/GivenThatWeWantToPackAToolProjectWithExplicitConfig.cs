// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            var packCommand = new PackCommand(helloWorldAsset);

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
                                        });
            var packCommand = new PackCommand(helloWorldAsset);

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
