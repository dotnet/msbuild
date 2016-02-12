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
        protected readonly TempDirectory TempProjectRoot;

        protected readonly string MainProject;
        protected readonly string ExpectedOutput;
        protected readonly TempDirectory Root;

        public IncrementalTestBase(string testProjectsRoot, string mainProject, string expectedOutput)
        {
            MainProject = mainProject;
            ExpectedOutput = expectedOutput;

            Root = Temp.CreateDirectory();

            TempProjectRoot = Root.CopyDirectory(testProjectsRoot);
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

            var buildCommand = new BuildCommand(mainProjectFile, output: GetBinRoot(), framework: "dnxcore50",  noIncremental : noIncremental, noDependencies : noDependencies);
            var result = buildCommand.ExecuteWithCapturedOutput();

            if (!expectBuildFailure)
            {
                result.Should().Pass();
                TestOutputExecutable(GetBinRoot(), buildCommand.GetOutputExecutableName(), ExpectedOutput);
            }
            else
            {
                result.Should().Fail();
            }

            return result;
        }

        protected string GetBinRoot()
        {
            return Path.Combine(TempProjectRoot.Path, "bin");
        }

        protected virtual string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TempProjectRoot.Path);
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