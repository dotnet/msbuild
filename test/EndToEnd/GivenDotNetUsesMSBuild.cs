// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : TestBase
    {
        [Fact]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();
         
            var testProjectDirectory = testInstance.Root;

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/9688")]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");;
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/9688")]
        public void ItCanRunAToolThatInvokesADependencyToolInACSProj()
        {
            var repoDirectoriesProvider = new RepoDirectoriesProvider();

            var testInstance = TestAssets.Get("TestAppWithProjDepTool")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var configuration = "Debug";

            var testProjectDirectory = testInstance.Root;

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute($"-c {configuration} ")
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput(
                    $"-d dependency-tool-invoker -c {configuration} -f netcoreapp2.2 portable")
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello Portable World!");;
        }
    }
}
