// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Run.Tests
{
    public class RunTests : TestBase
    {
        private const string PortableAppsTestBase = "PortableTests";
        private const string RunTestsBase = "RunTestsApps";

        [WindowsOnlyFact]
        public void RunsSingleTarget()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppFullClr"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void RunsDefaultWhenPresent()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppMultiTarget"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void FailsWithMultipleTargetAndNoDefault()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppMultiTargetNoCoreClr"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Fail();
        }

        [Fact]
        public void ItRunsPortableApps()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "PortableApp"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/1940")]
        public void ItRunsPortableAppsWithNative()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "PortableAppWithNative"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void ItRunsStandaloneApps()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "StandaloneApp"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }


        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            // copy all the files to temp dir
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}
