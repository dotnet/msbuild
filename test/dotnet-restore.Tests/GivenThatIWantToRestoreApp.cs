// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using System.Linq;

namespace Microsoft.DotNet.Restore.Test
{
    public class GivenThatIWantToRestoreApp : TestBase
    {
        private static string RepoRootNuGetConfig = Path.Combine(RepoDirectoriesProvider.RepoRoot, "NuGet.config");

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            var sln = "TestAppWithSlnAndSolutionFolders";
            var projectDirectory = TestAssets
                .Get(sln)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            string args = $"--configfile {RepoRootNuGetConfig} --packages \"{fullPath}\"";
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new RestoreCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresLibToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string newArgs = $"classlib -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string args = $"--configfile {RepoRootNuGetConfig} --packages \"{dir}\"";
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresTestAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = TestAssets.Get("VSTestCore").CreateInstance().WithSourceFiles().Root.FullName;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string args = $"--configfile {RepoRootNuGetConfig} --packages \"{dir}\"";
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresWithTheSpecifiedVerbosity(bool useStaticGraphEvaluation)
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string args = $"--configfile {RepoRootNuGetConfig} --packages \"{dir}\" --verbosity quiet";
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new RestoreCommand()
                 .WithWorkingDirectory(rootPath)
                 .ExecuteWithCapturedOutput(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr()
                 .And.NotHaveStdOut();
        }

        private static string HandleStaticGraphEvaluation(bool useStaticGraphEvaluation, string args) =>
            useStaticGraphEvaluation ?
                args + " /p:RestoreUseStaticGraphEvaluation=true" : args;
    }
}
