// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Runtime.InteropServices;
using Microsoft.DotNet.InternalAbstractions;
namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToCacheAProjectWithDependencies : SdkTest
    {
        [Fact]
        public void compose_dependencies()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();


            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            var tfm = "netcoreapp1.0";
            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            cacheCommand
                .Execute($"/p:RuntimeIdentifier={rid}", $"/p:TargetFramework={tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            string libPrefix = "";
            string runtimeos = "win7";
            string runtimelibos = "win";
            string arch = rid.Substring(rid.LastIndexOf("-") + 1);
            string runtimerid = "win7-" + arch;

            if (! RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libPrefix = "lib";
                runtimeos = "unix";
                runtimelibos = "unix";
                runtimerid = rid;
            }

           
            List<string> files_on_disk = new List < string > {
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/{libPrefix}coredistools{Constants.DynamicLibSuffix}",
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/coredistools.h"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && arch != "x86")
            {
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native{Constants.DynamicLibSuffix}");
            }
            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }
        [Fact]
        public void compose_with_fxfiles()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();


            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            var tfm = "netcoreapp1.0";
            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            cacheCommand
                .Execute($"/p:RuntimeIdentifier={rid}", $"/p:TargetFramework={tfm}", $"/p:ComposeDir={OutputFolder}", "/p:DoNotDecorateComposeDir=true", "/p:SkipRemovingSystemFiles=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            string libPrefix = "";
            string runtimeos = "win7";
            string runtimelibos = "win";
            string arch = rid.Substring(rid.LastIndexOf("-") + 1);
            string runtimerid = "win7-" + arch;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libPrefix = "lib";
                runtimeos = "unix";
                runtimelibos = "unix";
                runtimerid = rid;
            }


            List<string> files_on_disk = new List<string> {
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/{libPrefix}coredistools{Constants.DynamicLibSuffix}",
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/coredistools.h",
               $"runtime.{runtimeos}.system.private.uri/4.4.0-beta-24821-02/runtimes/{runtimelibos}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && arch != "x86")
            {
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native{Constants.DynamicLibSuffix}");
            }
            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void compose_dependencies_noopt()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();


            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            var tfm = "netcoreapp1.0";
            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "composedir");
            cacheCommand
                .Execute($"/p:RuntimeIdentifier={rid}", $"/p:TargetFramework={tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true", "/p:SkipOptimization=true", $"/p:ComposeWorkingDir={WorkingDir}", "/p:PreserveComposeWorkingDir=true")
                .Should()
                .Pass();
                        
            DirectoryInfo workingDirectory = new DirectoryInfo(WorkingDir);
            workingDirectory.Should().HaveFile("project.assets.json");

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            string libPrefix = "";
            string runtimeos = "win7";
            string runtimelibos = "win";
            string arch = rid.Substring(rid.LastIndexOf("-") + 1);
            string runtimerid = "win7-" + arch;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libPrefix = "lib";
                runtimeos = "unix";
                runtimelibos = "unix";
                runtimerid = rid;
            }


            List<string> files_on_disk = new List<string> {
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/{libPrefix}coredistools{Constants.DynamicLibSuffix}",
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/coredistools.h",
               $"runtime.{runtimeos}.system.private.uri/4.4.0-beta-24821-02/runtimes/{runtimelibos}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && arch != "x86")
            {
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{runtimerid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{runtimerid}/native/System.Native{Constants.DynamicLibSuffix}");
            }

            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void cache_nativeonlyassets()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("UnmanagedCache")
                .WithSource();

            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            string libPrefix = "";
            string arch = rid.Substring(rid.LastIndexOf("-") + 1);
            string runtimerid = "win7-" + arch;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libPrefix = "lib";
                runtimerid = rid;
            }

            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
           
            var tfm = "netcoreapp1.0";
            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            cacheCommand
                .Execute($"/p:RuntimeIdentifier={runtimerid}", $"/p:TargetFramework={tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

           


            List<string> files_on_disk = new List<string> {
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/{libPrefix}coredistools{Constants.DynamicLibSuffix}",
               $"runtime.{runtimerid}.microsoft.netcore.coredistools/1.0.1-prerelease-00001/runtimes/{runtimerid}/native/coredistools.h"
               };

            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);


        }
    }
}
