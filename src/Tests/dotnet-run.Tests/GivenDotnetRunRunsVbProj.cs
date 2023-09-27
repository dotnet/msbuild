// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Utils;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunRunsVbproj : SdkTest
    {
        public GivenDotnetRunRunsVbproj(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItGivesAnErrorWhenAttemptingToUseALaunchProfileThatDoesNotExistWhenThereIsNoLaunchSettingsFile()
        {
            var testAppName = "VBTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var runResult = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "test");

            string[] expectedErrorWords = LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile.Replace("\'{0}\'", "").Split(" ");
            runResult
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");

            expectedErrorWords.ForEach(word => runResult.Should().HaveStdErrContaining(word));
        }

        [Fact]
        public void ItFailsWhenTryingToUseLaunchProfileSharingTheSameNameWithAnotherProfileButDifferentCapitalization()
        {
            var testAppName = "AppWithDuplicateLaunchProfiles";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var runResult = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", "first");

            string expectedError = string.Format(LocalizableStrings.DuplicateCaseInsensitiveLaunchProfileNames, "\tfirst," + (OperatingSystem.IsWindows() ? "\r" : "") + "\n\tFIRST");
            runResult
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(expectedError);
        }

        [Fact]
        public void ItFailsWithSpecificErrorMessageIfLaunchProfileDoesntExist()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            string invalidLaunchProfileName = "Invalid";

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", "Invalid")
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(string.Format(LocalizableStrings.LaunchProfileDoesNotExist, invalidLaunchProfileName));
        }

        [Theory]
        [InlineData("Second")]
        [InlineData("sEcoND")] // ItAcceptsLaunchProfileWithAlternativeCasing
        public void ItUsesLaunchProfileOfTheSpecifiedName(string launchProfileName)
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: $"LaunchProfileSuccess-{launchProfileName}")
                            .WithSource();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", launchProfileName)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Second")
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void ItDefaultsToTheFirstUsableLaunchProfile()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItPrintsUsingLaunchSettingsMessageWhenNotQuiet()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VbAppWithLaunchSettings")
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "My Project", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("-v:m");

            cmd.Should().Pass()
                .And.HaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItGivesAnErrorWhenTheLaunchProfileNotFound()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Third")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "Third", "").Trim());
        }
    }
}
