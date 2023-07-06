// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var outputFolder = new DirectoryInfo(OutputPathCalculator.FromProject(testInstance.Path).GetOutputDirectory(configuration: configuration, runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64"));

            outputFolder.Should().NotBeEmpty();

            new DotnetCommand(Log, "clean", testInstance.Path)
                .Execute("-r", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should().Pass();

            outputFolder.Should().BeEmpty();
        }
    }
}
