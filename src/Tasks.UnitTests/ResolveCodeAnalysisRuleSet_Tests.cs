// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Tasks;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ResolveAnalyzerRuleSet_Tests
    {
        private class TemporaryFile : IDisposable
        {
            private readonly string _fullPath;

            public TemporaryFile(string fullPath, string contents)
            {
                _fullPath = fullPath;

                File.WriteAllText(fullPath, contents);
            }

            public void Dispose()
            {
                File.Delete(_fullPath);
            }
        }

        private class TemporaryDirectory : IDisposable
        {
            private readonly string _path;

            public TemporaryDirectory(string path)
            {
                _path = path;

                Directory.CreateDirectory(path);
            }

            public void Dispose()
            {
                FileUtilities.DeleteWithoutTrailingBackslash(_path, recursive: true);
            }
        }

        [Fact]
        public void GetResolvedRuleSetPath_FullPath_NonExistent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string codeAnalysisRuleSet = @"C:\foo\bar\CodeAnalysis.ruleset";

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        public void GetResolvedRuleSetPath_FullPath_Existent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string codeAnalysisRuleSet = Path.Combine(Path.GetTempPath(), @"CodeAnalysis.ruleset");

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            using (new TemporaryFile(codeAnalysisRuleSet, "foo"))
            {
                bool result = task.Execute();
                string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

                Assert.True(result);
                Assert.Equal(expected: codeAnalysisRuleSet, actual: resolvedRuleSet);
                mockEngine.AssertLogDoesntContain("MSB3884");
            }
        }

        [Fact]
        public void GetResolvedRuleSetPath_SimpleNameAlone_NonExistent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            task.CodeAnalysisRuleSet = @"CodeAnalysis.ruleset";
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        public void GetResolvedRuleSetPath_SimpleNameAndProjectDirectory_Existent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string codeAnalysisRuleSet = @"CodeAnalysis.ruleset";
            string projectDirectory = Path.GetTempPath();

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = projectDirectory;
            task.CodeAnalysisRuleSetDirectories = null;

            string ruleSetFullPath = Path.Combine(projectDirectory, codeAnalysisRuleSet);

            using (new TemporaryFile(ruleSetFullPath, "foo"))
            {
                bool result = task.Execute();
                string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

                Assert.True(result);
                Assert.Equal(expected: codeAnalysisRuleSet, actual: resolvedRuleSet);
                mockEngine.AssertLogDoesntContain("MSB3884");
            }
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetResolvedRuleSetPath_SimpleNameAndProjectDirectory_NonExistent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string projectDirectory = Path.GetTempPath();
            string codeAnalysisRuleSet = Path.GetRandomFileName() + ".ruleset";

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = projectDirectory;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        public void GetResolvedRuleSetPath_SimpleNameAndDirectories_Existent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string codeAnalysisRuleSet = @"CodeAnalysis.ruleset";
            var directory = Path.GetTempPath();

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = new[] { directory };

            string ruleSetFullPath = Path.Combine(directory, codeAnalysisRuleSet);

            using (new TemporaryFile(ruleSetFullPath, "foo"))
            {
                bool result = task.Execute();
                string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

                Assert.True(result);
                Assert.Equal(expected: ruleSetFullPath, actual: resolvedRuleSet);
                mockEngine.AssertLogDoesntContain("MSB3884");
            }
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetResolvedRuleSetPath_SimpleNameAndDirectories_NonExistent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string directory = Path.GetTempPath();

            task.CodeAnalysisRuleSet = Path.GetRandomFileName() + ".ruleset";
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = new[] { directory };

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetResolvedRuleSetPath_RelativePath_WithProject_NonExistent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string subdirectoryName = Path.GetRandomFileName();
            string projectDirectory = Path.GetTempPath();

            task.CodeAnalysisRuleSet = Path.Combine(subdirectoryName, "CodeAnalysis.ruleset");
            task.MSBuildProjectDirectory = projectDirectory;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetResolvedRuleSetPath_RelativePath_WithProject_Existent()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string subdirectoryName = Path.GetRandomFileName();
            string codeAnalysisRuleSet = Path.Combine(subdirectoryName, "CodeAnalysis.ruleset");
            string projectDirectory = Path.GetTempPath();

            task.CodeAnalysisRuleSet = codeAnalysisRuleSet;
            task.MSBuildProjectDirectory = projectDirectory;
            task.CodeAnalysisRuleSetDirectories = null;

            string ruleSetFullPath = Path.Combine(projectDirectory, codeAnalysisRuleSet);

            using (new TemporaryDirectory(Path.GetDirectoryName(ruleSetFullPath)))
            using (new TemporaryFile(ruleSetFullPath, "foo"))
            {
                bool result = task.Execute();
                string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

                Assert.True(result);
                Assert.Equal(expected: codeAnalysisRuleSet, actual: resolvedRuleSet);
                mockEngine.AssertLogDoesntContain("MSB3884");
            }
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetResolvedRuleSetPath_RelativePath_NoProject()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            string subdirectoryName = Path.GetRandomFileName();
            task.CodeAnalysisRuleSet = Path.Combine(subdirectoryName, "CodeAnalysis.ruleset");
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogContains("MSB3884");
        }

        [Fact]
        public void GetResolvedRuleSetPath_EmptyString()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            task.CodeAnalysisRuleSet = string.Empty;
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogDoesntContain("MSB3884");
        }

        [Fact]
        public void GetResolvedRuleSetPath_Null()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveCodeAnalysisRuleSet task = new ResolveCodeAnalysisRuleSet();
            task.BuildEngine = mockEngine;

            task.CodeAnalysisRuleSet = null;
            task.MSBuildProjectDirectory = null;
            task.CodeAnalysisRuleSetDirectories = null;

            bool result = task.Execute();
            string resolvedRuleSet = task.ResolvedCodeAnalysisRuleSet;

            Assert.True(result);
            Assert.Null(resolvedRuleSet);
            mockEngine.AssertLogDoesntContain("MSB3884");
        }
    }
}
