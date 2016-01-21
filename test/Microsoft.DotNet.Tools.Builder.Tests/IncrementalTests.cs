// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTests : IncrementalTestBase
    {
        private string _testProjectsRoot = @"TestProjects";
        private string _testProject = "TestProjectToProjectDependencies";
        private TempDirectory tempProjectRoot;

        public IncrementalTests() : base(
            Path.Combine("TestProjects", "TestSimpleIncrementalApp"),
            "TestSimpleIncrementalApp",
            "Hello World!" + Environment.NewLine)
        {
        }

        [Fact]
        public void TestForceIncrementalUnsafe()
        {
            var buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);

            buildResult = BuildProject(forceIncrementalUnsafe: true);
            Assert.Contains("[Forced Unsafe]", buildResult.StdOut);
        }

        [Fact]
        public void TestRebuildMissingPdb()
        {
            TestDeleteOutputWithExtension("pdb");
        }

        [Fact]
        public void TestRebuildMissingDll()
        {
            TestDeleteOutputWithExtension("dll");
        }

        [Fact]
        public void TestRebuildMissingXml()
        {
            TestDeleteOutputWithExtension("xml");
        }

        [Fact]
        public void TestNoLockFile()
        {

            var buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);

            var lockFile = Path.Combine(_tempProjectRoot.Path, "project.lock.json");
            Assert.True(File.Exists(lockFile));

            File.Delete(lockFile);
            Assert.False(File.Exists(lockFile));

            buildResult = BuildProject(expectBuildFailure : true);
            Assert.Contains("does not have a lock file", buildResult.StdErr);
        }

        [Fact]
        public void TestRebuildChangedLockFile()
        {

            var buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);

            var lockFile = Path.Combine(_tempProjectRoot.Path, "project.lock.json");
            TouchFile(lockFile);

            buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);
        }

        [Fact]
        public void TestRebuildChangedProjectFile()
        {

            var buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);

            TouchFile(GetProjectFile(_mainProject));

            buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);
        }

        private void TestDeleteOutputWithExtension(string extension)
        {

            var buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);

            Reporter.Verbose.WriteLine($"Files in {GetCompilationOutputPath()}");
            foreach (var file in Directory.EnumerateFiles(GetCompilationOutputPath()))
            {
                Reporter.Verbose.Write($"\t {file}");
            }

            // delete output files with extensions
            foreach (var outputFile in Directory.EnumerateFiles(GetCompilationOutputPath()).Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return fileName.StartsWith(_mainProject, StringComparison.OrdinalIgnoreCase) &&
                       fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }))
            {
                Reporter.Output.WriteLine($"Deleted {outputFile}");

                File.Delete(outputFile);
                Assert.False(File.Exists(outputFile));
            }

            // second build; should get rebuilt since we deleted an output item
            buildResult = BuildProject();
            AssertProjectCompiled(_mainProject, buildResult);
        }
    }
}