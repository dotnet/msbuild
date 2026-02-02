// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class DebugUtils_Tests
    {
        [Fact]
        public void DumpExceptionToFileShouldWriteInDebugDumpPath()
        {
            ExceptionHandling.ResetDebugDumpPathInRunningTests = true;
            var exceptionFilesBefore = Directory.GetFiles(ExceptionHandling.DebugDumpPath, "MSBuild_*failure.txt");

            string[] exceptionFiles = null;

            try
            {
                ExceptionHandling.DumpExceptionToFile(new Exception("hello world"));
                exceptionFiles = Directory.GetFiles(ExceptionHandling.DebugDumpPath, "MSBuild_*failure.txt");
            }
            finally
            {
                exceptionFilesBefore.ShouldNotBeNull();
                exceptionFiles.ShouldNotBeNull();
                (exceptionFiles.Length - exceptionFilesBefore.Length).ShouldBe(1);

                var exceptionFile = exceptionFiles.Except(exceptionFilesBefore).Single();
                File.ReadAllText(exceptionFile).ShouldContain("hello world");
                File.Delete(exceptionFile);
            }
        }

        [Fact]
        public void SetDebugPath_WhenUserSetRelativePath()
        {
            using TestEnvironment env = TestEnvironment.Create();
            {
                TransientTestProjectWithFiles dummyProject = env.CreateTestProjectWithFiles(@"
            <Project xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>");

                string testCurrentDir = Path.GetDirectoryName(dummyProject.ProjectFile);
                env.SetCurrentDirectory(testCurrentDir);

                string relativePath = "./TestLogs";
                var transientEnvVar = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", relativePath);
                var transientDebugEngine = env.SetEnvironmentVariable("MSBuildDebugEngine", "1");
                try
                {
                    DebugUtils.SetDebugPath();
                    string resultPath = DebugUtils.DebugPath;
                    resultPath.ShouldNotBeNull();
                    resultPath.ShouldBe(Path.Combine(relativePath, ".MSBuild_Logs"));
                    Directory.Exists(resultPath).ShouldBeTrue();
                }
                finally
                {
                    // Reset DebugPath to not affect other tests
                    transientEnvVar.Revert();
                    transientDebugEngine.Revert();
                    DebugUtils.SetDebugPath();
                }
            }
        }

        [Fact]
        public void SetDebugPath_WhenUserSetAbsolutePath()
        {
            using TestEnvironment env = TestEnvironment.Create();
            {
                TransientTestProjectWithFiles dummyProject = env.CreateTestProjectWithFiles(@"
            <Project xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>");

                string testCurrentDir = Path.GetDirectoryName(dummyProject.ProjectFile);
                env.SetCurrentDirectory(testCurrentDir);

                string inSolutionPath = Path.Combine(testCurrentDir, "AbsoluteLogs");
                string fullInSolutionPath = Path.GetFullPath(inSolutionPath);
                var transientEnvVar = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", inSolutionPath);
                var transientDebugEngine = env.SetEnvironmentVariable("MSBuildDebugEngine", "1");
                try
                {
                    DebugUtils.SetDebugPath();
                    string resultPath = DebugUtils.DebugPath;
                    resultPath.ShouldNotBeNull();
                    resultPath.ShouldBe(Path.Combine(fullInSolutionPath, ".MSBuild_Logs"));
                }
                finally
                {
                    // Reset DebugPath to not affect other tests
                    transientEnvVar.Revert();
                    transientDebugEngine.Revert();
                    DebugUtils.SetDebugPath();
                }
            }
        }

        [Fact]
        public void SetDebugPath_WhenUserNotSetDebugPath()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles dummyProject = env.CreateTestProjectWithFiles(@"
            <Project xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>");

                string testCurrentDir = Path.GetDirectoryName(dummyProject.ProjectFile);
                env.SetCurrentDirectory(testCurrentDir);
                var transientEnvVar = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", null);
                var transientDebugEngine = env.SetEnvironmentVariable("MSBuildDebugEngine", "1");
                try
                {
                    DebugUtils.SetDebugPath();
                    string resultPath = DebugUtils.DebugPath;
                    resultPath.ShouldNotBeNull();
                    resultPath.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), ".MSBuild_Logs"));
                }
                finally
                {
                    // Reset DebugPath to not affect other tests
                    transientEnvVar.Revert();
                    transientDebugEngine.Revert();
                    DebugUtils.SetDebugPath();
                }
            }
        }

        [Fact]
        public void IsInTaskHostNode_ReturnsFalseForCentralNode()
        {
            // When running in the main test process (no /nodemode argument),
            // we should not be in a TaskHost node
            DebugUtils.IsInTaskHostNode().ShouldBeFalse();
        }
    }
}
