// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackASimpleLibrary : SdkTest
    {
        public GivenThatWeWantToPackASimpleLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.1.0.60101")]
        public void It_packs_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var packCommand = new PackCommand(testAsset);

            packCommand
                .Execute()
                .Should()
                .Pass();

            var packageDirectory = packCommand.GetPackageDirectory();
            packageDirectory.Should().OnlyHaveFiles(new[]
            {
                "HelloWorld.1.0.0.nupkg",
            }, SearchOption.TopDirectoryOnly);

            var outputDirectory = packCommand.GetOutputDirectory();
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"HelloWorld.dll",
                $"HelloWorld.pdb",
                $"HelloWorld.deps.json",
                $"HelloWorld.runtimeconfig.json",
                $"HelloWorld{EnvironmentInfo.ExecutableExtension}",
            });
        }
    }
}
