// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for IBuildEngine callback support in TaskHost.
    /// Covers packet serialization and end-to-end integration tests.
    /// </summary>
    public class TaskHostCallback_Tests
    {
        private readonly ITestOutputHelper _output;

        public TaskHostCallback_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Packet Serialization Tests

        [Fact]
        public void TaskHostQueryRequest_RoundTrip_Serialization()
        {
            var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            request.RequestId = 42;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryRequest)TaskHostQueryRequest.FactoryForDeserialization(readTranslator);

            deserialized.Query.ShouldBe(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
            deserialized.RequestId.ShouldBe(42);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostQueryRequest);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TaskHostQueryResponse_RoundTrip_Serialization(bool boolResult)
        {
            var response = new TaskHostQueryResponse(123, boolResult);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostQueryResponse)TaskHostQueryResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.BoolResult.ShouldBe(boolResult);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostQueryResponse);
        }

        #endregion

        #region IsRunningMultipleNodes Callback Tests

        /// <summary>
        /// Verifies IsRunningMultipleNodes callback works when task is explicitly run in TaskHost via TaskHostFactory.
        /// </summary>
        [Theory]
        [InlineData(1, false)]  // Single node build
        [InlineData(4, true)]   // Multi-node build
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
                new BuildRequestData(projectFile, new Dictionary<string, string>(), null, ["Test"], null));

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was ejected to TaskHost
            logger.FullLog.ShouldContain("external task host");

            // Verify callback returned correct value
            logger.FullLog.ShouldContain($"IsRunningMultipleNodes = {expectedResult}");
        }

        #endregion
    }
}
