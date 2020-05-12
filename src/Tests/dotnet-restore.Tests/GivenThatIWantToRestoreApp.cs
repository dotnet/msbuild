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
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.DotNet.Restore.Test
{
    public class GivenThatIWantToRestoreApp : SdkTest
    {
        public GivenThatIWantToRestoreApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            var sln = "TestAppWithSlnAndSolutionFolders";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(sln)
                .WithSource()
                .Path;

            string[] args = new[] { "--packages", fullPath };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(projectDirectory)
                 .Execute(args)
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
            var testProject = new TestProject()
            {
                Name = "RestoreToDir",
                TargetFrameworks = "net5.0",
                IsSdkProject = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "12.0.3"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: useStaticGraphEvaluation.ToString());

            var rootPath = Path.Combine(testAsset.TestRoot, testProject.Name);

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            var dllCount = 0;

            if (Directory.Exists(fullPath))
            {
                dllCount = Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count();
            }

            if (dllCount == 0)
            {
                Log.WriteLine("Assets file contents:");
                Log.WriteLine(File.ReadAllText(Path.Combine(rootPath, "obj", "project.assets.json")));
            }

            Directory.Exists(fullPath).Should().BeTrue();
            dllCount.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresTestAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore")
                .WithSource()
                .WithVersionVariables()
                .Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
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
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] newArgs = new[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetCommand(Log, "new")
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "--packages", dir, "--verbosity",  "quiet" };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr()
                 .And.NotHaveStdOut();
        }

        private static string[] HandleStaticGraphEvaluation(bool useStaticGraphEvaluation, string[] args) =>
            useStaticGraphEvaluation ? 
                args.Append("/p:RestoreUseStaticGraphEvaluation=true").ToArray() :
                args;
    }
}
