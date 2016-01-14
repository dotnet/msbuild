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
    public class IncrementalTestBase : TestBase
    {
        protected readonly TempDirectory _tempProjectRoot;

        private readonly string _testProjectsRoot;
        protected readonly string _mainProject;
        protected readonly string _expectedOutput;

        public IncrementalTestBase(string testProjectsRoot, string mainProject, string expectedOutput)
        {
            _testProjectsRoot = testProjectsRoot;
            _mainProject = mainProject;
            _expectedOutput = expectedOutput;

            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();

            // recursively copy projects to the temp dir and restore them
            _tempProjectRoot = root.CopyDirectory(testProjectsRoot);
            RunRestore(_tempProjectRoot.Path);
        }

        protected void TouchSourcesOfProject()
        {
            TouchSourcesOfProject(_mainProject);
        }

        protected void TouchSourcesOfProject(string projectToTouch)
        {
            foreach (var sourceFile in GetSourceFilesForProject(projectToTouch))
            {
                TouchFile(sourceFile);
            }
        }

        protected static void TouchFile(string file)
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
        }

        protected CommandResult BuildProject(bool forceIncrementalUnsafe = false, bool expectBuildFailure = false)
        {
            var outputDir = GetBinDirectory();
            var mainProject = GetProjectFile(_mainProject);

            var buildCommand = new BuildCommand(mainProject, output: outputDir, forceIncrementalUnsafe : forceIncrementalUnsafe);
            var result = buildCommand.ExecuteWithCapturedOutput();

            if (!expectBuildFailure)
            {
                result.Should().Pass();
                TestOutputExecutable(outputDir, buildCommand.GetOutputExecutableName(), _expectedOutput);
            }
            else
            {
                result.Should().Fail();
            }

            return result;
        }

        protected static void AssertProjectSkipped(string skippedProject, CommandResult buildResult)
        {
            Assert.Contains($"Project {skippedProject} was previoulsy compiled. Skipping compilation.", buildResult.StdOut);
        }

        protected static void AssertProjectCompiled(string rebuiltProject, CommandResult buildResult)
        {
            Assert.Contains($"Project {rebuiltProject} will be compiled", buildResult.StdOut, StringComparison.OrdinalIgnoreCase);
        }

        protected string GetBinDirectory()
        {
            return Path.Combine(_tempProjectRoot.Path, "bin");
        }

        protected virtual string GetProjectDirectory(string projectName)
        {
            return Path.Combine(_tempProjectRoot.Path);
        }

        protected string GetProjectFile(string projectName)
        {
            return Path.Combine(GetProjectDirectory(projectName), "project.json");
        }

        private string GetOutputFileForProject(string projectName)
        {
            return Path.Combine(GetBinDirectory(), projectName + ".dll");
        }

        private IEnumerable<string> GetSourceFilesForProject(string projectName)
        {
            return Directory.EnumerateFiles(GetProjectDirectory(projectName)).
                Where(f => f.EndsWith(".cs"));
        }

        private void RunRestore(string args)
        {
            var restoreCommand = new RestoreCommand();
            restoreCommand.Execute($"--quiet {args}").Should().Pass();
        }
    }
}