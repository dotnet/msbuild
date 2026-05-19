// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// End-to-end tests for task host factory lifecycle behavior.
    /// 
    /// Tests validate the behavior based on whether the TaskHost runtime matches 
    /// the executing MSBuild runtime and whether TaskHostFactory is explicitly requested.
    /// 
    /// This is a regression test for https://github.com/dotnet/msbuild/issues/13013
    /// </summary>
    public class TaskHostFactoryLifecycle_E2E_Tests
    {
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(TaskHostFactoryLifecycle_E2E_Tests).Assembly.Location) ?? System.AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets", "TaskHostLifecycle");

        private readonly ITestOutputHelper _output;

        public TaskHostFactoryLifecycle_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Validates task host lifecycle behavior for all scenarios.
        /// 
        /// Test scenarios:
        /// 1. Runtime matches + TaskHostFactory requested → short-lived out of proc (nodereuse:False)
        /// 2. Runtime matches + TaskHostFactory NOT requested → in-proc execution
        /// 3. Runtime doesn't match + TaskHostFactory requested → short-lived out of proc (nodereuse:False)
        /// 4. Runtime doesn't match + TaskHostFactory NOT requested → long-lived sidecar out of proc (nodereuse:True)
        /// </summary>
        /// <param name="runtimeToUse">The runtime to use for the task (CurrentRuntime or NET)</param>
        /// <param name="taskFactoryToUse">The task factory to use (TaskHostFactory or AssemblyTaskFactory)</param>
        [Theory]
#if NET
        [InlineData("CurrentRuntime", "AssemblyTaskFactory")] // Match + No Explicit → in-proc
        [InlineData("CurrentRuntime", "TaskHostFactory")]     // Match + Explicit → short-lived out-of-proc
#endif
        [InlineData("NET", "AssemblyTaskFactory")]            // No Match + No Explicit → long-lived sidecar out-of-proc
        [InlineData("NET", "TaskHostFactory")]                // No Match + Explicit → short-lived out-of-proc
        public void TaskHostLifecycle_ValidatesAllScenarios(
            string runtimeToUse,
            string taskFactoryToUse)
        {
            bool? expectedNodeReuse;

            // TaskHostFactory is always short lived and out-of-proc
            if (taskFactoryToUse == "TaskHostFactory")
            {
                expectedNodeReuse = false;
            }
            // AssemblyTaskFactory behavior depends on runtime
            else if (taskFactoryToUse == "AssemblyTaskFactory")
            {
                if (runtimeToUse == "CurrentRuntime")
                {
                    // in-proc
                    expectedNodeReuse = null;
                }
                else if (runtimeToUse == "NET")
                {
                    // When running on .NET Framework: out-of-proc, otherwise on .NET in-proc.
                    expectedNodeReuse = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase) ? true : null;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(runtimeToUse), "Unknown runtime to use: " + runtimeToUse);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(taskFactoryToUse), "Unknown task factory to use: " + taskFactoryToUse);
            }

            using TestEnvironment env = TestEnvironment.Create(_output);
            string testProjectPath = Path.Combine(TestAssetsRootPath, "TaskHostLifecycleTestApp.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{testProjectPath} -v:n -restore /p:RuntimeToUse={runtimeToUse} /p:TaskFactoryToUse={taskFactoryToUse}", 
                out bool successTestTask,
                outputHelper: _output);

            successTestTask.ShouldBeTrue();

            // Verify execution mode (out-of-proc vs in-proc) and node reuse behavior
            if (expectedNodeReuse.HasValue)
            {
                // For out-of-proc scenarios, validate the task runs in a separate process
                // by checking for the presence of command-line arguments that indicate task host execution
                testTaskOutput.ShouldContain("/nodemode:", 
                    customMessage: "Task should run out-of-proc and have /nodemode: in its command-line arguments");

                // Validate the nodereuse flag in the task's command-line arguments
                string expectedFlag = expectedNodeReuse.Value ? "/nodereuse:True" : "/nodereuse:False";
                testTaskOutput.ShouldContain(expectedFlag, 
                    customMessage: $"Task should have {expectedFlag} in its command-line arguments");
            }
            else
            {
                // For in-proc scenarios, validate the task does NOT run in a task host
                // by ensuring task host specific command-line flags are not present
                testTaskOutput.ShouldNotContain("/nodemode:", 
                    customMessage: "Task should run in-proc and not have task host command-line arguments like /nodemode:");
            }
        }
    }
}
