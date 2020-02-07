// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Protocol;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;
using System.Threading;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;


// There are tests which modify static Telemetry.CurrentSessionId and they cannot run in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetMSBuildBuildsProjects : SdkTest
    {

        public GivenDotnetMSBuildBuildsProjects(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildBareBonesProject")
                                        .WithSource();

            var testProjectDirectory = testInstance.Path;

            new MSBuildCommand(Log, "SayHello", testProjectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, from MSBuild!");

            new MSBuildCommand(Log, "SayGoodbye", testProjectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Goodbye, from MSBuild. :'(");

            new DotnetCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("msbuild", "/t:SayThis",  "/p:This=GreatScott")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("You want me to say 'GreatScott'");
        }

        [Theory(Skip="New parser feature needed")]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("restore")]
        public void When_help_is_invoked_Then_MSBuild_extra_options_text_is_included_in_output(string commandName)
        {
            const string MSBuildHelpText = " Any extra options that should be passed to MSBuild. See 'dotnet msbuild -h' for available options.";

            var projectDirectory = _testAssetsManager.CreateTestDirectory(identifier: commandName);
            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory.Path)
                .Execute(commandName, "--help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(MSBuildHelpText);
        }

        [Theory]
        [InlineData("/p")]
        [InlineData("/property")]
        [InlineData("-p")]
        [InlineData("-property")]
        public void WhenRestoreSourcesStartsWithUnixPathThenHttpsSourceIsParsedCorrectly(string propertyFormat)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
            var testInstance = _testAssetsManager.CopyTestAsset("LibraryWithUnresolvablePackageReference")
                                        .WithSource();

            var root = testInstance.Path;
            var somePathThatExists = "/usr/local/bin";

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(root)
                .Execute("msbuild", $"{propertyFormat}:RestoreSources={somePathThatExists};https://api.nuget.org/v3/index.json", "/t:restore", "LibraryWithUnresolvablePackageReference.csproj");

            result.Should().Fail();

            result.StdOut.Should().ContainVisuallySameFragment("NU1101");
        }

        [Fact]
        public void WhenDotnetRunHelpIsInvokedAppArgumentsTextIsIncludedInOutput()
        {
            string AppArgumentsText = "Arguments passed to the application that is being run.";

            var projectDirectory = _testAssetsManager.CreateTestDirectory("RunContainsAppArgumentsText");
            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory.Path)
                .Execute("run", "--help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(AppArgumentsText);
        }



        
    }

    
}
