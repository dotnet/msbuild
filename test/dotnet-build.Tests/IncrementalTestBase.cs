// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTestBase : TestBase
    {
        protected virtual string MainProject
        {
            get; set;
        }

        protected virtual string ExpectedOutput
        {
            get; set;
        }

        protected virtual string TestProjectRoot
        {
            get; set;
        }

        protected IncrementalTestBase()
        {

        }


        public IncrementalTestBase(string testProjectsRoot, string mainProject, string expectedOutput)
        {
            MainProject = mainProject;
            ExpectedOutput = expectedOutput;
            TestProjectRoot = testProjectsRoot;
        }

        protected void TouchSourcesOfProject()
        {
            TouchSourcesOfProject(MainProject);
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

        protected CommandResult BuildProject(bool noDependencies = false, bool noIncremental = false, bool expectBuildFailure = false)
        {
            var mainProjectFile = GetProjectFile(MainProject);
            return BuildProject(mainProjectFile, noDependencies, noIncremental, expectBuildFailure);
        }

        protected CommandResult BuildProject(string projectFile, bool noDependencies = false, bool noIncremental = false, bool expectBuildFailure = false)
        {
            var buildCommand = new BuildCommand(projectFile, output: GetOutputDir(), framework: "dnxcore50", noIncremental: noIncremental, noDependencies : noDependencies);
            var result = buildCommand.ExecuteWithCapturedOutput();

            if (!expectBuildFailure)
            {
                result.Should().Pass();
                TestOutputExecutable(GetOutputExePath(), buildCommand.GetOutputExecutableName(), ExpectedOutput);
            }
            else
            {
                result.Should().Fail();
            }

            return result;
        }

        protected virtual string GetOutputExePath()
        {
            return GetBinRoot();
        }

        protected virtual string GetOutputDir()
        {
            return GetBinRoot();
        }

        protected string GetBinRoot()
        {
            return Path.Combine(TestProjectRoot, "bin");
        }

        protected virtual string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TestProjectRoot);
        }

        protected string GetProjectFile(string projectName)
        {
            return Path.Combine(GetProjectDirectory(projectName), "project.json");
        }

        private string GetOutputFileForProject(string projectName)
        {
            return Path.Combine(GetCompilationOutputPath(), projectName + ".dll");
        }

        private IEnumerable<string> GetSourceFilesForProject(string projectName)
        {
            return Directory.EnumerateFiles(GetProjectDirectory(projectName)).
                Where(f => f.EndsWith(".cs"));
        }

        protected string GetCompilationOutputPath()
        {
            var executablePath = Path.Combine(GetBinRoot(), "Debug", "dnxcore50");

            return executablePath;
        }
    }
}
