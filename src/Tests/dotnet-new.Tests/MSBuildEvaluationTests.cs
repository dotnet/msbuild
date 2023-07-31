// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class MSBuildEvaluationTests : BaseIntegrationTest
    {
        public MSBuildEvaluationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void Class_BasicTest()
        {
            string tempDir = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            string templateLocation = GetTestTemplateLocation("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(tempDir)
                .Execute("console", "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir, "MyConsole");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
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
            string tempDir = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            string templateLocation = GetTestTemplateLocation("Item/TestClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(tempDir)
                .Execute("xunit", "--name", "MyTestProject");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir, "MyTestProject");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
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
            string tempDir = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            string templateLocation = GetTestTemplateLocation("Item/TestClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            templateLocation = GetTestTemplateLocation("Item/ClassTemplate");
            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("list");
            cmd.Should().Pass();
            cmd.StdOut.Should().NotContain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(tempDir)
                .Execute("console", "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir, "MyConsole");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(projectPath)
                .Execute("list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");
        }

        [Fact]
        public void MultipleProjects_BasicTest()
        {
            string tempDir = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            string templateLocation = GetTestTemplateLocation("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(tempDir)
                .Execute("console", "--name", "MyProject");
            cmd.Should().Pass();

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(tempDir)
                .Execute("classlib", "--language", "F#", "--name", "MyProject");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir, "MyProject");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass");
            cmd.Should().Fail()
                .And.HaveStdErrContaining("Failed to instatiate template 'ClassTemplate', the following constraints are not met:")
                .And.HaveStdErrContaining("Project capabiltities: Multiple projects found:")
                .And.HaveStdErrContaining("Specify the project to use using --project option.");

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
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
            string tempDir = CreateTemporaryFolder();
            string tempSettingsDir = CreateTemporaryFolder("Home");

            string templateLocation = GetTestTemplateLocation("Item/ClassTemplate");
            CommandResult cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .Execute("install", templateLocation);
            cmd.Should().Pass();
            string projectPath = Path.Combine(tempDir, "ConsoleFullFramework");
            TestUtils.DirectoryCopy(GetTestTemplateLocation("ConsoleFullFramework"), projectPath, copySubDirs: true);

            cmd = new DotnetNewCommand(Log)
                .WithCustomHive(tempSettingsDir)
                .WithWorkingDirectory(projectPath)
                .Execute("TestAssets.ClassTemplate", "--name", "MyTestClass");
            cmd.Should().Fail()
                .And.HaveStdErrContaining("Failed to instatiate template 'ClassTemplate', the following constraints are not met:")
                .And.HaveStdErrContaining($"Project capabiltities: The project {Path.Combine(projectPath, "ConsoleFullFramework.csproj")} is not an SDK style project, and is not supported for evaluation.");
        }
    }
}
