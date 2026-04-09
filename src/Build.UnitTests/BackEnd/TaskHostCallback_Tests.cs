// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Integration tests for IBuildEngine callback support in TaskHost.
    /// These tests use BuildManager to run real builds with TaskHostFactory.
    /// For packet serialization tests, see <see cref="TaskHostCallbackPacket_Tests"/>.
    /// </summary>
    public class TaskHostCallback_Tests
    {
        private readonly ITestOutputHelper _output;

        public TaskHostCallback_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Verifies IsRunningMultipleNodes callback works when task is explicitly run in TaskHost via TaskHostFactory.
        /// IsRunningMultipleNodes is configuration-based (MaxNodeCount > 1), not based on actual running nodes.
        /// See TaskHost.IsRunningMultipleNodes: returns _host.BuildParameters.MaxNodeCount > 1 || _disableInprocNode.
        /// </summary>
        [Theory]
        [InlineData(1, false)]  // MaxNodeCount=1 → IsRunningMultipleNodes=false
        [InlineData(4, true)]   // MaxNodeCount=4 → IsRunningMultipleNodes=true (even with one project)
        public void IsRunningMultipleNodes_WorksWithExplicitTaskHostFactory(int maxNodeCount, bool expectedResult)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(IsRunningMultipleNodesTask)}"" AssemblyFile=""{typeof(IsRunningMultipleNodesTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(IsRunningMultipleNodesTask)}>
            <Output PropertyName=""Result"" TaskParameter=""IsRunningMultipleNodes"" />
        </{nameof(IsRunningMultipleNodesTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = maxNodeCount, EnableNodeReuse = false },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            bool.Parse(projectInstance.GetPropertyValue("Result")).ShouldBe(expectedResult);
        }

        /// <summary>
        /// Verifies IsRunningMultipleNodes callback works when unmarked task is auto-ejected to TaskHost in MT mode.
        /// </summary>
        [Theory]
        [InlineData(1, false)]
        [InlineData(4, true)]
        public void IsRunningMultipleNodes_WorksWhenAutoEjectedInMultiThreadedMode(int maxNodeCount, bool expectedResult)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");
            string testDir = env.CreateFolder().Path;

            // IsRunningMultipleNodesTask lacks MSBuildMultiThreadableTask attribute, so it's auto-ejected to TaskHost in MT mode
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(IsRunningMultipleNodesTask)}"" AssemblyFile=""{typeof(IsRunningMultipleNodesTask).Assembly.Location}"" />
    <Target Name=""Test"">
        <{nameof(IsRunningMultipleNodesTask)}>
            <Output PropertyName=""Result"" TaskParameter=""IsRunningMultipleNodes"" />
        </{nameof(IsRunningMultipleNodesTask)}>
    </Target>
</Project>";

            string projectFile = Path.Combine(testDir, "Test.proj");
            File.WriteAllText(projectFile, projectContents);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters
                {
                    MultiThreaded = true,
                    MaxNodeCount = maxNodeCount,
                    Loggers = [logger],
                    EnableNodeReuse = false
                },
                new BuildRequestData(projectFile, new Dictionary<string, string?>(), null, ["Test"], null));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was ejected to TaskHost
            logger.FullLog.ShouldContain("external task host");

            // Verify callback returned correct value
            logger.FullLog.ShouldContain($"IsRunningMultipleNodes = {expectedResult}");
        }

        /// <summary>
        /// Verifies that accessing IsRunningMultipleNodes when callbacks are disabled
        /// logs error MSB5022 (BuildEngineCallbacksInTaskHostUnsupported).
        /// This preserves the pre-callback behavior where unsupported IBuildEngine
        /// methods in TaskHost log an error.
        /// </summary>
        [Fact]
        public void IsRunningMultipleNodes_LogsErrorWhenCallbacksNotSupported()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Explicitly do NOT set MSBUILDENABLETASKHOSTCALLBACKS — callbacks should be disabled
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(IsRunningMultipleNodesTask)}"" AssemblyFile=""{typeof(IsRunningMultipleNodesTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(IsRunningMultipleNodesTask)}>
            <Output PropertyName=""Result"" TaskParameter=""IsRunningMultipleNodes"" />
        </{nameof(IsRunningMultipleNodesTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            // MSB5022 error should be logged — the callback was not forwarded
            logger.ErrorCount.ShouldBeGreaterThan(0);
            logger.FullLog.ShouldContain("MSB5022");
        }

        /// <summary>
        /// Verifies RequestCores callback works when task is explicitly run in TaskHost via TaskHostFactory.
        /// The first RequestCores call should always return at least 1 (the implicit core).
        /// </summary>
        [Fact]
        public void RequestCores_WorksWithExplicitTaskHostFactory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(RequestCoresTask)}"" AssemblyFile=""{typeof(RequestCoresTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(RequestCoresTask)} CoreCount=""2"">
            <Output PropertyName=""Result"" TaskParameter=""GrantedCores"" />
        </{nameof(RequestCoresTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            // First RequestCores call gets at least the implicit core
            int.Parse(projectInstance.GetPropertyValue("Result")).ShouldBeGreaterThanOrEqualTo(1);
        }

        /// <summary>
        /// Verifies RequestCores + ReleaseCores works end-to-end when task runs in TaskHost.
        /// </summary>
        [Fact]
        public void RequestAndReleaseCores_WorksWithExplicitTaskHostFactory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(RequestCoresTask)}"" AssemblyFile=""{typeof(RequestCoresTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(RequestCoresTask)} CoreCount=""2"" ReleaseAfter=""true"">
            <Output PropertyName=""Result"" TaskParameter=""GrantedCores"" />
        </{nameof(RequestCoresTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            // Verify both RequestCores and ReleaseCores ran without error
            logger.AssertNoErrors();
            logger.FullLog.ShouldContain("ReleaseCores(");
        }

        /// <summary>
        /// Verifies RequestCores callback works when task is auto-ejected to TaskHost in multithreaded mode.
        /// </summary>
        [Fact]
        public void RequestCores_WorksWhenAutoEjectedInMultiThreadedMode()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");
            string testDir = env.CreateFolder().Path;

            // RequestCoresTask lacks MSBuildMultiThreadableTask attribute, so it's auto-ejected to TaskHost in MT mode
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(RequestCoresTask)}"" AssemblyFile=""{typeof(RequestCoresTask).Assembly.Location}"" />
    <Target Name=""Test"">
        <{nameof(RequestCoresTask)} CoreCount=""1"">
            <Output PropertyName=""Result"" TaskParameter=""GrantedCores"" />
        </{nameof(RequestCoresTask)}>
    </Target>
</Project>";

            string projectFile = Path.Combine(testDir, "Test.proj");
            File.WriteAllText(projectFile, projectContents);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters
                {
                    MultiThreaded = true,
                    MaxNodeCount = 4,
                    Loggers = [logger],
                    EnableNodeReuse = false
                },
                new BuildRequestData(projectFile, new Dictionary<string, string?>(), null, ["Test"], null));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.FullLog.ShouldContain("external task host");
            logger.FullLog.ShouldContain("RequestCores(1) =");
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13153
        /// Verifies that request-level global properties (passed via BuildRequestData, not just
        /// BuildParameters.GlobalProperties) are forwarded through TaskHostTask to the out-of-proc
        /// TaskHost when a task is auto-ejected in multithreaded mode.
        ///
        /// Before the fix, TaskHostTask.Execute() used BuildParameters.GlobalProperties (build-level),
        /// which did not include per-request properties like MSBuildRestoreSessionId. This caused
        /// NuGet static graph restore to fail for conditional ProjectReference items.
        /// </summary>
        [Fact]
        public void GlobalProperties_ForwardedToAutoEjectedTaskInMultiThreadedMode()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string testDir = env.CreateFolder().Path;

            // GetGlobalPropertiesTask lacks MSBuildMultiThreadableTask attribute,
            // so it's auto-ejected to TaskHost in MT mode
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(GetGlobalPropertiesTask)}"" AssemblyFile=""{typeof(GetGlobalPropertiesTask).Assembly.Location}"" />
    <Target Name=""Test"">
        <{nameof(GetGlobalPropertiesTask)}>
            <Output PropertyName=""PropCount"" TaskParameter=""GlobalPropertyCount"" />
        </{nameof(GetGlobalPropertiesTask)}>
    </Target>
</Project>";

            string projectFile = Path.Combine(testDir, "Test.proj");
            File.WriteAllText(projectFile, projectContents);

            // Pass request-level global properties via BuildRequestData (simulates what
            // ExecuteRestore() does when adding MSBuildRestoreSessionId)
            var requestGlobalProperties = new Dictionary<string, string?>
            {
                ["TestRequestProperty"] = "RequestValue",
                ["AnotherRequestProp"] = "AnotherValue",
            };

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters
                {
                    MultiThreaded = true,
                    MaxNodeCount = 4,
                    Loggers = [logger],
                    EnableNodeReuse = false,
                },
                new BuildRequestData(projectFile, requestGlobalProperties, null, ["Test"], null));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was ejected to TaskHost
            logger.FullLog.ShouldContain("external task host");

            // Verify request-level global properties were forwarded to the TaskHost
            logger.FullLog.ShouldContain("GlobalProperty: TestRequestProperty=RequestValue");
            logger.FullLog.ShouldContain("GlobalProperty: AnotherRequestProp=AnotherValue");
        }

        /// <summary>
        /// Verifies that when ChangeWave 18.6 is disabled, the old behavior is preserved:
        /// TaskHostTask sends build-level properties (BuildParameters.GlobalProperties) instead
        /// of request-level properties. This is the opt-out for the fix in #13153.
        /// </summary>
        [Fact]
        public void GlobalProperties_UseBuildLevelWhenChangeWaveDisabled()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
            string testDir = env.CreateFolder().Path;

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(GetGlobalPropertiesTask)}"" AssemblyFile=""{typeof(GetGlobalPropertiesTask).Assembly.Location}"" />
    <Target Name=""Test"">
        <{nameof(GetGlobalPropertiesTask)}>
            <Output PropertyName=""PropCount"" TaskParameter=""GlobalPropertyCount"" />
        </{nameof(GetGlobalPropertiesTask)}>
    </Target>
</Project>";

            string projectFile = Path.Combine(testDir, "Test.proj");
            File.WriteAllText(projectFile, projectContents);

            // These request-level properties should NOT be forwarded when the wave is disabled
            var requestGlobalProperties = new Dictionary<string, string?>
            {
                ["TestRequestProperty"] = "RequestValue",
            };

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters
                {
                    MultiThreaded = true,
                    MaxNodeCount = 4,
                    Loggers = [logger],
                    EnableNodeReuse = false,
                },
                new BuildRequestData(projectFile, requestGlobalProperties, null, ["Test"], null));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // With wave disabled, build-level properties are used (empty in this test),
            // so request-level properties should NOT appear
            logger.FullLog.ShouldNotContain("GlobalProperty: TestRequestProperty=RequestValue");
            logger.FullLog.ShouldContain("GlobalPropertyCount = 0");
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13333
        /// When callbacks are not supported (cross-version OOP TaskHost), RequestCores must
        /// throw NotImplementedException (not log a build error and return 0).
        /// Real callers (MonoAOTCompiler, EmccCompile, ILStrip, EmitBundleBase) catch this
        /// exception and fall back to their own parallelism estimate. The previous behavior
        /// of logging BuildErrorEventArgs caused the build to fail silently.
        /// </summary>
        [Fact]
        public void RequestCores_ThrowsNotImplementedWhenCallbacksNotSupported()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Explicitly do NOT set MSBUILDENABLETASKHOSTCALLBACKS — callbacks should be disabled.
            // Use RequestCoresWithFallbackTask which catches NotImplementedException like real callers do.
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(RequestCoresWithFallbackTask)}"" AssemblyFile=""{typeof(RequestCoresWithFallbackTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(RequestCoresWithFallbackTask)} CoreCount=""4"">
            <Output PropertyName=""GrantedResult"" TaskParameter=""GrantedCores"" />
            <Output PropertyName=""FellBack"" TaskParameter=""UsedFallback"" />
        </{nameof(RequestCoresWithFallbackTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            // Build must succeed — the task catches NotImplementedException and falls back.
            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // No errors should be logged — NotImplementedException is caught by the task, not by MSBuild.
            logger.ErrorCount.ShouldBe(0);

            // The task should have used its fallback path (NotImplementedException was thrown).
            logger.FullLog.ShouldContain("RequestCores threw NotImplementedException, using fallback");

            // GrantedCores should be the task's own fallback (CoreCount), not 0.
            logger.FullLog.ShouldContain("GrantedCores = 4");
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13333
        /// When callbacks are not supported, the full caller pattern (RequestCores with catch,
        /// then skip ReleaseCores) must work. This matches MonoAOTCompiler/EmccCompile/ILStrip:
        ///   try { cores = be9.RequestCores(N); }
        ///   catch (NotImplementedException) { be9 = null; }
        ///   finally { be9?.ReleaseCores(cores); }
        /// ReleaseCores must NOT be called when the fallback fires (be9 is nulled).
        /// </summary>
        [Fact]
        public void RequestAndReleaseCores_FallbackSkipsReleaseWhenCallbacksNotSupported()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Explicitly do NOT set MSBUILDENABLETASKHOSTCALLBACKS — callbacks should be disabled
            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(RequestCoresWithFallbackTask)}"" AssemblyFile=""{typeof(RequestCoresWithFallbackTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(RequestCoresWithFallbackTask)} CoreCount=""4"" ReleaseAfter=""true"">
            <Output PropertyName=""GrantedResult"" TaskParameter=""GrantedCores"" />
            <Output PropertyName=""FellBack"" TaskParameter=""UsedFallback"" />
        </{nameof(RequestCoresWithFallbackTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            // Build must succeed.
            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.ErrorCount.ShouldBe(0);

            // Fallback fired — ReleaseCores should have been skipped (be9 nulled in catch).
            logger.FullLog.ShouldContain("RequestCores threw NotImplementedException, using fallback");
            logger.FullLog.ShouldNotContain("ReleaseCores(");
        }
    }
}
