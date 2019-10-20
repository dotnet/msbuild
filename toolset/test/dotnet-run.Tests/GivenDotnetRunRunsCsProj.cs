// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunBuildsCsproj : TestBase
    {
        [Fact]
        public void ItCanRunAMSBuildProject()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItCanRunAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = TestAssets.Get(
                    TestAssetKinds.DesktopTestProjects,
                    "NETFrameworkReferenceNETStandard20")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(testInstance.Root.FullName, "MultiTFMTestApp");

            new RunCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("--framework netcoreapp3.0")
                .Should().Pass()
                         .And.HaveStdOutContaining("This string came from the test library!");
        }

        [Fact]
        public void ItDoesNotImplicitlyBuildAProjectWhenRunningWithTheNoBuildOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var result = new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName)
                .ExecuteWithCapturedOutput("--no-build -v:m");

            result.Should().Fail();
            if (!DotnetUnderTest.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Restore");
            }
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenRunningWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItBuildsTheProjectBeforeRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItCanRunAMSBuildProjectWhenSpecifyingAFramework()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--framework netcoreapp3.0")
                .Should().Pass()
                         .And.HaveStdOut("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathAfterBuilding()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput($"--no-build")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathWithoutBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var projectFile = testInstance.Root.GetFile(testAppName + ".csproj");

            new RunCommand()
                .WithWorkingDirectory(testInstance.Root.Parent)
                .ExecuteWithCapturedOutput($"--project {projectFile.FullName}")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathSpecifyingOnlyTheDirectoryWithoutBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RunCommand()
                .WithWorkingDirectory(testInstance.Root.Parent)
                .ExecuteWithCapturedOutput($"--project {testProjectDirectory}")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            string dir = "pkgs";
            string args = $"--packages {dir}";

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new RunCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItReportsAGoodErrorWhenProjectHasMultipleFrameworks()
        {
            var testAppName = "MSBuildAppWithMultipleFrameworks";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            // use --no-build so this test can run on all platforms.
            // the test app targets net451, which can't be built on non-Windows
            new RunCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("--no-build")
                .Should().Fail()
                    .And.HaveStdErrContaining("--framework");
        }

        [Fact]
        public void ItCanPassArgumentsToSubjectAppByDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("-- foo bar baz")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:foo;bar;baz");
        }

        [Fact]
        public void ItGivesAnErrorWhenAttemptingToUseALaunchProfileThatDoesNotExistWhenThereIsNoLaunchSettingsFile()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile test")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!")
                         .And.HaveStdErrContaining(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile);
        }

        [Fact]
        public void ItUsesLaunchProfileOfTheSpecifiedName()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile Second");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Second");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItDefaultsToTheFirstUsableLaunchProfile()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput();

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItPrintsUsingLaunchSettingsMessageWhenNotQuiet()
        {
            var testInstance = TestAssets.Get("AppWithLaunchSettings")
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("-v:m");

            cmd.Should().Pass()
                .And.HaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItPrefersTheValueOfAppUrlFromEnvVarOverTheProp()
        {
            var testAppName = "AppWithApplicationUrlInLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile First");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("http://localhost:12345/");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItUsesTheValueOfAppUrlIfTheEnvVarIsNotSet()
        {
            var testAppName = "AppWithApplicationUrlInLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile Second");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("http://localhost:54321/");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItGivesAnErrorWhenTheLaunchProfileNotFound()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile Third")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "Third", "").Trim());
        }

        [Fact]
        public void ItGivesAnErrorWhenTheLaunchProfileCanNotBeHandled()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--launch-profile \"IIS Express\"")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "IIS Express", "").Trim());
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenTheSwitchIsSupplied()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--no-launch-profile");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenTheSwitchIsSuppliedWithoutErrorWhenThereAreNoLaunchSettings()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--no-launch-profile");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Hello World!");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenThereIsNoUsableDefault()
        {
            var testAppName = "AppWithLaunchSettingsNoDefault";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput();

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)")
                .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, LocalizableStrings.DefaultLaunchProfileDisplayName, "").Trim());
        }

        [Fact]
        public void ItPrintsAnErrorWhenLaunchSettingsAreCorrupted()
        {
            var testAppName = "AppWithCorruptedLaunchSettings";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var cmd = new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput();

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)")
                .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, LocalizableStrings.DefaultLaunchProfileDisplayName, "").Trim());
        }

        [Fact]
        public void ItRunsWithTheSpecifiedVerbosity()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var result = new RunCommand()
                .WithWorkingDirectory( testInstance.Root.FullName)
                .ExecuteWithCapturedOutput("-v:n");

            result.Should().Pass()
                .And.HaveStdOutContaining("Hello World!");

            if (!DotnetUnderTest.IsLocalized())
            {
                result.Should().HaveStdOutContaining("Restore")
                    .And.HaveStdOutContaining("CoreCompile");
            }
        }

        [Fact]
        public void ItDoesNotShowImportantLevelMessageByDefault()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName)
                .ExecuteWithCapturedOutput();

            result.Should().Pass()
                .And.NotHaveStdOutContaining("Important text");
        }

        [Fact]
        public void ItShowImportantLevelMessageWhenPassInteractive()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName)
                .ExecuteWithCapturedOutput("--interactive");

            result.Should().Pass()
                .And.HaveStdOutContaining("Important text");
        }

        [Fact]
        public void ItRunsWithDotnetWithoutApphost()
        {
            var testInstance = TestAssets.Get("AppOutputsExecutablePath").CreateInstance().WithSourceFiles();

            var command = new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName);

            command.Environment["UseAppHost"] = "false";

            command.ExecuteWithCapturedOutput()
                   .Should()
                   .Pass()
                   .And
                   .HaveStdOutContaining($"dotnet{Constants.ExeSuffix}");
        }

        [Fact]
        public void ItRunsWithApphost()
        {
            var testInstance = TestAssets.Get("AppOutputsExecutablePath").CreateInstance().WithSourceFiles();

            var result = new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName)
                .ExecuteWithCapturedOutput();

            result.Should().Pass()
                .And.HaveStdOutContaining($"AppOutputsExecutablePath{Constants.ExeSuffix}");
        }

        [Fact]
        public void ItForwardsEmptyArgumentsToTheApp()
        {
            var testAppName = "TestAppSimple";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles();

            new RunCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("a \"\" c")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining($"0 = a{Environment.NewLine}1 = {Environment.NewLine}2 = c");
        }
    }
}
