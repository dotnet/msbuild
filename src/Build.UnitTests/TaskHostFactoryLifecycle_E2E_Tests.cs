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
        /// Scenario 1: Runtime matches + TaskHostFactory requested → short-lived out of proc (no node reuse)
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void Scenario1_MatchingRuntime_ExplicitFactory_ShortLivedOutOfProc()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "Scenario1_MatchingRuntime_ExplicitFactory", "Scenario1.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            
            // Task should run out of process (dotnet.exe for NET runtime on Windows Full Framework MSBuild)
            testTaskOutput.ShouldContain("The task is executed in process: dotnet", customMessage: "Task should run out of process");
            
            // With explicit TaskHostFactory, node reuse should be False (short-lived)
            testTaskOutput.ShouldContain("/nodereuse:False", customMessage: "TaskHostFactory explicitly requested should use short-lived task host (no node reuse)");
        }

        /// <summary>
        /// Scenario 2: Runtime matches + TaskHostFactory NOT requested → in-proc execution
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void Scenario2_MatchingRuntime_NoFactory_InProc()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "Scenario2_MatchingRuntime_NoFactory", "Scenario2.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            
            // Task should run in-process (MSBuild.exe on Windows Full Framework)
            testTaskOutput.ShouldContain("The task is executed in process: MSBuild", customMessage: "Task should run in-process when runtime matches and no TaskHostFactory is requested");
        }

        /// <summary>
        /// Scenario 3: Runtime doesn't match + TaskHostFactory requested → short-lived out of proc (no node reuse)
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void Scenario3_NonMatchingRuntime_ExplicitFactory_ShortLivedOutOfProc()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "Scenario3_NonMatchingRuntime_ExplicitFactory", "Scenario3.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            
            // Task should run out of process (dotnet.exe for NET runtime)
            testTaskOutput.ShouldContain("The task is executed in process: dotnet", customMessage: "Task should run out of process when runtime doesn't match");
            
            // With explicit TaskHostFactory, node reuse should be False (short-lived)
            testTaskOutput.ShouldContain("/nodereuse:False", customMessage: "TaskHostFactory explicitly requested should use short-lived task host (no node reuse)");
        }

        /// <summary>
        /// Scenario 4: Runtime doesn't match + TaskHostFactory NOT requested → long-lived sidecar out of proc (node reuse enabled)
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void Scenario4_NonMatchingRuntime_NoFactory_LongLivedSidecarOutOfProc()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "Scenario4_NonMatchingRuntime_NoFactory", "Scenario4.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            
            // Task should run out of process (dotnet.exe for NET runtime)
            testTaskOutput.ShouldContain("The task is executed in process: dotnet", customMessage: "Task should run out of process when runtime doesn't match");
            
            // Without explicit TaskHostFactory, node reuse should be True (long-lived sidecar)
            testTaskOutput.ShouldContain("/nodereuse:True", customMessage: "When TaskHostFactory is NOT explicitly requested and runtime doesn't match, should use long-lived sidecar task host (node reuse enabled)");
        }
    }
}
