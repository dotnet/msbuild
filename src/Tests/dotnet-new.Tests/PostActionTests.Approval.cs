// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class PostActionTests : BaseIntegrationTest
    {
        [Fact]
        public Task Restore_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RestoreNuGet/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.PostActions.RestoreNuGet.Basic", "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.Replace(workingDirectory, "%working directory%");
                    output.UnixifyNewlines();
                    output.ScrubByRegex("(?<=Restoring %working directory%(\\\\|\\/)MyProject.csproj:\\n)(.*?)(?=\\nRestore succeeded)", "%RESTORE CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task RunScript_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RunScript/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform();
        }

        [Fact]
        public Task AddPackageReference_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddPackageReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.Replace(workingDirectory, "%working directory%");
                    output.UnixifyNewlines();
                    output.ScrubByRegex($"(?<=Adding a package reference Newtonsoft.Json \\(version: {ToolsetInfo.GetNewtonsoftJsonPackageVersion()}\\) to project file %working directory%(\\\\|\\/)MyProject.csproj:\\n)(.*?)(?=\\nSuccessfully added a reference to the project file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task AddProjectReference_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.Replace(workingDirectory, "%working directory%");
                    output.UnixifyNewlines();
                    output.ScrubByRegex("(?<=to project file %working directory%(\\\\|\\/)Project1(\\\\|\\/)Project1.csproj:\\n)(.*?)(?=\\nSuccessfully added a reference to the project file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task AddProjectToSolution_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectToSolution/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.Replace(workingDirectory, "%working directory%");
                    output.UnixifyNewlines();
                    output.ScrubByRegex("(?<=solution folder: src\\n)(.*?)(?=\\nSuccessfully added project\\(s\\) to a solution file.)", "%CALLBACK OUTPUT%", System.Text.RegularExpressions.RegexOptions.Singleline);
                });
        }

        [Fact]
        public Task PrintInstructions_Basic_Approval()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/Instructions/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.Instructions.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task PostActions_DryRun()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RestoreNuGet/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-n", "MyProject", "--dry-run")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Assert.False(File.Exists(Path.Combine(workingDirectory, "MyProject.csproj")));
            Assert.False(File.Exists(Path.Combine(workingDirectory, "Program.cs")));

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanProcessUnknownPostAction()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/UnknownPostAction", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.UnknownPostAction";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail();

            return Verify(commandResult.StdOut + Environment.NewLine + commandResult.StdErr);
        }

        [Fact]
        public Task RunScript_DoNotExecuteWhenScriptsAreNotAllowed()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RunScript/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "no")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdOut + Environment.NewLine + commandResult.StdErr)
                .UniqueForOSPlatform();
        }
    }
}
