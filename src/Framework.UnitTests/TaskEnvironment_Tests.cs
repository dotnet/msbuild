// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.PathHelpers;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TaskEnvironmentTests
    {
        private const string StubEnvironmentName = "Stub";
        private const string MultithreadedEnvironmentName = "Multithreaded";

        public static TheoryData<string> EnvironmentTypes =>
            new TheoryData<string>
            {
                StubEnvironmentName,
                MultithreadedEnvironmentName
            };

        private static TaskEnvironment CreateTaskEnvironment(string environmentType)
        {
            return environmentType switch
            {
                StubEnvironmentName => new TaskEnvironment(StubTaskEnvironmentDriver.Instance),
                MultithreadedEnvironmentName => new TaskEnvironment(new MultithreadedTaskEnvironmentDriver(
                    Path.GetTempPath(),
                    new Dictionary<string, string>(Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                        .Where(e => e.Key is not null && e.Value is not null)
                        .ToDictionary(e => e.Key!.ToString()!, e => e.Value!.ToString()!)))),
                _ => throw new ArgumentException($"Unknown environment type: {environmentType}")
            };
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetAndGetEnvironmentVariable_ShouldWork(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testVarName = $"MSBUILD_TEST_VAR_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = $"test_value_{environmentType}";

            try
            {
                // Act
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);
                var retrievedValue = taskEnvironment.GetEnvironmentVariable(testVarName);

                // Assert
                retrievedValue.ShouldBe(testVarValue);

                // Verify it appears in GetEnvironmentVariables
                var allVariables = taskEnvironment.GetEnvironmentVariables();
                allVariables.TryGetValue(testVarName, out string? actualValue).ShouldBeTrue();
                actualValue.ShouldBe(testVarValue);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetEnvironmentVariableToNull_ShouldRemoveVariable(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testVarName = $"MSBUILD_REMOVE_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "value_to_remove";

            try
            {
                // Setup - first set the variable
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);
                taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);

                // Act - remove the variable
                taskEnvironment.SetEnvironmentVariable(testVarName, null);

                // Assert
                taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBeNull();
                var allVariables = taskEnvironment.GetEnvironmentVariables();
                allVariables.TryGetValue(testVarName, out string? actualValue).ShouldBeFalse();
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetEnvironment_ShouldReplaceAllVariables(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string prefix = $"MSBUILD_SET_ENV_TEST_{environmentType}_{Guid.NewGuid():N}";
            string var1Name = $"{prefix}_VAR1";
            string var2Name = $"{prefix}_VAR2";
            string var3Name = $"{prefix}_VAR3";

            try
            {
                // Setup initial state
                taskEnvironment.SetEnvironmentVariable(var1Name, "initial_value1");
                taskEnvironment.SetEnvironmentVariable(var2Name, "initial_value2");

                var newEnvironment = new Dictionary<string, string>
                {
                    [var2Name] = "updated_value2", // Update existing
                    [var3Name] = "new_value3"      // Add new
                    // var1Name is intentionally omitted to test removal
                };

                // Act
                taskEnvironment.SetEnvironment(newEnvironment);

                // Assert
                taskEnvironment.GetEnvironmentVariable(var1Name).ShouldBeNull(); // Should be removed
                taskEnvironment.GetEnvironmentVariable(var2Name).ShouldBe("updated_value2"); // Should be updated
                taskEnvironment.GetEnvironmentVariable(var3Name).ShouldBe("new_value3"); // Should be added
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(var1Name, null);
                Environment.SetEnvironmentVariable(var2Name, null);
                Environment.SetEnvironmentVariable(var3Name, null);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_SetAndGetProjectDirectory_ShouldWork(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string originalDirectory = Directory.GetCurrentDirectory();
            string testDirectory = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            string alternateDirectory = Path.GetDirectoryName(testDirectory)!;

            try
            {
                // Act - Set project directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(testDirectory, ignoreRootedCheck: true);
                var retrievedDirectory = taskEnvironment.ProjectDirectory;

                // Assert
                retrievedDirectory.Path.ShouldBe(testDirectory);

                // Act - Change to alternate directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(alternateDirectory, ignoreRootedCheck: true);
                var newRetrievedDirectory = taskEnvironment.ProjectDirectory;

                // Assert
                newRetrievedDirectory.Path.ShouldBe(alternateDirectory);

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
                // Restore original directory
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_ShouldResolveCorrectly(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string baseDirectory = Path.GetTempPath();
            string relativePath = Path.Combine("subdir", "file.txt");
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                // Set project directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(baseDirectory, ignoreRootedCheck: true);

                // Act
                var absolutePath = taskEnvironment.GetAbsolutePath(relativePath);

                // Assert
                Path.IsPathRooted(absolutePath.Path).ShouldBeTrue();
                string expectedPath = Path.Combine(baseDirectory, relativePath);
                absolutePath.Path.ShouldBe(expectedPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_WithAlreadyAbsolutePath_ShouldReturnUnchanged(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string absoluteInputPath = Path.Combine(Path.GetTempPath(), "already", "absolute", "path.txt");

            // Act
            var resultPath = taskEnvironment.GetAbsolutePath(absoluteInputPath);

            // Assert
            resultPath.Path.ShouldBe(absoluteInputPath);
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetProcessStartInfo_ShouldConfigureCorrectly(string environmentType)
        {
            // Arrange
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testDirectory = Path.GetTempPath();
            string testVarName = $"MSBUILD_PROCESS_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "process_test_value";

            try
            {
                // Setup
                taskEnvironment.ProjectDirectory = new AbsolutePath(testDirectory, ignoreRootedCheck: true);
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                // Act
                var processStartInfo = taskEnvironment.GetProcessStartInfo();

                // Assert
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
                // Cleanup
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Fact]
        public void TaskEnvironment_StubEnvironment_ShouldAffectSystemEnvironment()
        {
            // Arrange
            string testVarName = $"MSBUILD_STUB_ISOLATION_TEST_{Guid.NewGuid():N}";
            string testVarValue = "stub_test_value";

            var stubEnvironment = new TaskEnvironment(StubTaskEnvironmentDriver.Instance);

            try
            {
                // Act - Set variable in stub environment
                stubEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                // Assert - Stub should affect system environment
                Environment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                stubEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);

                // Act - Remove from stub environment
                stubEnvironment.SetEnvironmentVariable(testVarName, null);

                // Assert - System environment should also be affected
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
            // Arrange
            string testVarName = $"MSBUILD_MULTITHREADED_ISOLATION_TEST_{Guid.NewGuid():N}";
            string testVarValue = "multithreaded_test_value";

            var multithreadedEnvironment = new TaskEnvironment(new MultithreadedTaskEnvironmentDriver(
                Path.GetTempPath(),
                new Dictionary<string, string>()));

            try
            {
                // Verify system doesn't have the test variable initially
                Environment.GetEnvironmentVariable(testVarName).ShouldBeNull();

                // Act - Set variable in multithreaded environment
                multithreadedEnvironment.SetEnvironmentVariable(testVarName, testVarValue);

                // Assert - Multithreaded should have the value but system should not
                multithreadedEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                Environment.GetEnvironmentVariable(testVarName).ShouldBeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }
    }
}
