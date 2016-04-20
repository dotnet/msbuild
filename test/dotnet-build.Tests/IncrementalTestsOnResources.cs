// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTestsOnResources : IncrementalTestBase
    {
        public IncrementalTestsOnResources()
        {
            MainProject = "TestProjectWithResource";
            ExpectedOutput = "Hello World!" + Environment.NewLine;
        }

        [Fact]
        public void TestRebuildSkipsCompilationOnNonCultureResource()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectWithResource")
                                                .WithLockFiles();

            TestProjectRoot = testInstance.TestRoot;

            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject, _appFrameworkFullName);

            buildResult = BuildProject();
            buildResult.Should().HaveSkippedProjectCompilation(MainProject, _appFrameworkFullName);
        }
    }
}
