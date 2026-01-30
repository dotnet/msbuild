// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for TaskHostBuildRequest and TaskHostBuildResponse packets.
    /// </summary>
    public class TaskHostBuildPacket_Tests
    {
        #region TaskHostBuildRequest Tests

        [Fact]
        public void TaskHostBuildRequest_BuildEngine1_RoundTrip()
        {
            var globalProps = new Hashtable { { "Configuration", "Debug" }, { "Platform", "x64" } };
            var request = TaskHostBuildRequest.CreateBuildEngine1Request(
                "test.csproj",
                new[] { "Build", "Test" },
                globalProps);
            request.RequestId = 100;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.Variant.ShouldBe(TaskHostBuildRequest.BuildRequestVariant.BuildEngine1);
            deserialized.ProjectFileName.ShouldBe("test.csproj");
            deserialized.TargetNames.ShouldBe(new[] { "Build", "Test" });
            deserialized.GlobalProperties["Configuration"].ShouldBe("Debug");
            deserialized.GlobalProperties["Platform"].ShouldBe("x64");
            deserialized.RequestId.ShouldBe(100);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostBuildRequest);
        }

        [Fact]
        public void TaskHostBuildRequest_BuildEngine2Single_RoundTrip()
        {
            var globalProps = new Hashtable { { "Prop1", "Value1" } };
            var request = TaskHostBuildRequest.CreateBuildEngine2SingleRequest(
                "project.csproj",
                new[] { "Compile" },
                globalProps,
                "15.0");
            request.RequestId = 200;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.Variant.ShouldBe(TaskHostBuildRequest.BuildRequestVariant.BuildEngine2Single);
            deserialized.ProjectFileName.ShouldBe("project.csproj");
            deserialized.ToolsVersion.ShouldBe("15.0");
            deserialized.RequestId.ShouldBe(200);
        }

        [Fact]
        public void TaskHostBuildRequest_BuildEngine2Parallel_RoundTrip()
        {
            var globalProps1 = new Hashtable { { "Config", "Debug" } };
            var globalProps2 = new Hashtable { { "Config", "Release" } };

            var request = TaskHostBuildRequest.CreateBuildEngine2ParallelRequest(
                new[] { "proj1.csproj", "proj2.csproj" },
                new[] { "Build" },
                new IDictionary[] { globalProps1, globalProps2 },
                new[] { "15.0", "16.0" },
                useResultsCache: true,
                unloadProjectsOnCompletion: false);
            request.RequestId = 300;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.Variant.ShouldBe(TaskHostBuildRequest.BuildRequestVariant.BuildEngine2Parallel);
            deserialized.ProjectFileNames.ShouldBe(new[] { "proj1.csproj", "proj2.csproj" });
            deserialized.ToolsVersions.ShouldBe(new[] { "15.0", "16.0" });
            deserialized.GlobalPropertiesArray[0]["Config"].ShouldBe("Debug");
            deserialized.GlobalPropertiesArray[1]["Config"].ShouldBe("Release");
            deserialized.UseResultsCache.ShouldBeTrue();
            deserialized.UnloadProjectsOnCompletion.ShouldBeFalse();
            deserialized.RequestId.ShouldBe(300);
        }

        [Fact]
        public void TaskHostBuildRequest_BuildEngine3Parallel_RoundTrip()
        {
            var globalProps = new Hashtable { { "Prop", "Val" } };
            var removeProps = new List<string> { "RemoveMe", "AndMe" };

            var request = TaskHostBuildRequest.CreateBuildEngine3ParallelRequest(
                new[] { "project.csproj" },
                new[] { "Build" },
                new IDictionary[] { globalProps },
                new IList<string>[] { removeProps },
                new[] { "Current" },
                returnTargetOutputs: true);
            request.RequestId = 400;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.Variant.ShouldBe(TaskHostBuildRequest.BuildRequestVariant.BuildEngine3Parallel);
            deserialized.RemoveGlobalProperties[0].ShouldBe(new List<string> { "RemoveMe", "AndMe" });
            deserialized.ReturnTargetOutputs.ShouldBeTrue();
            deserialized.RequestId.ShouldBe(400);
        }

        [Fact]
        public void TaskHostBuildRequest_NullGlobalProperties_RoundTrip()
        {
            var request = TaskHostBuildRequest.CreateBuildEngine1Request(
                "test.csproj",
                new[] { "Build" },
                null);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            request.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildRequest)TaskHostBuildRequest.FactoryForDeserialization(readTranslator);

            deserialized.GlobalProperties.ShouldBeNull();
        }

        [Fact]
        public void TaskHostBuildRequest_ImplementsITaskHostCallbackPacket()
        {
            var request = TaskHostBuildRequest.CreateBuildEngine1Request("test.csproj", null, null);
            request.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        #endregion

        #region TaskHostBuildResponse Tests

        [Fact]
        public void TaskHostBuildResponse_SingleProject_Success_RoundTrip()
        {
            var outputs = new Hashtable
            {
                { "Build", new ITaskItem[] { new TaskItem("output.dll") } }
            };
            var response = new TaskHostBuildResponse(100, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            deserialized.OverallResult.ShouldBeTrue();
            deserialized.RequestId.ShouldBe(100);
            deserialized.Type.ShouldBe(NodePacketType.TaskHostBuildResponse);

            var restoredOutputs = deserialized.GetTargetOutputsForSingleProject();
            restoredOutputs.Contains("Build").ShouldBeTrue();
            var items = (ITaskItem[])restoredOutputs["Build"];
            items.Length.ShouldBe(1);
            items[0].ItemSpec.ShouldBe("output.dll");
        }

        [Fact]
        public void TaskHostBuildResponse_SingleProject_Failure_RoundTrip()
        {
            var response = new TaskHostBuildResponse(200, false, (IDictionary)null);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            deserialized.OverallResult.ShouldBeFalse();
            deserialized.RequestId.ShouldBe(200);
            deserialized.GetTargetOutputsForSingleProject().ShouldBeNull();
        }

        [Fact]
        public void TaskHostBuildResponse_TaskItemWithMetadata_RoundTrip()
        {
            var item = new TaskItem("test.cs");
            item.SetMetadata("BuildAction", "Compile");
            item.SetMetadata("Link", @"Source\test.cs");

            var outputs = new Hashtable
            {
                { "Compile", new ITaskItem[] { item } }
            };
            var response = new TaskHostBuildResponse(300, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            var restoredOutputs = deserialized.GetTargetOutputsForSingleProject();
            var restoredItem = ((ITaskItem[])restoredOutputs["Compile"])[0];
            restoredItem.ItemSpec.ShouldBe("test.cs");
            restoredItem.GetMetadata("BuildAction").ShouldBe("Compile");
            restoredItem.GetMetadata("Link").ShouldBe(@"Source\test.cs");
        }

        [Fact]
        public void TaskHostBuildResponse_MultipleTargets_RoundTrip()
        {
            var outputs = new Hashtable
            {
                { "Build", new ITaskItem[] { new TaskItem("app.exe") } },
                { "Test", new ITaskItem[] { new TaskItem("test1.dll"), new TaskItem("test2.dll") } },
                { "Pack", new ITaskItem[] { new TaskItem("app.1.0.0.nupkg") } }
            };
            var response = new TaskHostBuildResponse(400, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            var restoredOutputs = deserialized.GetTargetOutputsForSingleProject();
            ((ITaskItem[])restoredOutputs["Build"]).Length.ShouldBe(1);
            ((ITaskItem[])restoredOutputs["Test"]).Length.ShouldBe(2);
            ((ITaskItem[])restoredOutputs["Pack"]).Length.ShouldBe(1);
        }

        [Fact]
        public void TaskHostBuildResponse_BuildEngine3_MultipleProjects_RoundTrip()
        {
            var outputs = new List<IDictionary<string, ITaskItem[]>>
            {
                new Dictionary<string, ITaskItem[]>
                {
                    { "Build", new ITaskItem[] { new TaskItem("proj1.dll") } }
                },
                new Dictionary<string, ITaskItem[]>
                {
                    { "Build", new ITaskItem[] { new TaskItem("proj2.dll") } }
                }
            };

            var response = new TaskHostBuildResponse(500, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            deserialized.OverallResult.ShouldBeTrue();
            deserialized.RequestId.ShouldBe(500);

            var restoredOutputs = deserialized.GetTargetOutputsForBuildEngineResult();
            restoredOutputs.Count.ShouldBe(2);
            restoredOutputs[0]["Build"][0].ItemSpec.ShouldBe("proj1.dll");
            restoredOutputs[1]["Build"][0].ItemSpec.ShouldBe("proj2.dll");
        }

        [Fact]
        public void TaskHostBuildResponse_EmptyTargetOutputs_RoundTrip()
        {
            var outputs = new Hashtable(); // Empty but not null
            var response = new TaskHostBuildResponse(600, true, outputs);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            response.Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            var deserialized = (TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(readTranslator);

            var restoredOutputs = deserialized.GetTargetOutputsForSingleProject();
            restoredOutputs.ShouldNotBeNull();
            restoredOutputs.Count.ShouldBe(0);
        }

        [Fact]
        public void TaskHostBuildResponse_ImplementsITaskHostCallbackPacket()
        {
            var response = new TaskHostBuildResponse(1, true, (IDictionary)null);
            response.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
        }

        #endregion
    }
}
