// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.List;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Workloads.Workload.Restore;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Restore.Tests
{
    public class DiscoverAllProjectsTests : SdkTest
    {
        public DiscoverAllProjectsTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenCallWithNoSlnOrProjectArgumentItCollectProjectsFromSolution()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var result = WorkloadRestoreCommand.DiscoverAllProjects(projectDirectory);
            result.Should().Contain(f => Path.GetFileName(f) == "App.csproj");
        }

        [Fact]
        public void WhenCallWithSlnOrProjectArgumentItCollectProjectsFromSolution()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var result =
                WorkloadRestoreCommand.DiscoverAllProjects("",
                    new[]
                    {
                        Path.Combine(projectDirectory, "App.sln"),
                        Path.Combine(projectDirectory, "Lib", "Lib.csproj")
                    });

            // current directory is ignored
            result.Should().Contain(f => Path.GetFileName(f) == "App.csproj", "from checking the sln file");
            result.Should().Contain(f => Path.GetFileName(f) == "Lib.csproj", "from directly pass in");
        }

        [Fact]
        public void WhenCallWithDirectoryWith2ProjectItShouldFindAll()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var result =
                WorkloadRestoreCommand.DiscoverAllProjects(Path.Combine(projectDirectory, "Multiple"));

            result.Should().Contain(f => Path.GetFileName(f) == "First.csproj");
            result.Should().Contain(f => Path.GetFileName(f) == "Second.csproj");
        }

        [Fact]
        public void WhenCallWithSlnContainingSolutionFolderItExcludesFolderProjectsFromSolution()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndSolutionFolders")
                .WithSource()
                .Path;

            var result =
                WorkloadRestoreCommand.DiscoverAllProjects("",
                    new[]
                    {
                        Path.Combine(projectDirectory, "App.sln"),
                    });

            // 'src' solution folder is filtered out
            result.Should().Contain(f => Path.GetFileName(f) == "App.csproj", "from checking the sln file");
            result.Count.Should().Be(1);
        }
    }
}
