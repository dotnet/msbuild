// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.New.Tests
{
    public class MSBuildEvaluationTests : SdkTest
    {
        public MSBuildEvaluationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void Class_BasicTest()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("console", "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir.Path, "MyConsole");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass");
            cmd.Should().Pass();

            string testFilePath = Path.Combine(projectPath, "MyTestClass.cs");

            Assert.True(File.Exists(testFilePath));
            Assert.Contains("namespace MyConsole", File.ReadAllText(testFilePath));

            cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute();

            cmd.Should().Pass();
        }

        [Fact]
        public void TestClass_BasicTest()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/TestClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("xunit", "--name", "MyTestProject");
            cmd.Should().Pass();


            string projectPath = Path.Combine(tempDir.Path, "MyTestProject");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                .Execute("TestAssets.TestClassTemplate", "--name", "MyTestClass");
            cmd.Should().Pass();

            string testFilePath = Path.Combine(projectPath, "MyTestClass.cs");

            Assert.True(File.Exists(testFilePath));
            Assert.Contains("namespace MyTestProject", File.ReadAllText(testFilePath));

            cmd = new DotnetBuildCommand(Log)
                 .WithWorkingDirectory(projectPath)
                 .Execute();

            cmd.Should().Pass();
        }

        [Fact]
        public void ListFiltersOutRestrictedTemplates()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/TestClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            templateLocation = GetTestTemplatePath("Item/ClassTemplate");
            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("list");
            cmd.Should().Pass();
            cmd.StdOut.Should().NotContain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("console", "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir.Path, "MyConsole");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .Execute("list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");
        }

        [Fact]
        public void MultipleProjects_BasicTest()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("console", "--name", "MyProject");
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("classlib", "--language", "F#", "--name", "MyProject");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir.Path, "MyProject");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass");
            cmd.Should().Fail()
                .And.HaveStdErrContaining("Failed to instatiate template 'ClassTemplate', the following constraints are not met:")
                .And.HaveStdErrContaining("Project capabiltities: Multiple projects found:")
                .And.HaveStdErrContaining("Specify the project to use using --project option.");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass", "--project", "MyProject.csproj");
            cmd.Should().Pass();

            cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute("MyProject.csproj");
            cmd.Should().Pass();

            cmd = new DotnetBuildCommand(Log)
            .WithWorkingDirectory(projectPath)
            .Execute("MyProject.fsproj");
            cmd.Should().Pass();
        }

        [Fact]
        public void NonSDKStyleProject_BasicTest()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .Execute("install", templateLocation);
            cmd.Should().Pass();
            string projectPath = Path.Combine(tempDir.Path, "ConsoleFullFramework");
            DirectoryCopy(GetTestTemplatePath("ConsoleFullFramework"), projectPath);

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir.Path)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass");
            cmd.Should().Fail()
                .And.HaveStdErrContaining("Failed to instatiate template 'ClassTemplate', the following constraints are not met:")
                .And.HaveStdErrContaining($"Project capabiltities: The project {Path.Combine(projectPath, "ConsoleFullFramework.csproj")} is not an SDK style project, and is not supported for evaluation.");
        }



        private static string GetTestTemplatePath(string templateName)
        {
            string templateFolder = Path.Combine(Path.GetDirectoryName(typeof(NewCommandTests).Assembly.Location) ?? string.Empty, "TestTemplates", templateName);
            Assert.True(Directory.Exists(templateFolder));
            return Path.GetFullPath(templateFolder);
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + dir.FullName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath);
            }
        }
    }
}
