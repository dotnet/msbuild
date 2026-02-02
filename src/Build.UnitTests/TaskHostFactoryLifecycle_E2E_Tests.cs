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
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(TaskHostFactoryLifecycle_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets", "TaskHostLifecycle");

        private const string TaskHostFactory = "TaskHostFactory";
        private const string AssemblyTaskFactory = "AssemblyTaskFactory";
        private const string CurrentRuntime = "CurrentRuntime";
        private const string NetRuntime = "NET";

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
        [InlineData(CurrentRuntime, AssemblyTaskFactory)] // Match + No Explicit → in-proc
        [InlineData(CurrentRuntime, TaskHostFactory)] // Match + Explicit → short-lived out-of-proc
#endif
        [InlineData(NetRuntime, AssemblyTaskFactory)] // No Match + No Explicit → long-lived sidecar out-of-proc
        [InlineData(NetRuntime, TaskHostFactory)] // No Match + Explicit → short-lived out-of-proc
        public void TaskHostLifecycle_ValidatesAllScenarios(
            string runtimeToUse,
            string taskFactoryToUse)
        {
            bool? expectedNodeReuse = DetermineExpectedNodeReuse(runtimeToUse, taskFactoryToUse);

            using TestEnvironment env = TestEnvironment.Create(_output);

            string buildOutput = ExecuteBuildWithTaskHost(runtimeToUse, taskFactoryToUse);

            ValidateTaskHostBehavior(buildOutput, expectedNodeReuse);
        }

        private static bool? DetermineExpectedNodeReuse(string runtimeToUse, string taskFactoryToUse)
            => (taskFactoryToUse, runtimeToUse) switch
            {
                // TaskHostFactory is always short-lived and out-of-proc (nodereuse:False)
                (TaskHostFactory, _) => false,

                // AssemblyTaskFactory with CurrentRuntime runs in-proc
                (AssemblyTaskFactory, CurrentRuntime) => null,

                // AssemblyTaskFactory with NET runtime:
                // - On .NET Framework host: out-of-proc with long-lived sidecar (nodereuse:True)
                // - On .NET host: in-proc
                (AssemblyTaskFactory, NetRuntime) =>
#if NET
    null,  // On .NET host: in-proc execution
#else
    true,  // On .NET Framework host: out-of-proc with long-lived sidecar
#endif
                _ => throw new ArgumentException($"Unknown combination: runtime={runtimeToUse}, factory={taskFactoryToUse}")
            };

        private string ExecuteBuildWithTaskHost(string runtimeToUse, string taskFactoryToUse)
        {
            string testProjectPath = Path.Combine(TestAssetsRootPath, "TaskHostLifecycleTestApp.csproj");

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{testProjectPath} -v:n -restore /p:RuntimeToUse={runtimeToUse} /p:TaskFactoryToUse={taskFactoryToUse} /p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}",
                out bool success,
                outputHelper: _output);

            success.ShouldBeTrue("Build should succeed");

            return output;
        }

        private static void ValidateTaskHostBehavior(string buildOutput, bool? expectedNodeReuse)
        {
            if (expectedNodeReuse.HasValue)
            {
                buildOutput.ShouldContain("/nodemode:", customMessage: "Task should run out-of-proc and have /nodemode: in its command-line arguments");

                string expectedFlag = expectedNodeReuse.Value ? "/nodereuse:True" : "/nodereuse:False";
                buildOutput.ShouldContain(expectedFlag, customMessage: $"Task should have {expectedFlag} in its command-line arguments");
            }
            else
            {
                buildOutput.ShouldNotContain("/nodemode:", customMessage: "Task should run in-proc and not have task host command-line arguments like /nodemode:");
            }
        }
    }
}
