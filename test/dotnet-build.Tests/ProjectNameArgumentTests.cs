using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class ProjectNameArgumentTests : TestBase
    {
        private TestInstance _testInstance;

        [Fact]
        public void TestProjectDirectoryPath()
        {
            Test(new[] { Path.Combine("src", "L21") }, new[] { "L21" });
        }

        [Fact]
        public void TestProjectFile()
        {
            Test(new[] { Path.Combine("src", "L21", "project.json") }, new[] { "L21" });
        }

        [Fact]
        public void TestMultipleProjectDirectories()
        {
            Test(new[]
            {
                Path.Combine("src", "L21"),
                Path.Combine("src", "L11")
            },
            new[] { "L21", "L11" });
        }

        [Fact]
        public void TestMultipleProjectFiles()
        {
            Test(new[]
            {
                Path.Combine("src", "L21", "project.json"),
                Path.Combine("src", "L11", "project.json"),
            },
            new[] { "L21", "L11" });
        }

        [Fact]
        public void TestGlobbing()
        {
            Test(new[]
            {
                Path.Combine("src", "**", "project.json")
            },
            new[] { "L21", "L11", "L12" });
        }

        [Fact]
        public void TestMultipleGlobbing()
        {
            Test(new[]
            {
                Path.Combine("src", "L1*", "project.json"),
                Path.Combine("src", "L2*", "project.json")
            },
            new[] { "L11", "L12", "L21", "L22" });
        }

        [Fact]
        public void TestFailsWhenNoGlobbingNoMatch()
        {
            Test(new[]
            {
                Path.Combine("src", "L33*", "project.json")
            },
            null);
        }

        [Fact]
        public void TestFailsFileDoedNotExist()
        {
            Test(new[]
            {
                Path.Combine("src", "L33", "project.json")
            },
            null);
        }

        [Fact]
        public void TestFindsProjectJsonInCurrentDirectoryWithNoArguments()
        {
            Test(new string[] { }, new[] { "L21" }, workingDirectory: Path.Combine("src", "L21"));
        }
        [Fact]
        public void TestFailsIfNoProjectJsonInCurrentDirectoryWithNoArguments()
        {
            Test(new string[] { }, null, workingDirectory: "src");
        }

        private void Test(IEnumerable<string> inputs, IEnumerable<string> expectedProjects, string workingDirectory = null, [CallerMemberName] string testName = null)
        {
            var instance = TestAssetsManager.CreateTestInstance("TestProjectToProjectDependencies", testName)
                .WithLockFiles()
                .WithBuildArtifacts();
            string args = string.Join(" ", inputs);

            workingDirectory = workingDirectory != null
                ? Path.Combine(instance.TestRoot, workingDirectory)
                : instance.TestRoot;

            var result = new TestCommand("dotnet")
            {
                WorkingDirectory = Path.Combine(workingDirectory)
            }.ExecuteWithCapturedOutput("--verbose build --no-dependencies " + args);
            if (expectedProjects != null)
            {
                result.Should().Pass();
                foreach (var expectedProject in expectedProjects)
                {
                    result.Should().HaveSkippedProjectCompilation(expectedProject, NuGetFramework.Parse("netstandard1.5").DotNetFrameworkName);
                }
            }
            else
            {
                result.Should().Fail();
            }
        }
    }
}
