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

        public IncrementalTestsTransitiveRuntime() : base(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", "TestAppWithTransitiveAppDependency"),
            "TestAppWithTransitiveAppDependency",
            "This string came from the test library!" + Environment.NewLine)
        {
            Root.CopyDirectory(Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", LibraryProject));
            Root.CopyDirectory(Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", AppProject));
            Root.CopyDirectory(Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", TestLibraryWithAppDependency));
        }

        [Fact]
        public void TestSkipsRebuildWithTransitiveExeDependency()
        {
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
    }
}