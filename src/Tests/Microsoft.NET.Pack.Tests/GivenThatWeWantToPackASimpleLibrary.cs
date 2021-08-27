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

        [Fact]
        public void It_packs_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            new PackCommand(Log, testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            

            var outputDirectory = new DirectoryInfo(Path.Combine(testAsset.TestRoot, "bin", "Debug"));
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.1.0.0.nupkg",
                $"{ToolsetInfo.CurrentTargetFramework}/HelloWorld.dll",
                $"{ToolsetInfo.CurrentTargetFramework}/HelloWorld.pdb",
                $"{ToolsetInfo.CurrentTargetFramework}/HelloWorld.deps.json",
                $"{ToolsetInfo.CurrentTargetFramework}/HelloWorld.runtimeconfig.json",
                $"{ToolsetInfo.CurrentTargetFramework}/HelloWorld{EnvironmentInfo.ExecutableExtension}",
                $"{ToolsetInfo.CurrentTargetFramework}/ref/HelloWorld.dll"
            });
        }
    }
}
