// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

//[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests
{
    public class DotNetFirstTime
    {
        public DirectoryInfo NugetFallbackFolder;
        public DirectoryInfo DotDotnetFolder;
        public string TestDirectory;

        public TestCommand Setup(ITestOutputHelper log, TestAssetsManager testAssets, [CallerMemberName] string testName = null)
        {
            TestDirectory = testAssets.CreateTestDirectory(testName).Path;
            var testNuGetHome = Path.Combine(TestDirectory, "nuget_home");
            var cliTestFallbackFolder = Path.Combine(testNuGetHome, ".dotnet", "NuGetFallbackFolder");
            var profiled = Path.Combine(TestDirectory, "profile.d");
            var pathsd = Path.Combine(TestDirectory, "paths.d");

            var command = new DotnetCommand(log)
                .WithWorkingDirectory(TestDirectory)
                .WithEnvironmentVariable("HOME", testNuGetHome)
                .WithEnvironmentVariable("USERPROFILE", testNuGetHome)
                .WithEnvironmentVariable("APPDATA", testNuGetHome)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER", cliTestFallbackFolder)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_LINUX_PROFILED_PATH", profiled)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_OSX_PATHSD_PATH", pathsd)
                .WithEnvironmentVariable("SkipInvalidConfigurations", "true")
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, "");

            NugetFallbackFolder = new DirectoryInfo(cliTestFallbackFolder);
            DotDotnetFolder = new DirectoryInfo(Path.Combine(testNuGetHome, ".dotnet"));

            return command;
        }
    }

    public class DotNetFirstTimeFixture : IDisposable
    {
        public CommandResult FirstDotnetNonVerbUseCommandResult;
        public CommandResult FirstDotnetVerbUseCommandResult;
        public CommandResult FirstDotnetWorkloadInfoResult;
        public DirectoryInfo NugetFallbackFolder;
        public DirectoryInfo DotDotnetFolder;
        public string TestDirectory;

        public Dictionary<string, string> ExtraEnvironmentVariables = new();

        public void Init(ITestOutputHelper log, TestAssetsManager testAssets)
        {
            if (TestDirectory == null)
            {
                var dotnetFirstTime = new DotNetFirstTime();

                var command = dotnetFirstTime.Setup(log, testAssets, testName: "Dotnet_first_time_experience_tests");

                FirstDotnetNonVerbUseCommandResult = command.Execute("--info");
                FirstDotnetVerbUseCommandResult = command.Execute("new", "--debug:ephemeral-hive");

                TestDirectory = dotnetFirstTime.TestDirectory;
                NugetFallbackFolder = dotnetFirstTime.NugetFallbackFolder;
                DotDotnetFolder = dotnetFirstTime.DotDotnetFolder;
            }
        }

        public void Dispose()
        {

        }
    }

    public class GivenThatTheUserIsRunningDotNetForTheFirstTime : SdkTest, IClassFixture<DotNetFirstTimeFixture>
    {
        DotNetFirstTimeFixture _fixture;

        public GivenThatTheUserIsRunningDotNetForTheFirstTime(ITestOutputHelper log, DotNetFirstTimeFixture fixture) : base(log)
        {
            fixture.Init(log, _testAssetsManager);
            _fixture = fixture;
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeSucceeds()
        {
            _fixture.FirstDotnetVerbUseCommandResult
                .Should()
                .Pass();
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeWithNonVerbsDoesNotPrintEula()
        {
            string firstTimeNonVerbUseMessage = Cli.Utils.LocalizableStrings.DotNetSdkInfoLabel;

            _fixture.FirstDotnetNonVerbUseCommandResult.StdOut
                .Should()
                .StartWith(firstTimeNonVerbUseMessage);
        }

        [WindowsOnlyFact]
        public void ItShowsTheAppropriateMessageToTheUser()
        {

            var expectedVersion = GetDotnetVersion();
            _fixture.FirstDotnetVerbUseCommandResult.StdOut
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.ParseDotNetVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation)
                .And.NotContain("Restore completed in");
        }

        [Fact]
        public void ItCreatesAFirstUseSentinelFileUnderTheDotDotNetFolder()
        {
            _fixture.DotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.dotnetFirstUseSentinel");
        }

        [Fact]
        public void ItCreatesAnAspNetCertificateSentinelFileUnderTheDotDotNetFolder()
        {
            _fixture.DotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.aspNetCertificateSentinel");
        }

        [Fact]
        public void ItDoesNotCreateAFirstUseSentinelFileNorAnAspNetCertificateSentinelFileUnderTheDotDotNetFolderWhenInternalReportInstallSuccessIsInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, _testAssetsManager);

            // Disable telemetry to prevent the creation of the .dotnet folder
            // for machineid and docker cache files
            command = command.WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            var homeFolder = dotnetFirstTime.NugetFallbackFolder.Parent;
            homeFolder.Should().NotExist();
        }

        [WindowsOnlyFact]
        public void ItShowsTheTelemetryNoticeWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, _testAssetsManager);

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            var result = command.Execute("new", "--debug:ephemeral-hive");

            var expectedVersion = GetDotnetVersion();

            result.StdOut
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.ParseDotNetVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation);
        }

        [Fact]
        public void ItShowsTheAspNetCertificateGenerationMessageWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, _testAssetsManager);


            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            command.Execute("new", "--debug:ephemeral-hive");
        }

        [LinuxOnlyFact]
        public void ItCreatesTheProfileFileOnLinuxWhenInvokedFromNativeInstaller()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, _testAssetsManager);

            var profiled = Path.Combine(dotnetFirstTime.TestDirectory, "profile.d");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            File.Exists(profiled).Should().BeTrue();
            File.ReadAllText(profiled).Should().Be(
                $"export PATH=\"$PATH:{CliFolderPathCalculator.ToolsShimPathInUnix.PathWithDollar}\"");
        }

        [MacOsOnlyFact]
        public void ItCreatesThePathDFileOnMacOSWhenInvokedFromNativeInstaller()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, _testAssetsManager);

            var pathsd = Path.Combine(dotnetFirstTime.TestDirectory, "paths.d");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            File.Exists(pathsd).Should().BeTrue();
            File.ReadAllText(pathsd).Should().Be(CliFolderPathCalculator.ToolsShimPathInUnix.PathWithTilde);
        }

        private string GetDotnetVersion()
        {
            return TestContext.Current.ToolsetUnderTest.SdkVersion;
        }
    }
}
