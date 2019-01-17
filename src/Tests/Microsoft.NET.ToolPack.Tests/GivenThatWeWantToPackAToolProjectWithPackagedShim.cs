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
using NuGet.Frameworks;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithPackagedShim : SdkTest
    {
        private string _testRoot;
        private string _packageId;
        private string _packageVersion = "1.0.0";
        private const string _customToolCommandName = "customToolCommandName";

        public GivenThatWeWantToPackAToolProjectWithPackagedShim(ITestOutputHelper log) : base(log)
        {
        }

        private string SetupNuGetPackage(
            bool multiTarget,
            [CallerMemberName] string callingMethod = "",
            Dictionary<string, string> additionalProperty = null)
        {
            TestAsset helloWorldAsset = CreateTestAsset(multiTarget, callingMethod, additionalProperty);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute().Should().Pass();
            _packageId = Path.GetFileNameWithoutExtension(packCommand.ProjectFile);

            return packCommand.GetNuGetPackage(packageVersion: _packageVersion);
        }

        private TestAsset CreateTestAsset(
            bool multiTarget,
            string uniqueName,
            Dictionary<string, string> additionalProperty = null)
        {
            return _testAssetsManager
                .CopyTestAsset("PortableTool", uniqueName)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", "win-x64;osx.10.12-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));

                    if (additionalProperty != null)
                    {
                        foreach (KeyValuePair<string, string> pair in additionalProperty)
                        {
                            propertyGroup.Add(new XElement(ns + pair.Key, pair.Value));
                        }
                    }

                    if (multiTarget)
                    {
                        propertyGroup.Element(ns + "TargetFramework").Remove();
                        propertyGroup.Add(new XElement(ns + "TargetFrameworks", "netcoreapp2.1"));
                    }
                })
                .Restore(Log);
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
        public void It_contains_dependencies_dll(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Newtonsoft.Json.dll");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_shim(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/win-x64/{_customToolCommandName}.exe",
                        "Name should be the same as the command name even customized");
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/osx.10.12-x64/{_customToolCommandName}",
                        "RID should be the exact match of the RID in the property, even Apphost only has version of win, osx and linux");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_uses_customized_PackagedShimOutputRootDirectory(bool multiTarget)
        {
            string shimoutputPath = Path.Combine(TestContext.Current.TestExecutionDirectory, "shimoutput");
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", "PackagedShimOutputRootDirectory" + multiTarget.ToString())
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", "win-x64;osx.10.12-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));
                    propertyGroup.Add(new XElement(ns + "PackagedShimOutputRootDirectory", shimoutputPath));

                    if (multiTarget)
                    {
                        propertyGroup.Element(ns + "TargetFramework").Remove();
                        propertyGroup.Add(new XElement(ns + "TargetFrameworks", "netcoreapp2.1"));
                    }
                })
                .Restore(Log);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute().Should().Pass();

            string windowShimPath = Path.Combine(shimoutputPath, $"shims/netcoreapp2.1/win-x64/{_customToolCommandName}.exe");
            File.Exists(windowShimPath).Should().BeTrue($"Shim {windowShimPath} should exist");
            string osxShimPath = Path.Combine(shimoutputPath, $"shims/netcoreapp2.1/osx.10.12-x64/{_customToolCommandName}");
            File.Exists(osxShimPath).Should().BeTrue($"Shim {osxShimPath} should exist");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_uses_outputs_to_bin_by_default(bool multiTarget)
        {
            TestAsset helloWorldAsset = SetUpHelloWorld(multiTarget);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);
            var outputDirectory = packCommand.GetOutputDirectory("netcoreapp2.1");
            packCommand.Execute().Should().Pass();

            string windowShimPath = Path.Combine(outputDirectory.FullName, $"shims/netcoreapp2.1/win-x64/{_customToolCommandName}.exe");
            File.Exists(windowShimPath).Should().BeTrue($"Shim {windowShimPath} should exist");
            string osxShimPath = Path.Combine(outputDirectory.FullName, $"shims/netcoreapp2.1/osx.10.12-x64/{_customToolCommandName}");
            File.Exists(osxShimPath).Should().BeTrue($"Shim {osxShimPath} should exist");
        }

        private TestAsset SetUpHelloWorld(bool multiTarget, [CallerMemberName] string callingMethod = "")
        {
            return _testAssetsManager
                .CopyTestAsset("PortableTool", callingMethod + multiTarget)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", "win-x64;osx.10.12-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));

                    if (multiTarget)
                    {
                        propertyGroup.Element(ns + "TargetFramework").Remove();
                        propertyGroup.Add(new XElement(ns + "TargetFrameworks", "netcoreapp2.1"));
                    }
                })
                .Restore(Log);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Clean_should_remove_bin_output(bool multiTarget)
        {
            TestAsset helloWorldAsset = SetUpHelloWorld(multiTarget);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);
            packCommand.Execute().Should().Pass();

            var cleanCommand = new CleanCommand(Log, helloWorldAsset.TestRoot);
            cleanCommand.Execute().Should().Pass();

            var outputDirectory = packCommand.GetOutputDirectory("netcoreapp2.1");
            string windowShimPath = Path.Combine(outputDirectory.FullName, $"shims/netcoreapp2.1/win-x64/{_customToolCommandName}.exe");
            File.Exists(windowShimPath).Should().BeFalse($"Shim {windowShimPath} should not exists");
            string osxShimPath = Path.Combine(outputDirectory.FullName, $"shims/netcoreapp2.1/osx.10.12-x64/{_customToolCommandName}");
            File.Exists(osxShimPath).Should().BeFalse($"Shim {osxShimPath} should not exists");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Generate_shims_runs_incrementaly(bool multiTarget)
        {
            TestAsset helloWorldAsset = SetUpHelloWorld(multiTarget);

            _testRoot = helloWorldAsset.TestRoot;

            var buildCommand = new BuildCommand(Log, helloWorldAsset.TestRoot);
            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp2.1");
            string windowShimPath = Path.Combine(outputDirectory.FullName, $"shims/netcoreapp2.1/win-x64/{_customToolCommandName}.exe");

            DateTime windowShimPathFirstModifiedTime = File.GetLastWriteTimeUtc(windowShimPath);

            buildCommand.Execute().Should().Pass();

            DateTime windowShimPathSecondModifiedTime = File.GetLastWriteTimeUtc(windowShimPath);

            windowShimPathSecondModifiedTime.Should().Be(windowShimPathFirstModifiedTime);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_shim_with_no_build(bool multiTarget)
        {
            var testAsset = CreateTestAsset(multiTarget, "shim_with_no_build" + multiTarget);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand.Execute().Should().Pass();

            var packCommand = new PackCommand(Log, testAsset.TestRoot);

            packCommand.Execute("/p:NoBuild=true").Should().Pass();
            var nugetPackage = packCommand.GetNuGetPackage();

            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/win-x64/{_customToolCommandName}.exe",
                        "Name should be the same as the command name even customized");
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/osx.10.12-x64/{_customToolCommandName}",
                        "RID should be the exact match of the RID in the property, even Apphost only has version of win, osx and linux");
                }
            }
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_produces_valid_shims(bool multiTarget)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                // only sample test on win-x64 since shims are RID specific
                return;
            }

            var nugetPackage = SetupNuGetPackage(multiTarget);
            AssertValidShim(_testRoot, nugetPackage);
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_produces_valid_shims_when_the_first_build_is_wrong(bool multiTarget)
        {
            // The first build use wrong package id and should embed wrong string to shims. However, the pack should produce correct shim
            // since it includes build target. And the incremental build should consider the shim to be invalid and recreate that.

            if (!Environment.Is64BitOperatingSystem)
            {
                // only sample test on win-x64 since shims are RID specific
                return;
            }

            TestAsset helloWorldAsset = CreateTestAsset(multiTarget, "It_produces_valid_shims2" + multiTarget.ToString());

            var testRoot = helloWorldAsset.TestRoot;

            var buildCommand = new BuildCommand(Log, helloWorldAsset.TestRoot);
            buildCommand.Execute("/p:PackageId=wrongpackagefirstbuild");

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute().Should().Pass();
            var nugetPackage = packCommand.GetNuGetPackage();

            _packageId = Path.GetFileNameWithoutExtension(packCommand.ProjectFile);

            AssertValidShim(testRoot, nugetPackage);
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_version_and_packageVersion_is_different_It_produces_valid_shims(bool multiTarget)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                // only sample test on win-x64 since shims are RID specific
                return;
            }

            var nugetPackage = SetupNuGetPackage(multiTarget,
                additionalProperty: new Dictionary<string, string>()
                {
                    ["version"] = "1.0.0-rtm",
                    ["packageVersion"] = _packageVersion
                });

            AssertValidShim(_testRoot, nugetPackage);
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_version_and_packageVersion_is_different_It_produces_valid_shims2(bool multiTarget)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                // only sample test on win-x64 since shims are RID specific
                return;
            }

            _packageVersion = "1000.0.0";

            var nugetPackage = SetupNuGetPackage(multiTarget,
                additionalProperty: new Dictionary<string, string>()
                {
                    ["version"] = "1000",
                });

            AssertValidShim(_testRoot, nugetPackage);
        }

        private void AssertValidShim(string testRoot, string nugetPackage)
        {
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();
                var simulateToolPathRoot = Path.Combine(testRoot, "temp", Path.GetRandomFileName());

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    string[] portableAppContent = {
                        "consoledemo.runtimeconfig.json",
                        "consoledemo.deps.json",
                        "consoledemo.dll",
                        "Newtonsoft.Json.dll"};
                    CopyPackageAssetToToolLayout(portableAppContent, nupkgReader, simulateToolPathRoot, framework);

                    string shimPath = Path.Combine(simulateToolPathRoot, $"{_customToolCommandName}.exe");
                    nupkgReader.ExtractFile(
                        $"tools/{framework.GetShortFolderName()}/any/shims/win-x64/{_customToolCommandName}.exe",
                        shimPath,
                        null);

                    var command = new ShimCommand(Log, shimPath)
                    {
                        WorkingDirectory = simulateToolPathRoot
                    };
                    command.Execute().Should()
                      .Pass()
                      .And
                      .HaveStdOutContaining("Hello World from Global Tool");
                }
            }
        }

        private void CopyPackageAssetToToolLayout(
            string[] nupkgAssetNames,
            PackageArchiveReader nupkgReader,
            string tmpfilePathRoot,
            NuGetFramework framework)
        {
            var toolLayoutDirectory =
                Path.Combine(
                    tmpfilePathRoot,
                    ".store",
                    _packageId,
                    _packageVersion,
                    _packageId,
                    _packageVersion,
                    "tools",
                    framework.GetShortFolderName(),
                    "any");

            foreach (string nupkgAssetName in nupkgAssetNames)
            {
                var destinationFilePath =
                    Path.Combine(toolLayoutDirectory, nupkgAssetName);
                nupkgReader.ExtractFile(
                    $"tools/{framework.GetShortFolderName()}/any/{nupkgAssetName}",
                    destinationFilePath,
                    null);
            }
        }
    }
}
