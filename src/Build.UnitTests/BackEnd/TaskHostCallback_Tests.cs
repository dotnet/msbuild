// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

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
        [InlineData(1, false)]  // MaxNodeCount=1 -> IsRunningMultipleNodes=false
        [InlineData(4, true)]   // MaxNodeCount=4 -> IsRunningMultipleNodes=true (even with one project)
        public void IsRunningMultipleNodes_WorksWithExplicitTaskHostFactory(int maxNodeCount, bool expectedResult)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

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
        /// Verifies RequestCores callback works when task is explicitly run in TaskHost via TaskHostFactory.
        /// The first RequestCores call should always return at least 1 (the implicit core).
        /// </summary>
        [Fact]
        public void RequestCores_WorksWithExplicitTaskHostFactory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

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
        /// Verifies BuildProjectFile callback works when task is explicitly run in TaskHost via TaskHostFactory.
        /// The child project should build and the task should return success.
        /// </summary>
        [Fact]
        public void BuildProjectFile_WorksWithExplicitTaskHostFactory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string childProject = env.CreateFile("Child.proj", """
                <Project>
                    <Target Name="Build">
                        <Message Text="ChildProjectBuilt" Importance="high" />
                    </Target>
                </Project>
                """).Path;

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(BuildProjectFileTask)}"" AssemblyFile=""{typeof(BuildProjectFileTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(BuildProjectFileTask)} ProjectFile=""{childProject}"" Targets=""Build"">
            <Output PropertyName=""Result"" TaskParameter=""BuildSucceeded"" />
        </{nameof(BuildProjectFileTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            bool.Parse(projectInstance.GetPropertyValue("Result")).ShouldBeTrue();
            logger.FullLog.ShouldContain("ChildProjectBuilt");
        }

        /// <summary>
        /// Verifies BuildProjectFile forwards global properties to the child build.
        /// </summary>
        [Fact]
        public void BuildProjectFile_ForwardsGlobalProperties()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string childProject = env.CreateFile("Child.proj", """
                <Project>
                    <Target Name="Build">
                        <Message Text="Config=$(Configuration)" Importance="high" />
                    </Target>
                </Project>
                """).Path;

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(BuildProjectFileTask)}"" AssemblyFile=""{typeof(BuildProjectFileTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(BuildProjectFileTask)} ProjectFile=""{childProject}"" Targets=""Build"" Properties=""Configuration=Release"">
            <Output PropertyName=""Result"" TaskParameter=""BuildSucceeded"" />
        </{nameof(BuildProjectFileTask)}>
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.FullLog.ShouldContain("Config=Release");
        }

        /// <summary>
        /// Verifies BuildProjectFile returns ITaskItem[] target outputs through the TaskHost callback.
        /// </summary>
        [Fact]
        public void BuildProjectFile_ReturnsTargetOutputs()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string childProject = env.CreateFile("Child.proj", """
                <Project>
                    <ItemGroup>
                        <OutputItem Include="Output1.dll">
                            <CustomMeta>Value1</CustomMeta>
                        </OutputItem>
                        <OutputItem Include="Output2.dll" />
                    </ItemGroup>
                    <Target Name="GetOutputs" Returns="@(OutputItem)">
                        <Message Text="GetOutputs executed" Importance="high" />
                    </Target>
                </Project>
                """).Path;

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(BuildProjectFileTask)}"" AssemblyFile=""{typeof(BuildProjectFileTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(BuildProjectFileTask)} ProjectFile=""{childProject}"" Targets=""GetOutputs"">
            <Output PropertyName=""Result"" TaskParameter=""BuildSucceeded"" />
            <Output ItemName=""Items"" TaskParameter=""OutputItems"" />
        </{nameof(BuildProjectFileTask)}>
        <Message Text=""OutputItemCount=@(Items->Count())"" Importance=""high"" />
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            bool.Parse(projectInstance.GetPropertyValue("Result")).ShouldBeTrue();
            logger.FullLog.ShouldContain("OutputItemCount=2");
        }

        /// <summary>
        /// Verifies BuildProjectFile returns false when the child project fails.
        /// </summary>
        [Fact]
        public void BuildProjectFile_ChildFailure_ReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string childProject = env.CreateFile("Child.proj", """
                <Project>
                    <Target Name="Build">
                        <Error Text="Intentional failure" />
                    </Target>
                </Project>
                """).Path;

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(BuildProjectFileTask)}"" AssemblyFile=""{typeof(BuildProjectFileTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""Test"">
        <{nameof(BuildProjectFileTask)} ProjectFile=""{childProject}"" Targets=""Build"">
            <Output PropertyName=""Result"" TaskParameter=""BuildSucceeded"" />
        </{nameof(BuildProjectFileTask)}>
        <Message Text=""ChildResult=$(Result)"" Importance=""high"" />
    </Target>
</Project>";

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);

            var logger = new MockLogger(_output);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { MaxNodeCount = 4, EnableNodeReuse = false, Loggers = [logger] },
                new BuildRequestData(projectInstance, targetsToBuild: ["Test"]));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.FullLog.ShouldContain("ChildResult=False");
        }

        /// <summary>
        /// Verifies BuildProjectFile auto-ejection works in multithreaded mode.
        /// </summary>
        [Fact]
        public void BuildProjectFile_WorksWhenAutoEjectedInMultiThreadedMode()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string testDir = env.CreateFolder().Path;

            string childProject = Path.Combine(testDir, "Child.proj");
            File.WriteAllText(childProject, """
                <Project>
                    <Target Name="Build">
                        <Message Text="ChildBuiltInMT" Importance="high" />
                    </Target>
                </Project>
                """);

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(BuildProjectFileTask)}"" AssemblyFile=""{typeof(BuildProjectFileTask).Assembly.Location}"" />
    <Target Name=""Test"">
        <{nameof(BuildProjectFileTask)} ProjectFile=""{childProject}"" Targets=""Build"">
            <Output PropertyName=""Result"" TaskParameter=""BuildSucceeded"" />
        </{nameof(BuildProjectFileTask)}>
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
            logger.FullLog.ShouldContain("ChildBuiltInMT");
        }
    }
}
