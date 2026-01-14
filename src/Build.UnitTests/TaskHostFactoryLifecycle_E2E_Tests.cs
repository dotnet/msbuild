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
        /// 2. Runtime matches + TaskHostFactory NOT requested → in-proc execution (MSBuild process)
        /// 3. Runtime doesn't match + TaskHostFactory requested → short-lived out of proc (nodereuse:False)
        /// 4. Runtime doesn't match + TaskHostFactory NOT requested → long-lived sidecar out of proc (nodereuse:True)
        /// </summary>
        /// <param name="scenarioNumber">Scenario number (1-4)</param>
        /// <param name="scenarioFolder">Test project folder name</param>
        /// <param name="projectFile">Test project file name</param>
        /// <param name="expectedProcessName">Expected process name in output (dotnet or MSBuild)</param>
        /// <param name="expectedNodeReuse">Expected node reuse flag (True, False, or null for in-proc)</param>
        [WindowsFullFrameworkOnlyTheory]
        [InlineData(1, "Scenario1_MatchingRuntime_ExplicitFactory", "Scenario1.csproj", "dotnet", false)]        // Match + Explicit → short-lived out-of-proc
        [InlineData(2, "Scenario2_MatchingRuntime_NoFactory", "Scenario2.csproj", "MSBuild", null)]              // Match + No Explicit → in-proc
        [InlineData(3, "Scenario3_NonMatchingRuntime_ExplicitFactory", "Scenario3.csproj", "dotnet", false)]     // No Match + Explicit → short-lived out-of-proc
        [InlineData(4, "Scenario4_NonMatchingRuntime_NoFactory", "Scenario4.csproj", "dotnet", true)]            // No Match + No Explicit → long-lived sidecar out-of-proc
        public void TaskHostLifecycle_ValidatesAllScenarios(
            int scenarioNumber,
            string scenarioFolder,
            string projectFile,
            string expectedProcessName,
            bool? expectedNodeReuse)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, scenarioFolder, projectFile);

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            
            // Verify expected process name
            testTaskOutput.ShouldContain($"The task is executed in process: {expectedProcessName}", 
                customMessage: $"Scenario {scenarioNumber}: Task should run in {expectedProcessName} process");
            
            // Verify node reuse behavior if out-of-proc
            if (expectedNodeReuse.HasValue)
            {
                string expectedFlag = expectedNodeReuse.Value ? "/nodereuse:True" : "/nodereuse:False";
                string nodeReuseDescription = expectedNodeReuse.Value ? "long-lived sidecar (node reuse enabled)" : "short-lived (no node reuse)";
                testTaskOutput.ShouldContain(expectedFlag, 
                    customMessage: $"Scenario {scenarioNumber}: Task host should use {nodeReuseDescription}");
            }
        }
    }
}
