// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using System.IO;

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
            var cmd = new DotnetCommand(Log).Execute("new", "install", templateLocation, "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("new", "console", "--debug:custom-hive", tempSettingsDir.Path, "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir.Path, "MyConsole");

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute("new", "TestAssets.ClassTemplate", "--debug:custom-hive", tempSettingsDir.Path, "--debug:enable-project-context", "--name", "MyTestClass");
            cmd.Should().Pass();

            string testFilePath = Path.Combine(projectPath, "MyTestClass.cs");

            Assert.True(File.Exists(testFilePath));
            Assert.Contains("namespace MyConsole", File.ReadAllText(testFilePath));

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute("build");

            cmd.Should().Pass();
        }

        [Fact]
        public void TestClass_BasicTest()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/TestClassTemplate");
            var cmd = new DotnetCommand(Log).Execute("new", "install", templateLocation, "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("new", "xunit", "--debug:custom-hive", tempSettingsDir.Path, "--name", "MyTestProject");
            cmd.Should().Pass();


            string projectPath = Path.Combine(tempDir.Path, "MyTestProject");

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute("new", "TestAssets.TestClassTemplate", "--debug:custom-hive", tempSettingsDir.Path, "--debug:enable-project-context", "--name", "MyTestClass");
            cmd.Should().Pass();

            string testFilePath = Path.Combine(projectPath, "MyTestClass.cs");

            Assert.True(File.Exists(testFilePath));
            Assert.Contains("namespace MyTestProject", File.ReadAllText(testFilePath));

            cmd = new DotnetCommand(Log)
                 .WithWorkingDirectory(projectPath)
                 .Execute("build");

            cmd.Should().Pass();
        }

        [Fact]
        public void ListFiltersOutRestrictedTemplates()
        {
            TestDirectory tempDir = _testAssetsManager.CreateTestDirectory();
            TestDirectory tempSettingsDir = _testAssetsManager.CreateTestDirectory();

            string templateLocation = GetTestTemplatePath("Item/TestClassTemplate");
            var cmd = new DotnetCommand(Log).Execute("new", "install", templateLocation, "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();
            templateLocation = GetTestTemplatePath("Item/ClassTemplate");
            cmd = new DotnetCommand(Log).Execute("new", "install", templateLocation, "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();

            cmd = new DotnetCommand(Log).Execute("new", "list", "--debug:enable-project-context", "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();
            cmd.StdOut.Should().NotContain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(tempDir.Path)
                .Execute("new", "console", "--debug:custom-hive", tempSettingsDir.Path, "--name", "MyConsole");
            cmd.Should().Pass();

            string projectPath = Path.Combine(tempDir.Path, "MyConsole");

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectPath)
                .Execute("new", "list", "--debug:enable-project-context", "--debug:custom-hive", tempSettingsDir.Path);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("TestAssets.ClassTemplate").And.NotContain("TestAssets.TestClassTemplate");
        }



        private static string GetTestTemplatePath(string templateName)
        {
            string templateFolder = Path.Combine(Path.GetDirectoryName(typeof(NewCommandTests).Assembly.Location) ?? string.Empty, "TestTemplates", templateName);
            Assert.True(Directory.Exists(templateFolder));
            return Path.GetFullPath(templateFolder);
        }
    }
}
