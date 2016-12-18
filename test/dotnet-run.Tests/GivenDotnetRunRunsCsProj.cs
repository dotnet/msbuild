// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunBuildsCsproj : TestBase
    {
        [Fact]
        public void ItCanRunAMSBuildProject()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

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
        public void ItBuildsTheProjectBeforeRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

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
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--framework netcoreapp1.0")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");            
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
        public void ItRunsAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string args = $"--packages {dir}";

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute()
                .Should()
                .Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new RunCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }
    }
}