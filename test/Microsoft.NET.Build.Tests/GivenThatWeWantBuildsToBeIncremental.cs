// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantBuildsToBeIncremental : SdkTest
    {
        public GivenThatWeWantBuildsToBeIncremental(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GenerateBuildRuntimeConfigurationFiles_runs_incrementaly()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1").FullName;
            var runtimeConfigDevJsonPath = Path.Combine(outputDirectory, "HelloWorld.runtimeconfig.dev.json");

            buildCommand.Execute().Should().Pass();
            DateTime runtimeConfigDevJsonFirstModifiedTime = new FileInfo(runtimeConfigDevJsonPath).LastWriteTime;

            buildCommand.Execute().Should().Pass();
            DateTime runtimeConfigDevJsonSecondModifiedTime = new FileInfo(runtimeConfigDevJsonPath).LastWriteTime;

            runtimeConfigDevJsonSecondModifiedTime.Should().Be(runtimeConfigDevJsonFirstModifiedTime);
        }
    }
}
