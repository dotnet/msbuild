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

namespace Microsoft.DotNet.Restore.Test
{
    public class GivenThatIWantToRestoreApp : SdkTest
    {
        public GivenThatIWantToRestoreApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItRestoresAppToSpecificDirectory()
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
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(projectDirectory)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public void ItRestoresLibToSpecificDirectory()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string [] newArgs = new[] { "classlib", "-o", rootPath, "--no-restore" };
            new DotnetCommand(Log, "new")
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "--packages", dir };
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public void ItRestoresTestAppToSpecificDirectory()
        {
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore")
                .WithSource()
                .WithVersionVariables()
                .Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public void ItRestoresWithTheSpecifiedVerbosity()
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
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr()
                 .And.NotHaveStdOut();
        }
    }
}
