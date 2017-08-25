// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Protocol;
using Xunit;
using Xunit.Abstractions;
using MSBuildCommand = Microsoft.DotNet.Tools.Test.Utilities.MSBuildCommand;
using System.Diagnostics;
using System.Threading;


// There are tests which modify static Telemetry.CurrentSessionId and they cannot run in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetMSBuildBuildsProjects : TestBase
    {
        private readonly ITestOutputHelper _output;

        public GivenDotnetMSBuildBuildsProjects(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = TestAssets.Get("MSBuildBareBonesProject")
                                        .CreateInstance()
                                        .WithSourceFiles();

            var testProjectDirectory = testInstance.Root;

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

        [Theory(Skip="New parser feature needed")]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("restore")]
        public void When_help_is_invoked_Then_MSBuild_extra_options_text_is_included_in_output(string commandName)
        {
            const string MSBuildHelpText = " Any extra options that should be passed to MSBuild. See 'dotnet msbuild -h' for available options.";

            var projectDirectory = TestAssets.CreateTestDirectory(commandName);
            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"{commandName} --help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(MSBuildHelpText);
        }

        [Fact]
        public void WhenRestoreSourcesStartsWithUnixPathThenHttpsSourceIsParsedCorrectly()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
            var testInstance = TestAssets.Get("LibraryWithUnresolvablePackageReference")
                                        .CreateInstance()
                                        .WithSourceFiles();

            var root = testInstance.Root;
            var somePathThatExists = "/usr/local/bin";

            var result = new DotnetCommand()
                .WithWorkingDirectory(root)
                .Execute($"msbuild /p:RestoreSources={somePathThatExists};https://api.nuget.org/v3/index.json /t:restore LibraryWithUnresolvablePackageReference.csproj");

            _output.WriteLine($"[STDOUT]\n{result.StdOut}\n[STDERR]\n{result.StdErr}");

            result.Should().Fail();

            result.StdOut.Should().ContainVisuallySameFragment("NU1101");
        }

        [Fact]
        public void WhenDotnetRunHelpIsInvokedAppArgumentsTextIsIncludedInOutput()
        {
            string AppArgumentsText = Tools.Run.LocalizableStrings.RunCommandAdditionalArgsHelpText;

            var projectDirectory = TestAssets.CreateTestDirectory("RunContainsAppArgumentsText");
            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("run --help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(AppArgumentsText);
        }

        [Fact]
        public void WhenTelemetryIsEnabledTheLoggerIsAddedToTheCommandLine()
        {
            Telemetry.Telemetry telemetry;
            string[] allArgs = GetArgsForMSBuild(() => true, out telemetry);
            // telemetry will still be disabled if environment variable is set
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
            Telemetry.Telemetry telemetry;
            return GetArgsForMSBuild(sentinelExists, out telemetry);
        }

        private string[] GetArgsForMSBuild(Func<bool> sentinelExists, out Telemetry.Telemetry telemetry)
        {

            Telemetry.Telemetry.CurrentSessionId = null; // reset static session id modified by telemetry constructor
            telemetry = new Telemetry.Telemetry(new MockNuGetCacheSentinel(sentinelExists));

            MSBuildForwardingApp msBuildForwardingApp = new MSBuildForwardingApp(Enumerable.Empty<string>());

            object forwardingAppWithoutLogging = msBuildForwardingApp
                .GetType()
                .GetField("_forwardingAppWithoutLogging", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(msBuildForwardingApp);

            FieldInfo forwardingAppFieldInfo = forwardingAppWithoutLogging
                .GetType()
                .GetField("_forwardingApp", BindingFlags.Instance | BindingFlags.NonPublic);

            object forwardingApp = forwardingAppFieldInfo?.GetValue(forwardingAppWithoutLogging);

            FieldInfo allArgsFieldinfo = forwardingApp?
                .GetType()
                .GetField("_allArgs", BindingFlags.Instance | BindingFlags.NonPublic);

            return allArgsFieldinfo?.GetValue(forwardingApp) as string[];
        }
    }

    public sealed class MockNuGetCacheSentinel : IFirstTimeUseNoticeSentinel
    {
        private readonly Func<bool> _exists;

        public bool UnauthorizedAccess => true;

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
