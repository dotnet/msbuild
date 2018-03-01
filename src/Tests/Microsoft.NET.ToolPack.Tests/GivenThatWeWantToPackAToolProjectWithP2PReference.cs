// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithP2PReference : SdkTest
    {
        public GivenThatWeWantToPackAToolProjectWithP2PReference(ITestOutputHelper log) : base(log)
        {
        }

        private string SetupNuGetPackage([CallerMemberName] string callingMethod = "")
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("PortableToolWithP2P", callingMethod)
                .WithSource()
                .Restore(Log, "App");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "App");
            var packCommand = new PackCommand(Log, appProjectDirectory);

            packCommand.Execute();

            return packCommand.GetNuGetPackage();
        }

        [Fact]
        public void It_packs_successfully()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [Fact]
        public void It_contains_dependencies_dll()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Library.dll");
                }
            }
        }
    }
}
