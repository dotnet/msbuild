// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using System.IO;
using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnettestBuildsAndRunsTestFromDll : SdkTest
    {
        public GivenDotnettestBuildsAndRunsTestFromDll(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [Fact]
        public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        {
            var testAppName = "VSTestCore";
            var testAsset = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(testAsset)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");
            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 2")
                    .And.Contain("Passed: 1")
                    .And.Contain("Failed: 1")
                    .And.Contain("Passed VSTestPassTest")
                    .And.Contain("Failed VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItSetsDotnetRootToTheLocationOfDotnetExecutableWhenRunningDotnetTestWithDll()
        {
            var testAppName = "VSTestCore";
            var testAsset = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(testAsset)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{testAppName}.dll");

            // Call dotnet test + dll
            var result = new DotnetTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");

            result.ExitCode.Should().Be(1);
            var dotnet = result.StartInfo.FileName;
            Path.GetFileNameWithoutExtension(dotnet).Should().Be("dotnet");
            string dotnetRoot = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            result.StartInfo.EnvironmentVariables.ContainsKey(dotnetRoot).Should().BeTrue($"because {dotnetRoot} should be set");
            result.StartInfo.EnvironmentVariables[dotnetRoot].Should().Be(Path.GetDirectoryName(dotnet));
        }

        [Fact]
        public void TestsFromAGivenContainerAndArchSwitchShouldFlowToVsTestConsole()
        {
            var testAppName = "VSTestCore";
            var testAsset = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(testAsset)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetTestCommand(Log)
                .Execute(outputDll, "--arch", "wrongArchitecture");
            if (!TestContext.IsLocalized())
            {
                result.StdErr.Should().StartWith("Invalid platform type: wrongArchitecture. Valid platform types are ");
            }

            result.ExitCode.Should().Be(1);
        }
    }
}
