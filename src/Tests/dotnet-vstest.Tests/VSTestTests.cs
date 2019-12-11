// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.VSTest.Tests
{
    public class VSTestTests : SdkTest
    {
        public VSTestTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        {
            var testAppName = "VSTestCore";
            var testRoot = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables()
                .Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testRoot)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, "netcoreapp3.0", $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");
            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 2")
                    .And.Contain("Passed: 1")
                    .And.Contain("Failed: 1")
                    .And.Contain("\u221a VSTestPassTest")
                    .And.Contain("X VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }
    }
}
