// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRootEnv : SdkTest
    {
        public GivenDotnetRootEnv(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItShouldSetDotnetRootToDirectoryOfMuxer()
        {
            string expectDotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string expectOutput = GetExpectOutput(expectDotnetRoot);

            var projectRoot = SetupDotnetRootEchoProject();

            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");

            runCommand.Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(expectOutput);
        }

        [Fact]
        public void WhenDotnetRootIsSetItShouldSetDotnetRootToDirectoryOfMuxer()
        {
            string expectDotnetRoot = "OVERRIDE VALUE";

            var projectRoot = SetupDotnetRootEchoProject();

            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            if (Environment.Is64BitProcess)
            {
                runCommand = runCommand.WithEnvironmentVariable("DOTNET_ROOT", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");
            }
            else
            {
                runCommand = runCommand.WithEnvironmentVariable("DOTNET_ROOT(x86)", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            }

            runCommand
                .Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(GetExpectOutput(expectDotnetRoot));
        }

        private string SetupDotnetRootEchoProject([CallerMemberName] string callingMethod = null)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppEchoDotnetRoot", callingMethod)
                .WithSource()
                .Restore(Log);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            return testAsset.Path;
        }

        private static string GetExpectOutput(string expectDotnetRoot)
        {
            string expectOutput;
            if (Environment.Is64BitProcess)
            {
                expectOutput = @$"DOTNET_ROOT='{expectDotnetRoot}';DOTNET_ROOT(x86)=''";
            }
            else
            {
                expectOutput = @$"DOTNET_ROOT='';DOTNET_ROOT(x86)='{expectDotnetRoot}'";
            }

            return expectOutput;
        }
    }
}
