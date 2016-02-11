// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTestsTransitiveRuntime : IncrementalTestBase
    {
        private const string TestLibraryWithAppDependency = "TestLibraryWithAppDependency";
        private const string LibraryProject = "TestLibrary";
        private const string AppProject = "TestApp";

        public IncrementalTestsTransitiveRuntime()
        {
            MainProject = "TestLibrary2";
            ExpectedOutput = "This string came from the test library!" + Environment.NewLine;
        }

        [Fact]
        public void TestSkipsRebuildWithTransitiveExeDependency()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithTransitiveAppDependency")
                                                .WithLockFiles();
            TestProjectRoot = testInstance.TestRoot;
            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject);
            buildResult.Should().HaveCompiledProject(TestLibraryWithAppDependency);
            buildResult.Should().HaveCompiledProject(AppProject);
            buildResult.Should().HaveCompiledProject(LibraryProject);

            buildResult = BuildProject();

            buildResult.Should().HaveSkippedProjectCompilation(MainProject);
            buildResult.Should().HaveSkippedProjectCompilation(TestLibraryWithAppDependency);
            buildResult.Should().HaveSkippedProjectCompilation(AppProject);
            buildResult.Should().HaveSkippedProjectCompilation(LibraryProject);
        }

        protected override string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TestProjectRoot, projectName);
        }
    }
}