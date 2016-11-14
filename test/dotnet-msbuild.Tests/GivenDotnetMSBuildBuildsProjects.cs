// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Protocol;
using Xunit;
using MSBuildCommand = Microsoft.DotNet.Tools.Test.Utilities.MSBuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetMSBuildBuildsProjects : TestBase
    {
        [Fact]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("MSBuildBareBonesProject");

            var testProjectDirectory = testInstance.TestRoot;

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayHello")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, from MSBuild!");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayGoodbye")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Goodbye, from MSBuild. :'(");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayThis /p:This=GreatScott")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("You want me to say 'GreatScott'");
        }

        [Theory]
        [InlineData("build", true)]
        [InlineData("clean", true)]
        [InlineData("pack", true)]
        [InlineData("publish", true)]
        [InlineData("restore", true)]
        [InlineData("run", true)]
        public void When_help_is_invoked_Then_MSBuild_extra_options_text_is_included_in_output(string commandName, bool isMSBuildCommand)
        {
            const string MSBuildHelpText = "  Any extra options that should be passed to MSBuild. See 'dotnet msbuild -h' for available options.";

            var projectDirectory = TestAssetsManager.CreateTestDirectory("ItContainsMSBuildHelpText");
            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory.Path)
                .ExecuteWithCapturedOutput($"{commandName} --help");

            result.ExitCode.Should().Be(0);
            if (isMSBuildCommand)
            {
                result.StdOut.Should().Contain(MSBuildHelpText);
            }
            else
            {
                result.StdOut.Should().NotContain(MSBuildHelpText);
            }
        }

        [Fact]
        public void WhenTelemetryIsEnabledTheLoggerIsAddedToTheCommandLine()
        {
            Telemetry telemetry;
            string[] allArgs = GetArgsForMSBuild(() => true, out telemetry);
            // telemetry will still be disabled if environmental variable is set
            if (telemetry.Enabled)
            {
                allArgs.Should().NotBeNull();

                allArgs.Should().Contain(
                    value => value.IndexOf("/Logger", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The MSBuild logger argument should be specified when telemetry is enabled.");
            }
        }

        [Fact]
        public void WhenTelemetryIsDisabledTheLoggerIsNotAddedToTheCommandLine()
        {
            string[] allArgs = GetArgsForMSBuild(() => false);

            allArgs.Should().NotBeNull();

            allArgs.Should().NotContain(
                value => value.IndexOf("/Logger", StringComparison.OrdinalIgnoreCase) >= 0,
                $"The MSBuild logger argument should not be specified when telemetry is disabled.");
        }

        private string[] GetArgsForMSBuild(Func<bool> sentinelExists)
        {
            Telemetry telemetry;
            return GetArgsForMSBuild(sentinelExists, out telemetry);
        }

        private string[] GetArgsForMSBuild(Func<bool> sentinelExists, out Telemetry telemetry)
        {
            telemetry = new Telemetry(new MockNuGetCacheSentinel(sentinelExists));

            MSBuildForwardingApp msBuildForwardingApp = new MSBuildForwardingApp(Enumerable.Empty<string>());

            FieldInfo forwardingAppFieldInfo = msBuildForwardingApp
                .GetType()
                .GetField("_forwardingApp", BindingFlags.Instance | BindingFlags.NonPublic);

            ForwardingApp forwardingApp = forwardingAppFieldInfo?.GetValue(msBuildForwardingApp) as ForwardingApp;

            FieldInfo allArgsFieldinfo = forwardingApp?
                .GetType()
                .GetField("_allArgs", BindingFlags.Instance | BindingFlags.NonPublic);

            return allArgsFieldinfo?.GetValue(forwardingApp) as string[];
        }
    }

    public sealed class MockNuGetCacheSentinel : INuGetCacheSentinel
    {
        private readonly Func<bool> _exists;

        public MockNuGetCacheSentinel(Func<bool> exists = null)
        {
            _exists = exists ?? (() => true);
        }
        public void Dispose()
        {
        }

        public bool InProgressSentinelAlreadyExists() => false;

        public bool Exists() => _exists();

        public void CreateIfNotExists()
        {
        }
    }
}
