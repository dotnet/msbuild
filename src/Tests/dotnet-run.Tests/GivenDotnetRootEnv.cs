// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        private static Version Version6_0 = new Version(6, 0);

        public GivenDotnetRootEnv(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ItShouldSetDotnetRootToDirectoryOfMuxer(string targetFramework)
        {
            string expectDotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string expectOutput = GetExpectOutput(expectDotnetRoot, targetFramework);

            var projectRoot = SetupDotnetRootEchoProject(null, targetFramework);

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
            var processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
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

            runCommand.EnvironmentToRemove.Add($"DOTNET_ROOT_{processArchitecture}");
            runCommand
                .Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(GetExpectOutput(expectDotnetRoot));
        }

        private string SetupDotnetRootEchoProject([CallerMemberName] string callingMethod = null, string targetFramework = null)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppEchoDotnetRoot", callingMethod, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFrameworkOrFrameworks(targetFramework ?? null, false)
                .Restore(Log);

            new BuildCommand(testAsset)
                .Execute($"{(!string.IsNullOrEmpty(targetFramework) ? "/p:TargetFramework=" + targetFramework : string.Empty)}")
                .Should()
                .Pass();

            return testAsset.Path;
        }

        private static string GetExpectOutput(string expectDotnetRoot, string targetFramework = null)
        {
            string expectOutput;
            string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            if (!string.IsNullOrEmpty(targetFramework) && Version.Parse(targetFramework.AsSpan(3)) >= Version6_0)
            {
                expectOutput = $"DOTNET_ROOT='';DOTNET_ROOT(x86)='';DOTNET_ROOT_{processArchitecture}='{expectDotnetRoot}'";
            }
            else if (Environment.Is64BitProcess)
            {
                expectOutput = @$"DOTNET_ROOT='{expectDotnetRoot}';DOTNET_ROOT(x86)='';DOTNET_ROOT_{processArchitecture}=''";
            }
            else
            {
                expectOutput = @$"DOTNET_ROOT='';DOTNET_ROOT(x86)='{expectDotnetRoot}';DOTNET_ROOT_{processArchitecture}=''";
            }

            return expectOutput;
        }
    }
}
