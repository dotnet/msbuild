// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.TestFramework;
using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTests : IncrementalTestBase
    {
        public IncrementalTests()
        {
            MainProject = "TestSimpleIncrementalApp";
            ExpectedOutput = "Hello World!" + Environment.NewLine;
        }

        private TestInstance _testInstance;

        private void CreateTestInstance()
        {
            _testInstance = TestAssetsManager.CreateTestInstance("TestSimpleIncrementalApp")
                                             .WithLockFiles();
            TestProjectRoot = _testInstance.TestRoot;
        }

        [Fact]
        public void TestNoIncrementalFlag()
        {
            CreateTestInstance();

            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            buildResult = BuildProject(noIncremental: true);
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
            Assert.Contains("[Forced Unsafe]", buildResult.StdOut);
        }

        [Fact]
        public void TestRebuildMissingPdb()
        {
            CreateTestInstance();
            TestDeleteOutputWithExtension("pdb");
        }

        [Fact]
        public void TestRebuildMissingDll()
        {
            CreateTestInstance();
            TestDeleteOutputWithExtension("dll");
        }

        [Fact]
        public void TestRebuildMissingXml()
        {
            CreateTestInstance();
            TestDeleteOutputWithExtension("xml");
        }

        [Fact]
        public void TestNoLockFile()
        {
            CreateTestInstance();
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            var lockFile = Path.Combine(TestProjectRoot, "project.lock.json");
            Assert.True(File.Exists(lockFile));

            File.Delete(lockFile);
            Assert.False(File.Exists(lockFile));

            buildResult = BuildProject(expectBuildFailure: true);
            Assert.Contains("does not have a lock file", buildResult.StdErr);
            Assert.Contains("dotnet restore", buildResult.StdErr);
        }

        [Fact]
        public void TestModifiedVersionFile()
        {
            CreateTestInstance();
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // change version file
            var versionFile = Path.Combine(GetIntermediaryOutputPath(), ".SDKVersion");
            File.Exists(versionFile).Should().BeTrue();
            File.AppendAllText(versionFile, "text");

            // assert rebuilt
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
        }

        [Fact]
        public void TestNoVersionFile()
        {
            CreateTestInstance();
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // delete version file
            var versionFile = Path.Combine(GetIntermediaryOutputPath(), ".SDKVersion");
            File.Exists(versionFile).Should().BeTrue();
            File.Delete(versionFile);
            File.Exists(versionFile).Should().BeFalse();

            // assert build skipped due to no version file
            BuildProject().Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);

            // the version file should have been regenerated during the build, even if compilation got skipped
            File.Exists(versionFile).Should().BeTrue();
        }

        [Fact]
        public void TestRebuildDeletedSource()
        {
            CreateTestInstance();
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            var sourceFile = Path.Combine(GetProjectDirectory(MainProject), "Program2.cs");
            File.Delete(sourceFile);
            Assert.False(File.Exists(sourceFile));

            // second build; should get rebuilt since we deleted a source file
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // third build; incremental cache should have been regenerated and project skipped
            BuildProject().Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);
        }

        [Fact]
        public void TestRebuildRenamedSource()
        {
            CreateTestInstance();
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            var sourceFile = Path.Combine(GetProjectDirectory(MainProject), "Program2.cs");
            var destinationFile = Path.Combine(Path.GetDirectoryName(sourceFile), "ProgramNew.cs");
            File.Move(sourceFile, destinationFile);
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destinationFile));

            // second build; should get rebuilt since we renamed a source file
            buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // third build; incremental cache should have been regenerated and project skipped
            BuildProject().Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);
        }

        [Fact]
        public void TestRebuildDeletedSourceAfterCliChanged()
        {
            CreateTestInstance();
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // change version file
            var versionFile = Path.Combine(GetIntermediaryOutputPath(), ".SDKVersion");
            File.Exists(versionFile).Should().BeTrue();
            File.AppendAllText(versionFile, "text");

            // delete a source file
            var sourceFile = Path.Combine(GetProjectDirectory(MainProject), "Program2.cs");
            File.Delete(sourceFile);
            Assert.False(File.Exists(sourceFile));

            // should get rebuilt since we changed version file and deleted source file
            BuildProject().Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            // third build; incremental cache should have been regenerated and project skipped
            BuildProject().Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);
        }

        [Fact]
        public void TestRebuildChangedLockFile()
        {
            CreateTestInstance();
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            var lockFile = Path.Combine(TestProjectRoot, "project.lock.json");
            TouchFile(lockFile);

            buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
        }

        [Fact]
        public void TestRebuildChangedProjectFile()
        {
            CreateTestInstance();
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            TouchFile(GetProjectFile(MainProject));

            buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
        }

        // regression for https://github.com/dotnet/cli/issues/965
        [Fact]
        public void TestInputWithSameTimeAsOutputCausesProjectToCompile()
        {
            CreateTestInstance();
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            var outputTimestamp = SetAllOutputItemsToSameTime();

            // set an input to have the same last write time as an output item
            // this should trigger recompilation to account for file systems with second timestamp granularity
            // (an input file that changed within the same second as the previous outputs should trigger a rebuild)
            File.SetLastWriteTime(GetProjectFile(MainProject), outputTimestamp);

            buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
        }

        private DateTime SetAllOutputItemsToSameTime()
        {
            var now = DateTime.Now;
            foreach (var f in Directory.EnumerateFiles(GetCompilationOutputPath()))
            {
                File.SetLastWriteTime(f, now);
            }
            return now;
        }

        private void TestDeleteOutputWithExtension(string extension)
        {

            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            Reporter.Verbose.WriteLine($"Files in {GetBinRoot()}");
            foreach (var file in Directory.EnumerateFiles(GetBinRoot()))
            {
                Reporter.Verbose.Write($"\t {file}");
            }

            // delete output files with extensions
            foreach (var outputFile in Directory.EnumerateFiles(GetBinRoot()).Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return fileName.StartsWith(MainProject, StringComparison.OrdinalIgnoreCase) &&
                       fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }))
            {
                Reporter.Output.WriteLine($"Deleted {outputFile}");

                File.Delete(outputFile);
                Assert.False(File.Exists(outputFile));
            }

            // second build; should get rebuilt since we deleted an output item
            buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
        }
    }
}