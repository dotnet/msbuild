// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TaskEnvironment_Tests
    {
        private const string StubEnvironmentName = "Stub";
        private const string MultithreadedEnvironmentName = "Multithreaded";

        public static TheoryData<string> EnvironmentTypes =>
            new TheoryData<string>
            {
                StubEnvironmentName,
                MultithreadedEnvironmentName
            };

        // CA2000 is suppressed because the caller is responsible for disposal via DisposeTaskEnvironment
#pragma warning disable CA2000
        private static TaskEnvironment CreateTaskEnvironment(string environmentType)
        {
            return environmentType switch
            {
                StubEnvironmentName => TaskEnvironmentHelper.CreateForTest(),
                MultithreadedEnvironmentName => new TaskEnvironment(new MultiThreadedTaskEnvironmentDriver(GetResolvedTempPath())),
                _ => throw new ArgumentException($"Unknown environment type: {environmentType}")
            };
        }
#pragma warning restore CA2000

        private static void DisposeTaskEnvironment(TaskEnvironment taskEnvironment)
        {
            taskEnvironment?.Dispose();
        }

        /// <summary>
        /// Gets the fully resolved temp directory path. On macOS, Path.GetTempPath() returns paths starting with "/var/..."
        /// which is a symbolic link that resolves to "/private/var/...".
        /// The StubTaskEnvironmentDriver uses Directory.SetCurrentDirectory which resolves symlinks, while
        /// MultiThreadedTaskEnvironmentDriver stores the path as-is.
        /// This method ensures we get the canonical path to avoid test failures when comparing paths between the two drivers.
        /// </summary>
        /// <returns>The fully resolved temp directory path</returns>
        private static string GetResolvedTempPath()
        {
            string tempPath = Path.GetFullPath(Path.GetTempPath());

            // On macOS, /tmp is typically a symbolic link to /private/tmp
            // And /var is typically a symbolic link to /private/var
            // We need to manually resolve this because StubTaskEnvironmentDriver (via Directory.SetCurrentDirectory)
            // will resolve the symlink, but MultiThreadedTaskEnvironmentDriver will not.
            // By resolving it here, we ensure both drivers operate on the same canonical path string.
#if NET5_0_OR_GREATER
            bool IsMacOS = OperatingSystem.IsMacOS();
#else
            bool IsMacOS = Microsoft.Build.Framework.OperatingSystem.IsMacOS();
#endif
            if (IsMacOS)
            {
                // On macOS, /tmp -> /private/tmp and /var -> /private/var
                if (tempPath.StartsWith("/tmp/", StringComparison.OrdinalIgnoreCase) || string.Equals(tempPath, "/tmp", StringComparison.OrdinalIgnoreCase))
                {
                    tempPath = "/private" + tempPath;
                }
                else if (tempPath.StartsWith("/var/", StringComparison.OrdinalIgnoreCase) || string.Equals(tempPath, "/var", StringComparison.OrdinalIgnoreCase))
                {
                    tempPath = "/private" + tempPath;
                }
            }
            
            return tempPath;
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetAndGetEnvironmentVariable_ShouldWork(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testVarName = $"MSBUILD_TEST_VAR_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = $"test_value_{environmentType}";

            try
            {
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);
                var retrievedValue = taskEnvironment.GetEnvironmentVariable(testVarName);

                retrievedValue.ShouldBe(testVarValue);

                var allVariables = taskEnvironment.GetEnvironmentVariables();
                allVariables.TryGetValue(testVarName, out string? actualValue).ShouldBeTrue();
                actualValue.ShouldBe(testVarValue);
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetEnvironmentVariableToNull_ShouldRemoveVariable(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testVarName = $"MSBUILD_REMOVE_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "value_to_remove";

            try
            {
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);
                taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);

                taskEnvironment.SetEnvironmentVariable(testVarName, null);
                taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBeNull();

                var allVariables = taskEnvironment.GetEnvironmentVariables();
                allVariables.TryGetValue(testVarName, out string? actualValue).ShouldBeFalse();
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetEnvironment_ShouldReplaceAllVariables(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string prefix = $"MSBUILD_SET_ENV_TEST_{environmentType}_{Guid.NewGuid():N}";
            string var1Name = $"{prefix}_VAR1";
            string var2Name = $"{prefix}_VAR2";
            string var3Name = $"{prefix}_VAR3";

            var originalEnvironment = taskEnvironment.GetEnvironmentVariables().ToDictionary(k => k.Key, v => v.Value);

            try
            {
                taskEnvironment.SetEnvironmentVariable(var1Name, "initial_value1");
                taskEnvironment.SetEnvironmentVariable(var2Name, "initial_value2");

                var newEnvironment = new Dictionary<string, string>
                {
                    [var2Name] = "updated_value2", // Update existing
                    [var3Name] = "new_value3"      // Add new
                    // var1Name is intentionally omitted to test removal
                };

                taskEnvironment.SetEnvironment(newEnvironment);

                taskEnvironment.GetEnvironmentVariable(var1Name).ShouldBeNull(); // Should be removed
                taskEnvironment.GetEnvironmentVariable(var2Name).ShouldBe("updated_value2"); // Should be updated
                taskEnvironment.GetEnvironmentVariable(var3Name).ShouldBe("new_value3"); // Should be added
            }
            finally
            {
                taskEnvironment.SetEnvironment(originalEnvironment);
                DisposeTaskEnvironment(taskEnvironment);
                Environment.SetEnvironmentVariable(var1Name, null);
                Environment.SetEnvironmentVariable(var2Name, null);
                Environment.SetEnvironmentVariable(var3Name, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetAndGetProjectDirectory_ShouldWork(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string originalDirectory = Directory.GetCurrentDirectory();
            string testDirectory = GetResolvedTempPath().TrimEnd(Path.DirectorySeparatorChar);
            string alternateDirectory = Path.GetDirectoryName(testDirectory)!;

            try
            {
                // Set project directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(testDirectory, ignoreRootedCheck: true);
                var retrievedDirectory = taskEnvironment.ProjectDirectory;

                retrievedDirectory.Value.ShouldBe(testDirectory);

                // Change to alternate directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(alternateDirectory, ignoreRootedCheck: true);
                var newRetrievedDirectory = taskEnvironment.ProjectDirectory;

                newRetrievedDirectory.Value.ShouldBe(alternateDirectory);

                // Verify behavior differs based on environment type
                if (environmentType == StubEnvironmentName)
                {
                    // Stub should change system current directory
                    Directory.GetCurrentDirectory().ShouldBe(alternateDirectory);
                }
                else
                {
                    // Multithreaded should not change system current directory
                    Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
                }
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_ShouldResolveCorrectly(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string baseDirectory = GetResolvedTempPath();
            string relativePath = Path.Combine("subdir", "file.txt");
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                taskEnvironment.ProjectDirectory = new AbsolutePath(baseDirectory, ignoreRootedCheck: true);

                var absolutePath = taskEnvironment.GetAbsolutePath(relativePath);

                Path.IsPathRooted(absolutePath.Value).ShouldBeTrue();
                string expectedPath = Path.Combine(baseDirectory, relativePath);
                absolutePath.Value.ShouldBe(expectedPath);
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithAlreadyAbsolutePath_ShouldReturnUnchanged(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string absoluteInputPath = Path.Combine(GetResolvedTempPath(), "already", "absolute", "path.txt");

            try
            {
                var resultPath = taskEnvironment.GetAbsolutePath(absoluteInputPath);
                resultPath.Value.ShouldBe(absoluteInputPath);
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetProcessStartInfo_ShouldConfigureCorrectly(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testDirectory = GetResolvedTempPath();
            string testVarName = $"MSBUILD_PROCESS_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "process_test_value";
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                taskEnvironment.ProjectDirectory = new AbsolutePath(testDirectory, ignoreRootedCheck: true);
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                var processStartInfo = taskEnvironment.GetProcessStartInfo();

                processStartInfo.ShouldNotBeNull();

                if (environmentType == StubEnvironmentName)
                {
                    // Stub should reflect system environment, but working directory should be empty
                    processStartInfo.WorkingDirectory.ShouldBe(string.Empty);
                }
                else
                {
                    // Multithreaded should reflect isolated environment
                    processStartInfo.WorkingDirectory.ShouldBe(testDirectory);
                }

                processStartInfo.Environment.TryGetValue(testVarName, out string? actualValue).ShouldBeTrue();
                actualValue.ShouldBe(testVarValue);
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
                Environment.SetEnvironmentVariable(testVarName, null);
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Fact]
        public void TaskEnvironment_StubEnvironment_ShouldAffectSystemEnvironment()
        {
            string testVarName = $"MSBUILD_STUB_ISOLATION_TEST_{Guid.NewGuid():N}";
            string testVarValue = "stub_test_value";

            var stubEnvironment = TaskEnvironmentHelper.CreateForTest();

            try
            {
                // Set variable in stub environment
                stubEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                // Stub should affect system environment
                Environment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                stubEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);

                // Remove from stub environment
                stubEnvironment.SetEnvironmentVariable(testVarName, null);

                // System environment should also be affected
                Environment.GetEnvironmentVariable(testVarName).ShouldBeNull();
                stubEnvironment.GetEnvironmentVariable(testVarName).ShouldBeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Fact]
        public void TaskEnvironment_MultithreadedEnvironment_ShouldBeIsolatedFromSystem()
        {
            string testVarName = $"MSBUILD_MULTITHREADED_ISOLATION_TEST_{Guid.NewGuid():N}";
            string testVarValue = "multithreaded_test_value";

            using var driver = new MultiThreadedTaskEnvironmentDriver(
                GetResolvedTempPath(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            var multithreadedEnvironment = new TaskEnvironment(driver);

            try
            {
                // Verify system and environement doesn't have the test variable initially
                Environment.GetEnvironmentVariable(testVarName).ShouldBeNull();
                multithreadedEnvironment.GetEnvironmentVariable(testVarName).ShouldBeNull();

                // Set variable in multithreaded environment
                multithreadedEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                // Multithreaded should have the value but system should not
                multithreadedEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                Environment.GetEnvironmentVariable(testVarName).ShouldBeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithInvalidPathChars_ShouldNotThrow(string environmentType)
        {
            // Construct a path containing an invalid path character
            char invalidChar = Path.GetInvalidPathChars().FirstOrDefault();
            string invalidPath = "invalid" + invalidChar + "path";

            var taskEnvironment = CreateTaskEnvironment(environmentType);

            try
            {
                // Should not throw on invalid path characters
                var absolutePath = taskEnvironment.GetAbsolutePath(invalidPath);

                // The result should contain the invalid path combined with the base directory
                absolutePath.Value.ShouldNotBeNullOrEmpty();
                absolutePath.Value.ShouldContain(invalidPath);
            }
            finally
            {
                DisposeTaskEnvironment(taskEnvironment);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithEmptyPath_ReturnsProjectDirectory(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);

            // Empty path should absolutize to project directory (Path.Combine behavior)
            var absolutePath = taskEnvironment.GetAbsolutePath(string.Empty);

            absolutePath.Value.ShouldBe(taskEnvironment.ProjectDirectory.Value);
            absolutePath.OriginalValue.ShouldBe(string.Empty);
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithNullPath_WhenWave18_4Disabled_ReturnsNullPath(string environmentType)
        {
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            testEnv.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_4.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            var taskEnvironment = CreateTaskEnvironment(environmentType);

            // When Wave18_4 is disabled, null path returns as-is
            var absolutePath = taskEnvironment.GetAbsolutePath(null!);

            absolutePath.Value.ShouldBeNull();
            absolutePath.OriginalValue.ShouldBeNull();

            ChangeWaves.ResetStateForTests();
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithNullPath_WhenWave18_4Enabled_Throws(string environmentType)
        {
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            var taskEnvironment = CreateTaskEnvironment(environmentType);

            // When Wave18_4 is enabled, null path should throw
            Should.Throw<ArgumentNullException>(() => taskEnvironment.GetAbsolutePath(null!));

            ChangeWaves.ResetStateForTests();
        }
    }
}
