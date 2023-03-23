// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;


namespace Microsoft.DotNet.Cli.Clean.Tests
{
    public class GivenDotnetCleanCleansBuildArtifacts : SdkTest
    {
        public GivenDotnetCleanCleansBuildArtifacts(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItCleansAProjectBuiltWithRuntimeIdentifier()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log, testInstance.Path)
                .Execute("-r", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputFolder = new DirectoryInfo(Path.Combine(testInstance.Path, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64"));

            outputFolder.Should().NotBeEmpty();

            new DotnetCommand(Log, "clean", testInstance.Path)
                .Execute("-r", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should().Pass();

            outputFolder.Should().BeEmpty();
        }
    }
}
