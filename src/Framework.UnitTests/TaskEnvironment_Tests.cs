// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
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
                MultithreadedEnvironmentName => new TaskEnvironment(new MultithreadedTaskEnvironmentDriver(Path.GetTempPath())),
                _ => throw new ArgumentException($"Unknown environment type: {environmentType}")
            };
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
            string testDirectory = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            string alternateDirectory = Path.GetDirectoryName(testDirectory)!;

            try
            {
                // Set project directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(testDirectory, ignoreRootedCheck: true);
                var retrievedDirectory = taskEnvironment.ProjectDirectory;

                retrievedDirectory.Path.ShouldBe(testDirectory);

                // Change to alternate directory
                taskEnvironment.ProjectDirectory = new AbsolutePath(alternateDirectory, ignoreRootedCheck: true);
                var newRetrievedDirectory = taskEnvironment.ProjectDirectory;

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
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetAbsolutePath_ShouldResolveCorrectly(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string baseDirectory = Path.GetTempPath();
            string relativePath = Path.Combine("subdir", "file.txt");
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                taskEnvironment.ProjectDirectory = new AbsolutePath(baseDirectory, ignoreRootedCheck: true);

                var absolutePath = taskEnvironment.GetAbsolutePath(relativePath);

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
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string absoluteInputPath = Path.Combine(Path.GetTempPath(), "already", "absolute", "path.txt");

            var resultPath = taskEnvironment.GetAbsolutePath(absoluteInputPath);

            resultPath.Path.ShouldBe(absoluteInputPath);
        }

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_GetProcessStartInfo_ShouldConfigureCorrectly(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testDirectory = Path.GetTempPath();
            string testVarName = $"MSBUILD_PROCESS_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "process_test_value";

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
                Environment.SetEnvironmentVariable(testVarName, null);
            }
        }

        [Fact]
        public void TaskEnvironment_StubEnvironment_ShouldAffectSystemEnvironment()
        {
            string testVarName = $"MSBUILD_STUB_ISOLATION_TEST_{Guid.NewGuid():N}";
            string testVarValue = "stub_test_value";

            var stubEnvironment = new TaskEnvironment(StubTaskEnvironmentDriver.Instance);

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

        [Theory]
        [MemberData(nameof(EnvironmentTypes))]
        public void TaskEnvironment_EnvironmentVariableCaseSensitivity_ShouldMatchPlatform(string environmentType)
        {
            var taskEnvironment = CreateTaskEnvironment(environmentType);
            string testVarName = $"MSBUILD_case_TEST_{environmentType}_{Guid.NewGuid():N}";
            string testVarValue = "case_test_value";

            try
            {
                taskEnvironment.SetEnvironmentVariable(testVarName, testVarValue);
                
                // Test GetEnvironmentVariables()
                var envVars = taskEnvironment.GetEnvironmentVariables();
                
                // Test GetEnvironmentVariable()
                string upperVarName = testVarName.ToUpperInvariant();
                string lowerVarName = testVarName.ToLowerInvariant();
                
                if (NativeMethods.IsWindows)
                {
                    // On Windows, environment variables should be case-insensitive
                    
                    // Test GetEnvironmentVariables()
                    envVars.TryGetValue(testVarName, out string? exactValue).ShouldBeTrue();
                    exactValue.ShouldBe(testVarValue);
                    
                    envVars.TryGetValue(upperVarName, out string? upperValue).ShouldBeTrue();
                    upperValue.ShouldBe(testVarValue);
                    
                    envVars.TryGetValue(lowerVarName, out string? lowerValue).ShouldBeTrue();
                    lowerValue.ShouldBe(testVarValue);
                    
                    // Test GetEnvironmentVariable()
                    taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                    taskEnvironment.GetEnvironmentVariable(upperVarName).ShouldBe(testVarValue);
                    taskEnvironment.GetEnvironmentVariable(lowerVarName).ShouldBe(testVarValue);
                }
                else
                {
                    // On Unix-like systems, environment variables should be case-sensitive
                    
                    // Test GetEnvironmentVariables()
                    envVars.TryGetValue(testVarName, out string? exactValue).ShouldBeTrue();
                    exactValue.ShouldBe(testVarValue);
                    
                    // Different case should not match on Unix-like systems
                    envVars.TryGetValue(upperVarName, out string? upperValue).ShouldBe(upperVarName == testVarName);
                    envVars.TryGetValue(lowerVarName, out string? lowerValue).ShouldBe(lowerVarName == testVarName);
                    
                    // Test GetEnvironmentVariable()
                    taskEnvironment.GetEnvironmentVariable(testVarName).ShouldBe(testVarValue);
                    
                    // Different case should only work if they're actually the same string
                    var expectedUpperValue = upperVarName == testVarName ? testVarValue : null;
                    var expectedLowerValue = lowerVarName == testVarName ? testVarValue : null;
                    
                    taskEnvironment.GetEnvironmentVariable(upperVarName).ShouldBe(expectedUpperValue);
                    taskEnvironment.GetEnvironmentVariable(lowerVarName).ShouldBe(expectedLowerValue);
                }
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

            // On Windows, environment variables are case-insensitive; on Unix-like systems, they are case-sensitive
            var comparer = NativeMethods.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var multithreadedEnvironment = new TaskEnvironment(new MultithreadedTaskEnvironmentDriver(
                Path.GetTempPath(),
                new Dictionary<string, string>(comparer)));

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
    }
}
