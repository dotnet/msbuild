// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;

namespace Microsoft.DotNet.Cli.Clean.Tests
{
    public class GivenDotnetCleanCleansBuildArtifacts : TestBase
    {
        [Fact]
        public void ItCleansAProjectBuiltWithRuntimeIdentifier()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithRuntime("win7-x64")
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputFolder = testInstance.Root.GetDirectory("bin", configuration, "netcoreapp2.2", "win7-x64");

            outputFolder.Should().NotBeEmpty();

            new CleanCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute("-r win7-x64")
                .Should().Pass();

            outputFolder.Should().BeEmpty();
        }
    }
}