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
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);
            buildResult.Should().HaveCompiledProject(TestLibraryWithAppDependency, _appFrameworkFullName);
            buildResult.Should().HaveCompiledProject(AppProject, _appFrameworkFullName);
            buildResult.Should().HaveCompiledProject(LibraryProject, _appFrameworkFullName);

            buildResult = BuildProject();

            buildResult.Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);
            buildResult.Should().HaveSkippedProjectCompilation(TestLibraryWithAppDependency, _appFrameworkFullName);
            buildResult.Should().HaveSkippedProjectCompilation(AppProject, _appFrameworkFullName);
            buildResult.Should().HaveSkippedProjectCompilation(LibraryProject, _appFrameworkFullName);
        }

        protected override string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TestProjectRoot, projectName);
        }
    }
}