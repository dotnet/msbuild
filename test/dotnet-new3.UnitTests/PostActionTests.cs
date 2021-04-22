// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class PostActionTests
    {
        private readonly ITestOutputHelper _log;

        public PostActionTests(ITestOutputHelper log)
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
        public void Restore_Basic(string templateLocation, string templateName, string targetSubfolder = "")
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

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

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(Path.Combine(workingDirectory, targetSubfolder))
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
        public void Restore_SourceRenameTest(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

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

            new DotnetCommand(_log, "build", "--no-restore")
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
        public void Restore_RestoreOneProjectFromTwo(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

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

            new DotnetCommand(_log, "build", "src/TemplateApplication", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication");

            new DotnetCommand(_log, "build", "test/TemplateApplication.Tests", "--no-restore")
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should().Fail()
                  .And.NotHaveStdOutContaining("Build succeeded.")
                  .And.HaveStdOutContaining("TemplateApplication.Tests");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames")]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames2", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames2")]
        public void Restore_SourceRenameTwoProjectsTest(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

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

            new DotnetCommand(_log, "build", "TemplateApplication.UI", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication.UI");

            new DotnetCommand(_log, "build", "TemplateApplication.Tests", "--no-restore")
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
            string templateLocation = "PostActions/RunScript/Basic";
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
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

        [Theory]
        [InlineData("PostActions/AddReference/Basic", "TestAssets.PostActions.AddReference.Basic")]
        [InlineData("PostActions/AddReference/BasicWithFiles", "TestAssets.PostActions.AddReference.BasicWithFiles")]
        public void AddReference_Basic(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("reference: Newtonsoft.Json")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add");

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Theory]
        [InlineData("PostActions/AddProjectToSolution/Basic", "TestAssets.PostActions.AddProjectToSolution.Basic")]
        [InlineData("PostActions/AddProjectToSolution/BasicWithFiles", "TestAssets.PostActions.AddProjectToSolution.BasicWithFiles")]
        public void AddProjectToSolution_Basic(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

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
                .And.HaveStdOutContaining("Adding project reference(s) to solution file. Running dotnet sln")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.");

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(workingDirectory, "MySolution.sln")));
        }

        [Theory]
        [InlineData("PostActions/AddProjectToSolution/BasicWithIndexes", "TestAssets.PostActions.AddProjectToSolution.BasicWithIndexes")]
        public void AddProjectToSolution_PrimaryOutputIndexes(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Adding project reference(s) to solution file. Running dotnet sln")
                .And.HaveStdOutContaining("Successfully added")
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
            string templateLocation = "PostActions/Instructions/Basic";
            string templateName = "TestAssets.PostActions.Instructions.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            var commandResult = new DotnetNewCommand(_log, templateName)
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
    }
}
