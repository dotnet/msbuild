// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsCsproj : TestBase
    {
        [Fact]
        public void It_builds_a_runnable_output()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = testInstance.Root.GetDirectory("bin", configuration, "netcoreapp1.0")
                .GetFile($"{testAppName}.dll");

            var outputRunCommand = new TestCommand("dotnet");

            outputRunCommand.ExecuteWithCapturedOutput(outputDll.FullName)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }
    }
}
