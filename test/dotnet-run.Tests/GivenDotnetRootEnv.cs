// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRootEnv : TestBase
    {
        private readonly Lazy<string> _dotnetRootEchoProject = new Lazy<string>(() => SetupDotnetRootEchoProject());

        [Fact]
        public void ItShouldSetDotnetRootToDirectoryOfMuxer()
        {
            string expectDotnetRoot = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
            string expectOutput = GetExpectOutput(expectDotnetRoot);

            new RunCommand()
                .WithWorkingDirectory(_dotnetRootEchoProject.Value)
                .WithRemovingEnvironmentVariable("DOTNET_ROOT", "DOTNET_ROOT(x86)")
                .WithoutSettingDotnetRootEnvironmentVariable()
                .ExecuteWithCapturedOutput("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(expectOutput);
        }

        [Fact]
        public void WhenDotnetRootIsSetItShouldSetDotnetRootToDirectoryOfMuxer()
        {
            string expectDotnetRoot = "OVERRIDE VALUE";

            new RunCommand()
                .WithWorkingDirectory(_dotnetRootEchoProject.Value)
                .WithRemovingEnvironmentVariable("DOTNET_ROOT", "DOTNET_ROOT(x86)")
                .WithoutSettingDotnetRootEnvironmentVariable()
                .WithEnvironmentVariable(Environment.Is64BitProcess? "DOTNET_ROOT": "DOTNET_ROOT(x86)", expectDotnetRoot)
                .ExecuteWithCapturedOutput("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(GetExpectOutput(expectDotnetRoot));
        }

        private static string SetupDotnetRootEchoProject()
        {
            return TestAssets
                .Get("TestAppEchoDotnetRoot")
                .CreateInstance()
                .WithSourceFiles()
                .WithBuildFiles()
                .Root
                .FullName;
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
