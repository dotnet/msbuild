// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Pure unit tests for TaskHost callback packet serialization.
    /// No I/O or BuildManager -- just round-trip translation.
    /// </summary>
    public class TaskHostCallbackPacket_Tests
    {
        [Fact]
        public void TaskHostIsRunningMultipleNodesRequest_RoundTrip_Serialization()
        {
            var request = new TaskHostIsRunningMultipleNodesRequest();
            request.RequestId = 42;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostIsRunningMultipleNodesRequest)TaskHostIsRunningMultipleNodesRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(42);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostIsRunningMultipleNodesRequest);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TaskHostIsRunningMultipleNodesResponse_RoundTrip_Serialization(bool isRunningMultipleNodes)
        {
            var response = new TaskHostIsRunningMultipleNodesResponse(123, isRunningMultipleNodes);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostIsRunningMultipleNodesResponse)TaskHostIsRunningMultipleNodesResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(123);
            deserialized.IsRunningMultipleNodes.ShouldBe(isRunningMultipleNodes);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostIsRunningMultipleNodesResponse);
        }

        [Theory]
        [InlineData(4, false)]  // RequestCores(4)
        [InlineData(2, true)]   // ReleaseCores(2)
        public void TaskHostCoresRequest_RoundTrip_Serialization(int cores, bool isRelease)
        {
            var request = new TaskHostCoresRequest(cores, isRelease);
            request.RequestId = 77;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostCoresRequest)TaskHostCoresRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(77);
            deserialized.RequestedCores.ShouldBe(cores);
            deserialized.IsRelease.ShouldBe(isRelease);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostCoresRequest);
        }

        [Theory]
        [InlineData(0)]   // ReleaseCores acknowledgment
        [InlineData(3)]   // RequestCores granted 3
        public void TaskHostCoresResponse_RoundTrip_Serialization(int grantedCores)
        {
            var response = new TaskHostCoresResponse(99, grantedCores);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostCoresResponse)TaskHostCoresResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(99);
            deserialized.GrantedCores.ShouldBe(grantedCores);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostCoresResponse);
        }

        [Fact]
        public void TaskHostBuildRequest_RoundTrip_Serialization()
        {
            Dictionary<string, string>?[] globalProps = [new(StringComparer.OrdinalIgnoreCase) { ["Configuration"] = "Release" }, null];
            List<string>?[] removeProps = [new() { "Platform" }, null];
            string?[] toolsVersions = ["17.0", null];
            var request = new TaskHostBuildRequest(
                ["proj1.csproj", "proj2.csproj"],
                ["Build", "Test"],
                globalProps,
                removeProps,
                toolsVersions!,
                returnTargetOutputs: true);
            request.RequestId = 55;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(55);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostBuildRequest);
            deserialized.ProjectFileNames.ShouldBe(["proj1.csproj", "proj2.csproj"]);
            deserialized.TargetNames.ShouldBe(["Build", "Test"]);
            deserialized.ToolsVersions.ShouldBe(toolsVersions!);
            deserialized.ReturnTargetOutputs.ShouldBeTrue();
            deserialized.GlobalProperties!.Length.ShouldBe(2);
            deserialized.GlobalProperties![0]!["Configuration"].ShouldBe("Release");
            deserialized.GlobalProperties[1].ShouldBeNull();
            deserialized.RemoveGlobalProperties!.Length.ShouldBe(2);
            deserialized.RemoveGlobalProperties![0].ShouldBe(["Platform"]);
            deserialized.RemoveGlobalProperties[1].ShouldBeNull();
        }

        [Fact]
        public void TaskHostBuildRequest_NullArrays_RoundTrip_Serialization()
        {
            var request = new TaskHostBuildRequest(
                null, null, null, null, null, returnTargetOutputs: false);
            request.RequestId = 10;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(10);
            deserialized.ProjectFileNames.ShouldBeNull();
            deserialized.TargetNames.ShouldBeNull();
            deserialized.GlobalProperties.ShouldBeNull();
            deserialized.RemoveGlobalProperties.ShouldBeNull();
            deserialized.ToolsVersions.ShouldBeNull();
            deserialized.ReturnTargetOutputs.ShouldBeFalse();
        }

        [Fact]
        public void TaskHostBuildResponse_Success_WithOutputs_RoundTrip_Serialization()
        {
            var outputs = new List<Dictionary<string, TaskParameter>>
            {
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Build"] = new TaskParameter(new ITaskItem[] { new Utilities.TaskItem("item1.dll") }),
                    ["Test"] = new TaskParameter(new ITaskItem[] { new Utilities.TaskItem("result.trx") })
                }
            };

            var response = new TaskHostBuildResponse(88, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(88);
            deserialized.Success.ShouldBeTrue();
            deserialized.Type.ShouldBe(NodePacketType.TaskHostBuildResponse);
            deserialized.TargetOutputsPerProject.ShouldNotBeNull();
            deserialized.TargetOutputsPerProject.Count.ShouldBe(1);
            deserialized.TargetOutputsPerProject[0].ContainsKey("Build").ShouldBeTrue();

            var buildEngineResult = deserialized.ToBuildEngineResult();
            buildEngineResult.Result.ShouldBeTrue();
            buildEngineResult.TargetOutputsPerProject.Count.ShouldBe(1);
            buildEngineResult.TargetOutputsPerProject[0]["Build"].Length.ShouldBe(1);
            buildEngineResult.TargetOutputsPerProject[0]["Build"][0].ItemSpec.ShouldBe("item1.dll");
        }

        [Fact]
        public void TaskHostBuildResponse_Failure_NoOutputs_RoundTrip_Serialization()
        {
            var response = new TaskHostBuildResponse(33, false, null);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            deserialized.RequestId.ShouldBe(33);
            deserialized.Success.ShouldBeFalse();
            deserialized.TargetOutputsPerProject.ShouldBeNull();

            var buildEngineResult = deserialized.ToBuildEngineResult();
            buildEngineResult.Result.ShouldBeFalse();
        }
    }
}
