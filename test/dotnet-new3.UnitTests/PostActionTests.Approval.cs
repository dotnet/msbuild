// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Dotnet_new3.IntegrationTests
{
    [UsesVerify]
    public partial class PostActionTests
    {
        [Fact]
        public Task Restore_Basic_Approval()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("PostActions/RestoreNuGet/Basic", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.PostActions.RestoreNuGet.Basic", "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    //for OSX Verify.NET is not replacing temporary path correctly.
                    output.Replace("/private{TempPath}", "{TempPath}");
                    output.ScrubByRegex("(?<=Restoring {TempPath}TemplateEngine\\.Tests(\\\\|\\/)Guid_1(\\\\|\\/)MyProject.csproj:\\n)(.*?)(?=\\nRestore succeeded)", "%RESTORE CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task RunScript_Basic_Approval()
        {
            string templateLocation = "PostActions/RunScript/Basic";
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UniqueForOSPlatform();
        }

        [Fact]
        public Task AddPackageReference_Basic_Approval()
        {
            string templateLocation = "PostActions/AddPackageReference/Basic";
            string templateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    //for OSX Verify.NET is not replacing temporary path correctly.
                    output.Replace("/private{TempPath}", "{TempPath}");
                    output.ScrubByRegex("(?<=Adding a package reference Newtonsoft.Json \\(version: 13.0.1\\) to project file {TempPath}TemplateEngine\\.Tests(\\\\|\\/)Guid_1(\\\\|\\/)MyProject.csproj:\\n)(.*?)(?=\\nSuccessfully added a reference to the project file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task AddProjectReference_Basic_Approval()
        {
            string templateLocation = "PostActions/AddProjectReference/Basic";
            string templateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    //for OSX Verify.NET is not replacing temporary path correctly.
                    output.Replace("/private{TempPath}", "{TempPath}");
                    output.ScrubByRegex("(?<=Adding a project reference {TempPath}TemplateEngine.Tests(\\\\|\\/)Guid_1(\\\\|\\/)Project2(\\\\|\\/)Project2.csproj to project file {TempPath}TemplateEngine.Tests(\\\\|\\/)Guid_1(\\\\|\\/)Project1(\\\\|\\/)Project1.csproj:\\n)(.*?)(?=\\nSuccessfully added a reference to the project file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task AddProjectToSolution_Basic_Approval()
        {
            string templateLocation = "PostActions/AddProjectToSolution/Basic";
            string templateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            var commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    //for OSX Verify.NET is not replacing temporary path correctly.
                    output.Replace("/private{TempPath}", "{TempPath}");
                    output.ScrubByRegex("(?<=solution folder: src\\n)(.*?)(?=\\nSuccessfully added project\\(s\\) to a solution file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task PrintInstructions_Basic_Approval()
        {
            string templateLocation = "PostActions/Instructions/Basic";
            string templateName = "TestAssets.PostActions.Instructions.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings);
        }

        [Fact]
        public Task PostActions_DryRun()
        {
            string templateLocation = "PostActions/RestoreNuGet/Basic";
            string templateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject", "--dry-run")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Assert.False(File.Exists(Path.Combine(workingDirectory, "MyProject.csproj")));
            Assert.False(File.Exists(Path.Combine(workingDirectory, "Program.cs")));

            return Verifier.Verify(commandResult.StdOut, _verifySettings);
        }

        [Fact]
        public Task CanProcessUnknownPostAction()
        {
            string templateLocation = "PostActions/UnknownPostAction";
            string templateName = "TestAssets.PostActions.UnknownPostAction";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail();

            return Verifier.Verify(commandResult.StdOut + Environment.NewLine + commandResult.StdErr, _verifySettings);
        }

        [Fact]
        public Task RunScript_DoNotExecuteWhenScriptsAreNotAllowed()
        {
            string templateLocation = "PostActions/RunScript/Basic";
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "no")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verifier.Verify(commandResult.StdOut + Environment.NewLine + commandResult.StdErr, _verifySettings)
                .UniqueForOSPlatform();
        }
    }
}
