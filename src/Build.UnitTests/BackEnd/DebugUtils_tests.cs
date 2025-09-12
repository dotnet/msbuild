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
        public void SetDebugPath_ShouldRedirectSolutionDirectoryPathToTemp()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string originalEnvVar = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");
                string originalDebugEngineValue = Environment.GetEnvironmentVariable("MSBUILDDEBUGENGINE");
                
                try
                {
                    // Enable debug engine and set a relative path
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", "1");
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", "./TestLogs");

                    // Call SetDebugPath
                    DebugUtils.SetDebugPath();

                    string resultPath = DebugUtils.DebugPath;

                    // Should be redirected to temp directory with MSBuild_Logs
                    resultPath.ShouldNotBeNull();
                    resultPath.ShouldContain("MSBuild_Logs");
                    resultPath.ShouldContain(FileUtilities.TempFileDirectory);
                    resultPath.ShouldNotContain("TestLogs");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", originalEnvVar);
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", originalDebugEngineValue);
                }
            }
        }

        [Fact]
        public void SetDebugPath_ShouldRedirectAbsolutePathInSolutionToTemp()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string originalEnvVar = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");
                string originalDebugEngineValue = Environment.GetEnvironmentVariable("MSBUILDDEBUGENGINE");
                
                try
                {
                    // Enable debug engine and set the test path
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", "1");
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", Path.Combine(Directory.GetCurrentDirectory(), "AbsoluteLogs"));

                    // Call SetDebugPath
                    DebugUtils.SetDebugPath();

                    string resultPath = DebugUtils.DebugPath;

                    // Should be redirected to temp directory with MSBuild_Logs
                    resultPath.ShouldNotBeNull();
                    resultPath.ShouldContain("MSBuild_Logs");
                    resultPath.ShouldContain(FileUtilities.TempFileDirectory);
                    resultPath.ShouldNotBe(Path.Combine(Directory.GetCurrentDirectory(), "AbsoluteLogs"));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", originalEnvVar);
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", originalDebugEngineValue);
                }
            }
        }

        [Fact]
        public void SetDebugPath_ShouldNotRedirectPathOutsideSolution()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles dummyProject = env.CreateTestProjectWithFiles(@"
            <Project xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>");

                string testCurrentDir = Path.GetDirectoryName(dummyProject.ProjectFile);
                Directory.CreateDirectory(testCurrentDir);  

                string originalCurrentDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(testCurrentDir);  

                string originalEnvVar = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");
                string originalDebugEngineValue = Environment.GetEnvironmentVariable("MSBUILDDEBUGENGINE");

                try
                {
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", "1");
                    string outsidePath = Path.Combine(FileUtilities.TempFileDirectory, "ExternalLogs");

                    string fullCurrentDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                    string fullOutsidePath = Path.GetFullPath(outsidePath);
                    Console.WriteLine($"CurrentDir: {fullCurrentDir}");
                    Console.WriteLine($"OutsidePath: {fullOutsidePath}");

                    Directory.CreateDirectory(outsidePath);
                    string dummyFile = Path.Combine(outsidePath, "dummy.txt");
                    File.WriteAllText(dummyFile, "test");
                    File.Delete(dummyFile);  

                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", outsidePath);
                    DebugUtils.SetDebugPath();
                    string resultPath = DebugUtils.DebugPath;

                    resultPath.ShouldBe(fullOutsidePath);
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalCurrentDir);  // Restore current dir
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", originalEnvVar);
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGENGINE", originalDebugEngineValue);
                }
            }
        }
    }
}
