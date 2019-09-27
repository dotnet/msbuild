// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests
{
    public class GivenThatTheUserIsRunningDotNetForTheFirstTime : TestBase
    {
        private static CommandResult _firstDotnetNonVerbUseCommandResult;
        private static CommandResult _firstDotnetVerbUseCommandResult;
        private static DirectoryInfo _nugetFallbackFolder;
        private static DirectoryInfo _dotDotnetFolder;
        private static string _testDirectory;

        static GivenThatTheUserIsRunningDotNetForTheFirstTime()
        {
            _testDirectory = TestAssets.CreateTestDirectory("Dotnet_first_time_experience_tests").FullName;
            var testNuGetHome = Path.Combine(_testDirectory, "nuget_home");
            var cliTestFallbackFolder = Path.Combine(testNuGetHome, ".dotnet", "NuGetFallbackFolder");
            var profiled = Path.Combine(_testDirectory, "profile.d");
            var pathsd = Path.Combine(_testDirectory, "paths.d");

            var command = new DotnetCommand()
                .WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = testNuGetHome;
            command.Environment["USERPROFILE"] = testNuGetHome;
            command.Environment["APPDATA"] = testNuGetHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = cliTestFallbackFolder;
            command.Environment["DOTNET_CLI_TEST_LINUX_PROFILED_PATH"] = profiled;
            command.Environment["DOTNET_CLI_TEST_OSX_PATHSD_PATH"] = pathsd;
            command.Environment["SkipInvalidConfigurations"] = "true";

            _firstDotnetNonVerbUseCommandResult = command.ExecuteWithCapturedOutput("--info");
            _firstDotnetVerbUseCommandResult = command.ExecuteWithCapturedOutput("new --debug:ephemeral-hive");

            _nugetFallbackFolder = new DirectoryInfo(cliTestFallbackFolder);
            _dotDotnetFolder = new DirectoryInfo(Path.Combine(testNuGetHome, ".dotnet"));
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeSucceeds()
        {
            _firstDotnetVerbUseCommandResult
                .Should()
                .Pass();
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeWithNonVerbsDoesNotPrintEula()
        {
            string firstTimeNonVerbUseMessage = Cli.Utils.LocalizableStrings.DotNetSdkInfoLabel;

            _firstDotnetNonVerbUseCommandResult.StdOut
                .Should()
                .StartWith(firstTimeNonVerbUseMessage);
        }

        [Fact]
        public void ItShowsTheAppropriateMessageToTheUser()
        {
            var expectedVersion = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ExpectedSdkVersion.txt"));
            _firstDotnetVerbUseCommandResult.StdOut
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.DeriveDotnetVersionFromProductVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation)
                .And.NotContain("Restore completed in");
        }

        [Fact]
        public void ItCreatesAFirstUseSentinelFileUnderTheDotDotNetFolder()
        {
            _dotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.dotnetFirstUseSentinel");
        }

        [Fact]
        public void ItCreatesAnAspNetCertificateSentinelFileUnderTheDotDotNetFolder()
        {
            _dotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.aspNetCertificateSentinel");
        }

        [Fact]
        public void ItDoesNotCreateAFirstUseSentinelFileNorAnAspNetCertificateSentinelFileUnderTheDotDotNetFolderWhenInternalReportInstallSuccessIsInvoked()
        {
            var emptyHome = Path.Combine(_testDirectory, "empty_home");
            var profiled = Path.Combine(_testDirectory, "profile.d");
            var pathsd = Path.Combine(_testDirectory, "paths.d");

            var command = new DotnetCommand()
                .WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = emptyHome;
            command.Environment["USERPROFILE"] = emptyHome;
            command.Environment["APPDATA"] = emptyHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = _nugetFallbackFolder.FullName;
            command.Environment["DOTNET_CLI_TEST_LINUX_PROFILED_PATH"] = profiled;
            command.Environment["DOTNET_CLI_TEST_OSX_PATHSD_PATH"] = pathsd;
            // Disable to prevent the creation of the .dotnet folder by optimizationdata.
            command.Environment["DOTNET_DISABLE_MULTICOREJIT"] = "true";
            command.Environment["SkipInvalidConfigurations"] = "true";
            // Disable telemetry to prevent the creation of the .dotnet folder
            // for machineid and docker cache files
            command.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "true";

            command.ExecuteWithCapturedOutput("internal-reportinstallsuccess test").Should().Pass();

            var homeFolder = new DirectoryInfo(Path.Combine(emptyHome, ".dotnet"));
            homeFolder.Should().NotExist();
        }

        [Fact]
        public void ItShowsTheTelemetryNoticeWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var newHome = Path.Combine(_testDirectory, "new_home");
            var newHomeFolder = new DirectoryInfo(Path.Combine(newHome, ".dotnet"));
            var profiled = Path.Combine(_testDirectory, "profile.d");
            var pathsd = Path.Combine(_testDirectory, "paths.d");

            var command = new DotnetCommand()
                .WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = newHome;
            command.Environment["USERPROFILE"] = newHome;
            command.Environment["APPDATA"] = newHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = _nugetFallbackFolder.FullName;
            command.Environment["DOTNET_CLI_TEST_LINUX_PROFILED_PATH"] = profiled;
            command.Environment["DOTNET_CLI_TEST_OSX_PATHSD_PATH"] = pathsd;
            command.Environment["SkipInvalidConfigurations"] = "true";

            command.ExecuteWithCapturedOutput("internal-reportinstallsuccess test").Should().Pass();

            var result = command.ExecuteWithCapturedOutput("new --debug:ephemeral-hive");

            var expectedVersion = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ExpectedSdkVersion.txt"));

            result.StdOut
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.DeriveDotnetVersionFromProductVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation);
        }

        [Fact]
        public void ItShowsTheAspNetCertificateGenerationMessageWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var newHome = Path.Combine(_testDirectory, "aspnet_home");
            var newHomeFolder = new DirectoryInfo(Path.Combine(newHome, ".dotnet"));
            var profiled = Path.Combine(_testDirectory, "profile.d");
            var pathsd = Path.Combine(_testDirectory, "paths.d");

            var command = new DotnetCommand()
                .WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = newHome;
            command.Environment["USERPROFILE"] = newHome;
            command.Environment["APPDATA"] = newHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = _nugetFallbackFolder.FullName;
            command.Environment["DOTNET_CLI_TEST_LINUX_PROFILED_PATH"] = profiled;
            command.Environment["DOTNET_CLI_TEST_OSX_PATHSD_PATH"] = pathsd;
            command.Environment["SkipInvalidConfigurations"] = "true";

            command.ExecuteWithCapturedOutput("internal-reportinstallsuccess test").Should().Pass();

            command.ExecuteWithCapturedOutput("new --debug:ephemeral-hive");
        }

        [LinuxOnlyFact]
        public void ItCreatesTheProfileFileOnLinuxWhenInvokedFromNativeInstaller()
        {
            var emptyHome = Path.Combine(_testDirectory, "empty_home_for_profile_on_linux");
            var profiled = Path.Combine(_testDirectory, "profile.d");

            var command = new DotnetCommand().WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = emptyHome;
            command.Environment["USERPROFILE"] = emptyHome;
            command.Environment["APPDATA"] = emptyHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = _nugetFallbackFolder.FullName;
            command.Environment["DOTNET_CLI_TEST_LINUX_PROFILED_PATH"] = profiled;
            command.Environment["DOTNET_DISABLE_MULTICOREJIT"] = "true";
            command.Environment["SkipInvalidConfigurations"] = "true";
            command.ExecuteWithCapturedOutput("internal-reportinstallsuccess test").Should().Pass();

            File.Exists(profiled).Should().BeTrue();
            File.ReadAllText(profiled).Should().Be(
                $"export PATH=\"$PATH:{CliFolderPathCalculator.ToolsShimPathInUnix.PathWithDollar}\"");
        }

        [MacOsOnlyFact]
        public void ItCreatesThePathDFileOnMacOSWhenInvokedFromNativeInstaller()
        {
            var emptyHome = Path.Combine(_testDirectory, "empty_home_for_pathd");
            var pathsd = Path.Combine(_testDirectory, "paths.d");

            var command = new DotnetCommand().WithWorkingDirectory(_testDirectory);
            command.Environment["HOME"] = emptyHome;
            command.Environment["USERPROFILE"] = emptyHome;
            command.Environment["APPDATA"] = emptyHome;
            command.Environment["DOTNET_CLI_TEST_FALLBACKFOLDER"] = _nugetFallbackFolder.FullName;
            command.Environment["DOTNET_CLI_TEST_OSX_PATHSD_PATH"] = pathsd;
            command.Environment["DOTNET_DISABLE_MULTICOREJIT"] = "true";
            command.Environment["SkipInvalidConfigurations"] = "true";
            command.ExecuteWithCapturedOutput("internal-reportinstallsuccess test").Should().Pass();

            File.Exists(pathsd).Should().BeTrue();
            File.ReadAllText(pathsd).Should().Be(CliFolderPathCalculator.ToolsShimPathInUnix.PathWithTilde);
        }

        private string GetDotnetVersion()
        {
            return new DotnetCommand().ExecuteWithCapturedOutput("--version").StdOut
                .TrimEnd(Environment.NewLine.ToCharArray());
        }
    }
}
