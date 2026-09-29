// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

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

        /// <summary>
        /// A sidecar waiting in the pool must outlive the scenario's quiescence wait, and then end promptly on its own.
        /// </summary>
        private const int PooledTaskHostIdleTimeoutMs = 20_000;

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
        [NodeScenarioTheory]
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

            using NodeScenario scenario = NodeScenario.Create(_output);

            if (expectedNodeReuse == true)
            {
                // A long-lived sidecar of the bootstrap cannot be reached from here, so it ends on its idle timeout.
                // A short-lived TaskHost gets no such help: it has to exit on its own.
                scenario.UseShortNodeIdleTimeout(PooledTaskHostIdleTimeoutMs);
            }

            string buildOutput = ExecuteBuildWithTaskHost(scenario, runtimeToUse, taskFactoryToUse);

            ValidateTaskHostBehavior(buildOutput, expectedNodeReuse);
            ValidateTaskHostLifetime(scenario, expectedNodeReuse);
        }

        private static void ValidateTaskHostLifetime(NodeScenario scenario, bool? expectedNodeReuse)
        {
            if (expectedNodeReuse is null)
            {
                scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.TaskHost), "a TaskHost launched for an in-proc task");
                return;
            }

            int taskHost = scenario.Await(NodeJournalEvent.Launched, NodeJournalKind.TaskHost).SubjectProcessId;
            scenario.Count(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.TaskHost)).ShouldBe(1);
            if (expectedNodeReuse.Value)
            {
                // The long-lived sidecar stays for the next build after this one has completed. (A .NET TaskHost of the
                // resolved SDK, rather than of the bootstrap, does not journal, so this can only catch a bootstrap one.)
                scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.Exited, processId: taskHost), "the long-lived sidecar exiting after the build");
            }

            // Nothing asks the TaskHost to shut down, and only a long-lived one has a short idle timeout: a short-lived
            // TaskHost that stayed would reach the scenario's hang ceiling here.
            scenario.ShutdownNodes(static () => { });
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

        private static string ExecuteBuildWithTaskHost(NodeScenario scenario, string runtimeToUse, string taskFactoryToUse)
        {
            string testProjectPath = Path.Combine(TestAssetsRootPath, "TaskHostLifecycleTestApp.csproj");

            (bool success, string output) = scenario.RunBootstrapped(
                $"{testProjectPath} -v:n -restore /p:RuntimeToUse={runtimeToUse} /p:TaskFactoryToUse={taskFactoryToUse} /p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}");

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
