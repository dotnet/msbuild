// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Build3.Tests
{
    public class GivenDotnetBuild3BuildsCsproj : TestBase
    {
        [Fact]
        public void It_builds_a_runnable_output()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName)
                .WithLockFiles();

            var testProjectDirectory = testInstance.TestRoot;

            new Build3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp1.0", $"{testAppName}.dll");
            var outputRunCommand = new TestCommand("dotnet");

            outputRunCommand.ExecuteWithCapturedOutput(outputDll)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }
    }
}
