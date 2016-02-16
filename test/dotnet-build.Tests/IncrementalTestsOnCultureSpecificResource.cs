// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTestsOnCultureSpecificResource : IncrementalTestBase
    {
        public IncrementalTestsOnCultureSpecificResource()
        {
            MainProject = "TestProjectWithCultureSpecificResource";
            ExpectedOutput = "Hello World!" + Environment.NewLine + "Bonjour!" + Environment.NewLine;
        }

        // Sridhar-MS - temporarily disable the test and investigate why it is failing in CI machine.
        // [Fact]
        public void TestRebuildSkipsCompilationOnNonCultureResource()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectWithCultureSpecificResource")
                                                .WithLockFiles();

            TestProjectRoot = testInstance.TestRoot;

            var buildResult = BuildProject();
            buildResult.Should().HaveCompiledProject(MainProject);

            buildResult = BuildProject();

            buildResult.Should().HaveSkippedProjectCompilation(MainProject);
        }
    }
}
