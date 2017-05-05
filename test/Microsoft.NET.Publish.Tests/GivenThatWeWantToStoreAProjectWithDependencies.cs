// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToStoreAProjectWithDependencies : SdkTest
    {
        private static readonly string _libPrefix = FileConstants.DynamicLibPrefix;
        private static string _runtimeOs;
        private static string _runtimeLibOs;
        private static string _runtimeRid;
        private static string _testArch;
        private static string _tfm = "netcoreapp1.0";

        static GivenThatWeWantToStoreAProjectWithDependencies()
        {
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _runtimeOs = "win7";
                _runtimeLibOs = "win";
                _testArch = rid.Substring(rid.LastIndexOf("-") + 1);
                _runtimeRid = "win7-" + _testArch;
            }
            else
            {
                _runtimeOs = "unix";
                _runtimeLibOs = "unix";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // microsoft.netcore.coredistools only has assets for osx.10.10
                    _runtimeRid = "osx.10.10-x64";
                }
                else if (rid.Contains("ubuntu"))
                {
                    // microsoft.netcore.coredistools only has assets for ubuntu.14.04-x64
                    _runtimeRid = "ubuntu.14.04-x64";
                }
                else
                {
                    _runtimeRid = rid;
                }
            }
        }

        [Fact]
        public void compose_dependencies()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleStore")
                .WithSource();

            ComposeStore storeCommand = new ComposeStore(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleStore.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            storeCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true", "/p:PreserveComposeWorkingDir=true")
                .Should()
                .Pass();
            DirectoryInfo storeDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/{_libPrefix}coredistools{FileConstants.DynamicLibSuffix}",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/coredistools.h"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{FileConstants.DynamicLibSuffix}");
            }
            storeDirectory.Should().OnlyHaveFiles(files_on_disk);

            //valid artifact.xml
            var knownpackage = new HashSet<PackageIdentity>();

            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Targets", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("System.Private.Uri", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.CoreDisTools", NuGetVersion.Parse("1.0.1-prerelease-00001")));
            knownpackage.Add(new PackageIdentity($"runtime.{_runtimeOs}.System.Private.Uri", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Platforms", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity($"runtime.{_runtimeRid}.Microsoft.NETCore.CoreDisTools", NuGetVersion.Parse("1.0.1-prerelease-00001")));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                knownpackage.Add(new PackageIdentity("runtime.native.System", NuGetVersion.Parse("4.4.0-beta-24821-02")));
                knownpackage.Add(new PackageIdentity($"runtime.{_runtimeRid}.runtime.native.System", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            }

            var artifact = Path.Combine(OutputFolder, "artifact.xml");
            HashSet<PackageIdentity> packagescomposed = ParseStoreArtifacts(artifact);

            packagescomposed.Count.Should().Be(knownpackage.Count);

            foreach (var pkg in packagescomposed)
            {
                knownpackage.Should().Contain(elem => elem.Equals(pkg), "package {0}, version {1} was not expected to be stored", pkg.Id, pkg.Version);
            }

        }
        [Fact]
        public void compose_with_fxfiles()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleStore")
                .WithSource();


            ComposeStore storeCommand = new ComposeStore(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleStore.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            storeCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true", "/p:SkipRemovingSystemFiles=true")
                .Should()
                .Pass();

            DirectoryInfo storeDirectory = new DirectoryInfo(OutputFolder);
            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/{_libPrefix}coredistools{FileConstants.DynamicLibSuffix}",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/coredistools.h",
               $"runtime.{_runtimeOs}.system.private.uri/4.4.0-beta-24821-02/runtimes/{_runtimeLibOs}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{FileConstants.DynamicLibSuffix}");
            }
            storeDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void compose_dependencies_noopt()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleStore")
                .WithSource();


            ComposeStore storeCommand = new ComposeStore(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleStore.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            storeCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true", "/p:SkipOptimization=true", $"/p:ComposeWorkingDir={WorkingDir}", "/p:PreserveComposeWorkingDir=true")
                .Should()
                .Pass();

            DirectoryInfo storeDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/{_libPrefix}coredistools{FileConstants.DynamicLibSuffix}",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/coredistools.h",
               $"runtime.{_runtimeOs}.system.private.uri/4.4.0-beta-24821-02/runtimes/{_runtimeLibOs}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{FileConstants.DynamicLibSuffix}");
            }

            storeDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void store_nativeonlyassets()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("UnmanagedStore")
                .WithSource();

            ComposeStore storeCommand = new ComposeStore(Stage0MSBuild, simpleDependenciesAsset.TestRoot);

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");
            storeCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeWorkingDir={WorkingDir}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();

            DirectoryInfo storeDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/{_libPrefix}coredistools{FileConstants.DynamicLibSuffix}",
               $"runtime.{_runtimeRid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{_runtimeRid}/native/coredistools.h"
               };

            storeDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void compose_multifile()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("TargetManifests", "multifile")
                .WithSource();

            ComposeStore storeCommand = new ComposeStore(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "NewtonsoftFilterProfile.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "o");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");
            var additonalproj1 = Path.Combine(simpleDependenciesAsset.TestRoot, "NewtonsoftMultipleVersions.xml");
            var additonalproj2 = Path.Combine(simpleDependenciesAsset.TestRoot, "FluentAssertions.xml");

            storeCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:Additionalprojects={additonalproj1}%3b{additonalproj2}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();
            DirectoryInfo storeDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               @"newtonsoft.json/9.0.2-beta2/lib/netstandard1.1/Newtonsoft.Json.dll",
               @"newtonsoft.json/9.0.1/lib/netstandard1.0/Newtonsoft.Json.dll",
               @"fluentassertions.json/4.12.0/lib/netstandard1.3/FluentAssertions.Json.dll"
               };

            storeDirectory.Should().HaveFiles(files_on_disk);

            var knownpackage = new HashSet<PackageIdentity>();

            knownpackage.Add(new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("9.0.1")));
            knownpackage.Add(new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("9.0.2-beta2")));
            knownpackage.Add(new PackageIdentity("FluentAssertions.Json", NuGetVersion.Parse("4.12.0")));

            var artifact = Path.Combine(OutputFolder, "artifact.xml");
            var packagescomposed = ParseStoreArtifacts(artifact);

            packagescomposed.Count.Should().BeGreaterThan(0);

            foreach (var pkg in knownpackage)
            {
                packagescomposed.Should().Contain(elem => elem.Equals(pkg), "package {0}, version {1} was not expected to be stored", pkg.Id, pkg.Version);
            }
        }

        [Fact]
        public void It_uses_star_versions_correctly()
        {
            TestAsset targetManifestsAsset = _testAssetsManager
                .CopyTestAsset("TargetManifests")
                .WithSource();

            var outputFolder = Path.Combine(targetManifestsAsset.TestRoot, "o");
            var workingDir = Path.Combine(targetManifestsAsset.TestRoot, "w");

            new ComposeStore(Stage0MSBuild, targetManifestsAsset.TestRoot, "StarVersion.xml")
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={outputFolder}", $"/p:ComposeWorkingDir={workingDir}", "/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();

            var artifactFile = Path.Combine(outputFolder, "artifact.xml");
            var storeArtifacts = ParseStoreArtifacts(artifactFile);

            var nugetPackage = storeArtifacts.Single(p => string.Equals(p.Id, "NuGet.Common", StringComparison.OrdinalIgnoreCase));

            // nuget.org/packages/NuGet.Common currently contains:
            // 4.0.0
            // 4.0.0-rtm-2283
            // 4.0.0-rtm-2265
            // 4.0.0-rc3
            // 4.0.0-rc2
            // 4.0.0-rc-2048
            //
            // and the StarVersion.xml uses Version="4.0.0-*", 
            // so we expect a version greater than 4.0.0-rc2, since there is
            // a higher version on the feed that meets the criteria
            nugetPackage.Version.Should().BeGreaterThan(NuGetVersion.Parse("4.0.0-rc2"));

            // work around https://github.com/dotnet/sdk/issues/1045. The unnecessary assets getting
            // put in the store folder cause long path issues, so delete them
            foreach (var runtimeFolder in new DirectoryInfo(outputFolder).GetDirectories("runtime.*"))
            {
                runtimeFolder.Delete(true);
            }
        }

        [Fact]
        public void It_creates_profiling_symbols()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // profiling symbols are not supported on OSX
                return;
            }

            if (UsingFullFrameworkMSBuild)
            {
                //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
                //  See https://github.com/dotnet/sdk/issues/1077
                return;
            }

            TestAsset targetManifestsAsset = _testAssetsManager
                .CopyTestAsset("TargetManifests")
                .WithSource();

            var outputFolder = Path.Combine(targetManifestsAsset.TestRoot, "o");
            var workingDir = Path.Combine(targetManifestsAsset.TestRoot, "w");

            new ComposeStore(Stage0MSBuild, targetManifestsAsset.TestRoot, "NewtonsoftFilterProfile.xml")
                .Execute(
                    $"/p:RuntimeIdentifier={_runtimeRid}",
                    "/p:TargetFramework=netcoreapp2.0",
                    $"/p:ComposeDir={outputFolder}",
                    $"/p:ComposeWorkingDir={workingDir}",
                    "/p:DoNotDecorateComposeDir=true",
                    "/p:PreserveComposeWorkingDir=true",
                    "/p:CreateProfilingSymbols=true")
                .Should()
                .Pass();

            var symbolFileExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ni.pdb" : ".map";
            var symbolsFolder = new DirectoryInfo(Path.Combine(outputFolder, "symbols"));

            var newtonsoftSymbolsFolder = symbolsFolder.Sub("newtonsoft.json").Sub("9.0.1").Sub("lib").Sub("netstandard1.0");
            newtonsoftSymbolsFolder.Should().Exist();

            var newtonsoftSymbolsFiles = newtonsoftSymbolsFolder.GetFiles().ToArray();
            newtonsoftSymbolsFiles.Length.Should().Be(1);
            newtonsoftSymbolsFiles[0].Name.Should().StartWith("Newtonsoft.Json").And.EndWith(symbolFileExtension);
        }

        private static HashSet<PackageIdentity> ParseStoreArtifacts(string path)
        {
            return new HashSet<PackageIdentity>(
                from element in XDocument.Load(path).Root.Elements("Package")
                select new PackageIdentity(
                    element.Attribute("Id").Value,
                    NuGetVersion.Parse(element.Attribute("Version").Value)));
        }
    }
}
