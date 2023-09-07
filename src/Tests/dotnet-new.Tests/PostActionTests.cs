// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class PostActionTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public PostActionTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/Basic", "TestAssets.PostActions.RestoreNuGet.Basic")]
        [InlineData("PostActions/RestoreNuGet/BasicWithFiles", "TestAssets.PostActions.RestoreNuGet.BasicWithFiles")]
        [InlineData("PostActions/RestoreNuGet/CustomSourcePath", "TestAssets.PostActions.RestoreNuGet.CustomSourcePath")]
        [InlineData("PostActions/RestoreNuGet/CustomSourcePathFiles", "TestAssets.PostActions.RestoreNuGet.CustomSourcePathFiles")]
        [InlineData("PostActions/RestoreNuGet/CustomTargetPath", "TestAssets.PostActions.RestoreNuGet.CustomTargetPath", "./Custom/Path/")]
        [InlineData("PostActions/RestoreNuGet/CustomTargetPathFiles", "TestAssets.PostActions.RestoreNuGet.CustomTargetPathFiles", "./Custom/Path/")]
        [InlineData("PostActions/RestoreNuGet/CustomSourceTargetPath", "TestAssets.PostActions.RestoreNuGet.CustomSourceTargetPath", "./Target/Output/")]
        [InlineData("PostActions/RestoreNuGet/CustomSourceTargetPathFiles", "TestAssets.PostActions.RestoreNuGet.CustomSourceTargetPathFiles", "./Target/Output/")]
        public void Restore_Basic(string templatePartLocation, string templateName, string targetSubfolder = "")
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, targetSubfolder, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, targetSubfolder, $"Program.cs")));

            new DotnetBuildCommand(_log, "--no-restore")
                .WithWorkingDirectory(Path.Combine(workingDirectory, targetSubfolder))
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Fact]
        public void Restore_WithOutputAbsolutePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RestoreNuGet/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = CreateTemporaryFolder("output");
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            Assert.True(File.Exists(Path.Combine(outputDirectory, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"Program.cs")));

            new DotnetBuildCommand(_log, "--no-restore")
                .WithWorkingDirectory(outputDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Fact]
        public void Restore_WithOutputRelativePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RestoreNuGet/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = "output";
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MyProject.csproj"));

            Assert.True(File.Exists(Path.Combine(workingDirectory, outputDirectory, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, outputDirectory, $"Program.cs")));

            new DotnetBuildCommand(_log, "--no-restore")
                .WithWorkingDirectory(Path.Combine(workingDirectory, outputDirectory))
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/SourceRename", "TestAssets.PostActions.RestoreNuGet.SourceRename")]
        [InlineData("PostActions/RestoreNuGet/SourceRenameFiles", "TestAssets.PostActions.RestoreNuGet.SourceRenameFiles")]
        public void Restore_SourceRenameTest(string templatePartLocation, string templateName)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "MyProject", "--firstRename", "Awesome")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"MyAwesomeTestProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"Program.cs")));

            new DotnetBuildCommand(_log, "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyAwesomeTestProject");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsPrimaryOutputs", "TestAssets.PostActions.RestoreNuGet.TwoProjectsPrimaryOutputs")]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsFiles", "TestAssets.PostActions.RestoreNuGet.TwoProjectsFiles")]
        public void Restore_RestoreOneProjectFromTwo(string templatePartLocation, string templateName)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "TemplateApplication")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"src/TemplateApplication/TemplateApplication.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"test/TemplateApplication.Tests/TemplateApplication.Tests.csproj")));

            new DotnetBuildCommand(_log, "src/TemplateApplication", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication");

            new DotnetBuildCommand(_log, "test/TemplateApplication.Tests", "--no-restore")
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should().Fail()
                  .And.NotHaveStdOutContaining("Build succeeded.")
                  .And.HaveStdOutContaining("TemplateApplication.Tests");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames")]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames2", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames2")]
        public void Restore_SourceRenameTwoProjectsTest(string templatePartLocation, string templateName)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "TemplateApplication")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"TemplateApplication.UI/TemplateApplication.UI.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"TemplateApplication.Tests/TemplateApplication.Tests.csproj")));

            new DotnetBuildCommand(_log, "TemplateApplication.UI", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication.UI");

            new DotnetBuildCommand(_log, "TemplateApplication.Tests", "--no-restore")
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("Build succeeded.")
                  .And.HaveStdOutContaining("TemplateApplication.Tests");
        }

        [Fact]
        public void RunScript_Basic()
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
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                commandResult.Should().HaveStdOutContaining("Hello Windows");
            }
            else
            {
                commandResult.Should().HaveStdOutContaining("Hello Unix");
            }
        }

        [Fact]
        public void RunScript_DoNotRedirect()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RunScript/DoNotRedirect", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RunScript.DoNotRedirect";
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
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("This line goes to stdout")
                .And.HaveStdErrContaining("This line goes to stderr")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");
        }

        [Fact]
        public void RunScript_Redirect()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RunScript/Redirect", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RunScript.Redirect";
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
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.NotHaveStdOutContaining("This line goes to stdout")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");
        }

        [Fact]
        public void RunScript_RedirectOnError()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/RunScript/RedirectOnError", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string templateName = "TestAssets.PostActions.RunScript.RedirectOnError";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdErrContaining("Command failed")
                .And.HaveStdErrContaining("This line goes to stdout")
                .And.HaveStdErrContaining("This line goes to stderr")
                .And.NotHaveStdErrContaining("Run 'chmod +x *.sh'");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                commandResult.Should().HaveStdErrContaining("Manual instructions: Run 'setup.cmd'");
            }
            else
            {
                commandResult.Should().HaveStdErrContaining("Manual instructions: Run 'setup.sh'");
            }
        }

        [Theory]
        [InlineData("PostActions/AddPackageReference/Basic", "TestAssets.PostActions.AddPackageReference.Basic")]
        [InlineData("PostActions/AddPackageReference/BasicWithFiles", "TestAssets.PostActions.AddPackageReference.BasicWithFiles")]
        public void AddPackageReference_Basic(string templatePartLocation, string templateName)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added a reference to the project file.")
                .And.HaveStdOutContaining($"Adding a package reference Newtonsoft.Json (version: {ToolsetInfo.GetNewtonsoftJsonPackageVersion()}) to project file")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add");

            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddPackageReference_WithOutputAbsolutePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddPackageReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = CreateTemporaryFolder("output");
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", outputDirectory, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("reference Newtonsoft.Json")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            new DotnetBuildCommand(_log, Path.Combine(outputDirectory, "MyProject.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddPackageReference_WithOutputRelativePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddPackageReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", "output", "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("reference Newtonsoft.Json")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "MyProject.csproj"));

            new DotnetBuildCommand(_log, Path.Combine("output", "MyProject.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_Basic()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added");

            new DotnetBuildCommand(_log, "Project1/Project1.csproj")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetBuildCommand(_log, "Project2/Project2.csproj")
                 .WithWorkingDirectory(workingDirectory)
                 .Execute()
                 .Should()
                 .ExitWith(0)
                 .And
                 .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_WithOutputAbsolutePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = CreateTemporaryFolder("output");
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "Project1", "Project1.csproj"))
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "Project2", "Project2.csproj"));

            new DotnetBuildCommand(_log, Path.Combine(outputDirectory, "Project1", "Project1.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_WithOutputRelativePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectReference/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", "output")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "Project1", "Project1.csproj"))
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "Project2", "Project2.csproj"));

            new DotnetBuildCommand(_log, Path.Combine("output", "Project1", "Project1.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/Invalid", "TestAssets.PostActions.RestoreNuGet.Invalid", true)]
        [InlineData("PostActions/RestoreNuGet/Invalid_ContinueOnError", "TestAssets.PostActions.RestoreNuGet.Invalid.ContinueOnError", false)]
        public void ErrorExitCodeOnFailedPostAction(string templatePartLocation, string templateName, bool errorExpected)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName)
                   .WithCustomHive(home)
                   .WithWorkingDirectory(workingDirectory)
                   .Execute();

            if (errorExpected)
            {
                commandResult.Should().Fail();
            }
            else
            {
                commandResult.Should().Pass();
            }

            commandResult
                  .Should()
                  .HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                  .And.HaveStdOutContaining("Processing post-creation actions...")
                  .And.HaveStdOutContaining("Restoring")
                  .And.NotHaveStdOutContaining("Restore succeeded.")
                  .And.HaveStdErrContaining("Post action failed.")
                  .And.HaveStdErrContaining("Manual instructions: Run 'dotnet restore'");

            new DotnetBuildCommand(_log, "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail();
        }

        [Theory]
        [InlineData("PostActions/AddProjectToSolution/Basic", "TestAssets.PostActions.AddProjectToSolution.Basic")]
        [InlineData("PostActions/AddProjectToSolution/BasicWithFiles", "TestAssets.PostActions.AddProjectToSolution.BasicWithFiles")]
        public void AddProjectToSolution_Basic(string templatePartLocation, string templateName)
        {
            string templateLocation = _testAssetsManager.CopyTestAsset(templatePartLocation, testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
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

            new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added project(s) to a solution file.")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.");

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(workingDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_BasicInSolutionRoot()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectToSolution/BasicInSolutionRoot", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.BasicInSolutionRoot";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = CreateTemporaryFolder("output");
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("in the root of solution file")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MySolution.sln"))
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(outputDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_WithOutputAbsolutePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectToSolution/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = CreateTemporaryFolder("output");
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MySolution.sln"))
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(outputDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_WithOutputRelativePath()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectToSolution/Basic", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string outputDirectory = "output";
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MySolution.sln"))
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MyProject.csproj"));

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(workingDirectory, outputDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_PrimaryOutputIndexes()
        {
            string templateLocation = _testAssetsManager.CopyTestAsset("PostActions/AddProjectToSolution/BasicWithIndexes", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.BasicWithIndexes";
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

            new DotnetNewCommand(_log, expectedTemplateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added project(s) to a solution file.")
                .And.HaveStdOutContaining("solution folder: Server")
                .And.NotHaveStdOutContaining("Manual instructions: Add generated Server project to solution manually to folder 'Server'.");

            Assert.True(File.Exists(Path.Combine(workingDirectory, "MySolution.sln")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, "Server/Server.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, "Client/Client.csproj")));

            string solutionFileContents = File.ReadAllText(Path.Combine(workingDirectory, "MySolution.sln"));
            Assert.Contains("Server.csproj", solutionFileContents);
            Assert.DoesNotContain("Client.csproj", solutionFileContents);
        }

        [Fact]
        public void PrintInstructions_Basic()
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
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining($"Description: Manual actions needed")
                .And.HaveStdOutContaining($"Manual instructions: Run the following command:")
                .And.HaveStdOutContaining($"Actual command: setup.cmd <your project name>");
        }

        [Fact]
        public void ItCanCreateTemplate_WithAddProjectReference()
        {
            string workingDirectory = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");
            string templateLocation = _testAssetsManager.CopyTestAsset("AddProjectReference", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(workingDirectory)
                .Execute("TestAssets.AddReference");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Adding a project reference")
                .And.HaveStdOutContaining("Successfully added a reference to the project file.");
        }

        [Fact]
        public void ItCanCreateTemplate_WithAddPackageReference()
        {
            string workingDirectory = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");
            string templateLocation = _testAssetsManager.CopyTestAsset("AddPackageReference", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("TestAssets.AddReference", "-o", workingDirectory);
            cmd.Should().Pass()
                .And.HaveStdOutContaining($"Adding a package reference Newtonsoft.Json (version: {ToolsetInfo.GetNewtonsoftJsonPackageVersion()}) to project file")
                .And.HaveStdOutContaining("Successfully added a reference to the project file.");
        }

        [Fact]
        public void ItCanCreateTemplate_WithAddProjectToSolution()
        {
            string workingDirectory = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");
            string templateLocation = _testAssetsManager.CopyTestAsset("AddProjectToSolution", testAssetSubdirectory: DotnetNewTestTemplatesBasePath).WithSource().Path;
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("TestAssets.AddProjectToSolution", "-o", workingDirectory);
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Successfully added project(s) to a solution file.");
        }

        [Fact]
        public void ItCanCreateTemplate_WithRestore()
        {
            string workingDirectory = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(workingDirectory)
                .Execute("console");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("Restore succeeded.");
        }
    }
}
