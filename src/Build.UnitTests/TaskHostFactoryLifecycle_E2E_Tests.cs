// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
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
        [Theory]
        [InlineData("CurrentRuntime", "TaskHostFactory")]      // Match + Explicit → short-lived out-of-proc
        [InlineData("CurrentRuntime", "AssemblyTaskFactory")]  // Match + No Explicit → in-proc

        // Not test-able on .NET msbuild as it can't run a CLR2/CLR4 task host (out-of-proc)
#if !NET
        [InlineData("NET", "TaskHostFactory")]     // No Match + Explicit → short-lived out-of-proc
        [InlineData("NET", "AssemblyTaskFactory")] // No Match + No Explicit → long-lived sidecar out-of-proc
#endif
        public void TaskHostLifecycle_ValidatesAllScenarios(
            string runtimeToUse,
            string taskFactoryToUse)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string testProjectPath = Path.Combine(TestAssetsRootPath, "TaskHostLifecycleTestApp.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n /p:RuntimeToUse={runtimeToUse} /p:TaskFactoryToUse={taskFactoryToUse}", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
        }
    }
}
