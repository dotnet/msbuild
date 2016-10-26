// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Run3.Tests
{
    public class GivenDotnetRun3BuildsCsproj : TestBase
    {
        [Fact]
        public void ItCanRunAMSBuildProject()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new Build3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new Run3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItBuildsTheProjectBeforeRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new Run3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItCanRunAMSBuildProjectWhenSpecifyingAFramework()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new Run3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--framework netcoreapp1.0")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItReportsAGoodErrorWhenProjectHasMultipleFrameworks()
        {
            var testAppName = "MSBuildAppWithMultipleFrameworks";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should()
                .Pass();

            // use --no-build so this test can run on all platforms.
            // the test app targets net451, which can't be built on non-Windows
            new Run3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--no-build")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("--framework");
        }

        [Fact]
        public void It_runs_portable_apps_from_a_different_path_after_building()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new Build3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string workingDirectory = Directory.GetParent(testProjectDirectory).FullName;
            new Run3Command()
                .WithWorkingDirectory(workingDirectory)
                .ExecuteWithCapturedOutput($"--no-build --project {Path.Combine(testProjectDirectory, testAppName)}.csproj")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void It_runs_portable_apps_from_a_different_path_without_building()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string workingDirectory = Directory.GetParent(testProjectDirectory).FullName;
            new Run3Command()
                .WithWorkingDirectory(workingDirectory)
                .ExecuteWithCapturedOutput($"--project {Path.Combine(testProjectDirectory, testAppName)}.csproj")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }
    }
}