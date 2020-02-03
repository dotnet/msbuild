// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.MSBuild;
using FluentAssertions;
using Xunit;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenMsbuildForwardingApp : SdkTest
    {
        public GivenMsbuildForwardingApp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void DotnetExeIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().Be("dotnet.exe");
        }

        [UnixOnlyFact]
        public void DotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().Be("dotnet");
        }

        [Theory]
        [InlineData("MSBuildExtensionsPath")]
        [InlineData("MSBuildSDKsPath")]
        [InlineData("DOTNET_CLI_TELEMETRY_SESSIONID")]
        public void ItSetsEnvironmentalVariables(string envVarName)
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath).GetProcessStartInfo();
            startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
        }

        [Fact]
        public void ItSetsMSBuildExtensionPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildExtensionsPath";
            new DirectoryInfo(new MSBuildForwardingApp(new string[0], msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [Fact(Skip ="Test app base folder doesn't have Sdks")]
        public void ItSetsMSBuildSDKsPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildSDKsPath";
            new DirectoryInfo(new MSBuildForwardingApp(new string[0], msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [Fact]
        public void ItSetsOrIgnoresTelemetrySessionId()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "DOTNET_CLI_TELEMETRY_SESSIONID";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo();

            Log.WriteLine("StartInfo DOTNET_CLI_TELEMETRY_SESSIONID: " + startInfo.Environment[envVar]);
            
            (startInfo.Environment[envVar] == null || Guid.TryParse(startInfo.Environment[envVar], out _))
                .Should().BeTrue("DOTNET_CLI_TELEMETRY_SESSIONID should be null or current session id");
        }

        [Fact]
        public void ItDoesNotSetCurrentWorkingDirectory()
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo().WorkingDirectory.Should().Be("");
        }
    }
}
