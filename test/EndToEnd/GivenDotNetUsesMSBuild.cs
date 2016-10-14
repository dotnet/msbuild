// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : TestBase
    {
        public static void Main() 
        {
        } 

        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            using (DisposableDirectory directory = Temp.CreateDirectory())
            {
                string projectDirectory = directory.Path;

                new NewCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute("")
                    .Should()
                    .Pass();

                new Restore3Command()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute("/p:SkipInvalidConfigurations=true")
                    .Should()
                    .Pass();

                new Build3Command()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should()
                    .Pass();

                //TODO: https://github.com/dotnet/sdk/issues/187 - remove framework from below.
                new Run3Command()
                    .WithWorkingDirectory(projectDirectory)
                    .ExecuteWithCapturedOutput("--framework netcoreapp1.0")
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World!");

                var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
                binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

                new Clean3Command()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should()
                    .Pass();

                binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
            }
        }

        [Fact]
        public void ItCanRunToolsInACSProj()
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

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput("portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }

        [Fact]
        public void ItCanRunAToolThatInvokesADependencyToolInACSProj()
        {
            var repoDirectoriesProvider = new RepoDirectoriesProvider();
            var testAppName = "MSBuildTestAppWithToolInDependencies";
            var testInstance = TestAssetsManager
                .CreateTestInstance(testAppName);

            var testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute($"-s {repoDirectoriesProvider.TestPackages}")
                .Should()
                .Pass();

            new Build3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("dependency-tool-invoker -f netcoreapp1.0 portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }
    }
}
