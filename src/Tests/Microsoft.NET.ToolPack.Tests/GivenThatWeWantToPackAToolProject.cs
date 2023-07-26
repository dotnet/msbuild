// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Packaging;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        private string _testRoot;
        private string _targetFrameworkOrFrameworks = "netcoreapp2.1";

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
                .WithTargetFrameworkOrFrameworks(_targetFrameworkOrFrameworks, multiTarget);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(helloWorldAsset);

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
            AssertFiles(nugetPackage);
        }

        [Fact]
        public void Given_nuget_alias_It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file()
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Elements("TargetFramework").First().SetValue("targetframeworkAlias");
                    XElement conditionPropertyGroup = new XElement("PropertyGroup");
                    project.Root.Add(conditionPropertyGroup);
                    conditionPropertyGroup.SetAttributeValue("Condition", "'$(TargetFramework)' == 'targetframeworkAlias'");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkIdentifier", ".NETCoreApp");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkVersion", "v3.1");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkMoniker", ".NETCoreApp,Version=v3.1");
                });

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Pass();

            var nugetPackage = packCommand.GetNuGetPackage();
            AssertFiles(nugetPackage);
        }

        private void AssertFiles(string nugetPackage)
        {
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var tmpfilePath = Path.Combine(_testRoot, "temp", Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/{anyTfm}/any/DotnetToolSettings.xml", tmpfilePath, null);

                var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                allItems.Should().Contain($"tools/{anyTfm}/any/consoledemo.runtimeconfig.json");

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
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            _targetFrameworkOrFrameworks = ToolsetInfo.CurrentTargetFramework;

            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().NotContain($"tools/{framework.GetShortFolderName()}/any/consoledemo{extension}");
                }
            }

            var getValuesCommand = new GetValuesCommand(
               Log,
               _testRoot,
               _targetFrameworkOrFrameworks,
               "RunCommand",
               GetValuesCommand.ValueType.Property);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  If multi-targeted, we need to specify which target framework to get the value for
                string[] args = multiTarget ? new[] { $"/p:TargetFramework={_targetFrameworkOrFrameworks}" } : Array.Empty<string>();
                getValuesCommand.Execute(args)
                    .Should().Pass();
                string runCommandPath = getValuesCommand.GetValues().Single();
                Path.GetExtension(runCommandPath)
                    .Should().Be(extension);
                File.Exists(runCommandPath).Should()
                    .BeTrue("run command should be apphost executable (for WinExe) to debug. But it will not be packed");
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

        [Fact]
        public void Given_targetplatform_set_It_should_error()
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool")
                .WithSource()
                .WithTargetFramework($"{ToolsetInfo.CurrentTargetFramework}-windows");

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Fail().And.HaveStdOutContaining("NETSDK1146");

        }
    }
}
