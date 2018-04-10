// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackACrossTargetedLibrary : SdkTest
    {
        public GivenThatWeWantToPackACrossTargetedLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_packs_nondesktop_library_successfully_on_all_platforms()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore(Log, "NetStandardAndNetCoreApp");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "NetStandardAndNetCoreApp");

            new PackCommand(Log, libraryProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = new DirectoryInfo(Path.Combine(libraryProjectDirectory, "bin", "Debug"));
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "NetStandardAndNetCoreApp.1.0.0.nupkg",
                "netcoreapp1.1/NetStandardAndNetCoreApp.dll",
                "netcoreapp1.1/NetStandardAndNetCoreApp.pdb",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.dev.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.deps.json",
                "netstandard1.5/NetStandardAndNetCoreApp.dll",
                "netstandard1.5/NetStandardAndNetCoreApp.pdb",
                "netstandard1.5/NetStandardAndNetCoreApp.deps.json"
            });
        }
    }
}
