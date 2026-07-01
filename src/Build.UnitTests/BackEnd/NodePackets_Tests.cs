// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;
using Xunit;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Each packet is split up into a region, the region contains the tests for
    /// a given packet type.
    /// </summary>
    public class NodePackets_Tests
    {
        #region LogMessagePacket Tests

        /// <summary>
        /// Verify a null build event throws an exception
        /// </summary>
        [Fact]
        public void LogMessageConstructorNullBuildEvent()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                LogMessagePacket packet = new LogMessagePacket(null);
            });
        }

        /// <summary>
        /// Verify when creating a LogMessagePacket
        /// that the correct Event Type is set.
        /// </summary>
        [Fact]
        public void VerifyEventType()
        {
            BuildFinishedEventArgs buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
            BuildStartedEventArgs buildStarted = new BuildStartedEventArgs("Message", "Help");
            BuildMessageEventArgs lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
            TaskStartedEventArgs taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
            TaskFinishedEventArgs taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
            TaskCommandLineEventArgs commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
            TaskParameterEventArgs taskParameter = CreateTaskParameter();
            BuildWarningEventArgs warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            BuildErrorEventArgs error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            TargetStartedEventArgs targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
            TargetFinishedEventArgs targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
            TargetSkippedEventArgs targetSkipped = CreateTargetSkipped();
            ProjectStartedEventArgs projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
            ProjectFinishedEventArgs projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
            ExternalProjectStartedEventArgs externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");
            ExternalProjectFinishedEventArgs externalFinishedEvent = new("message", "help", "senderName", "projectFile", true);
            ProjectEvaluationStartedEventArgs evaluationStarted = new ProjectEvaluationStartedEventArgs();
            ProjectEvaluationFinishedEventArgs evaluationFinished = new ProjectEvaluationFinishedEventArgs();
            AssemblyLoadBuildEventArgs assemblyLoad = new(AssemblyLoadingContext.Evaluation, null, null, "path", Guid.NewGuid(), null);
            ExtendedBuildErrorEventArgs extError = new("extError", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            ExtendedBuildWarningEventArgs extWarning = new("extWarn", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            ExtendedBuildMessageEventArgs extMessage = new("extMsg", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender", MessageImportance.Normal);
            ExtendedCustomBuildEventArgs extCustom = new("extCustom", "message", "help", "sender");
            CriticalBuildMessageEventArgs criticalMessage = new("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", DateTime.Now, "arg1");
            ExtendedCriticalBuildMessageEventArgs extCriticalMessage = new("extCritMsg", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", DateTime.Now, "arg1");
            PropertyInitialValueSetEventArgs propInit = new("prop", "val", "propsource", "message", "help", "sender", MessageImportance.Normal);
            MetaprojectGeneratedEventArgs metaProjectGenerated = new("metaName", "path", "message");
            PropertyReassignmentEventArgs propReassign = new("prop", "prevValue", "newValue", "loc", "message", "help", "sender", MessageImportance.Normal);
            ResponseFileUsedEventArgs responseFileUsed = new("path");
            UninitializedPropertyReadEventArgs uninitializedPropertyRead = new("prop", "message", "help", "sender", MessageImportance.Normal);
            EnvironmentVariableReadEventArgs environmentVariableRead = new("env", "message", "file", 0, 0);
            GeneratedFileUsedEventArgs generatedFileUsed = new GeneratedFileUsedEventArgs("path", "some content");
            BuildSubmissionStartedEventArgs buildSubmissionStarted = new(new Dictionary<string, string> { { "Value1", "Value2" } }, ["Path1"], ["TargetName"], BuildRequestDataFlags.ReplaceExistingProjectInstance, 123);
            BuildCheckTracingEventArgs buildCheckTracing = new();
            BuildCanceledEventArgs buildCanceled = new("message", DateTime.UtcNow);
            WorkerNodeTelemetryEventArgs workerNodeTelemetry = new();

            VerifyLoggingPacket(buildFinished, LoggingEventType.BuildFinishedEvent);
            VerifyLoggingPacket(buildStarted, LoggingEventType.BuildStartedEvent);
            VerifyLoggingPacket(lowMessage, LoggingEventType.BuildMessageEvent);
            VerifyLoggingPacket(taskStarted, LoggingEventType.TaskStartedEvent);
            VerifyLoggingPacket(taskFinished, LoggingEventType.TaskFinishedEvent);
            VerifyLoggingPacket(commandLine, LoggingEventType.TaskCommandLineEvent);
            VerifyLoggingPacket(taskParameter, LoggingEventType.TaskParameterEvent);
            VerifyLoggingPacket(warning, LoggingEventType.BuildWarningEvent);
            VerifyLoggingPacket(error, LoggingEventType.BuildErrorEvent);
            VerifyLoggingPacket(targetStarted, LoggingEventType.TargetStartedEvent);
            VerifyLoggingPacket(targetFinished, LoggingEventType.TargetFinishedEvent);
            VerifyLoggingPacket(targetSkipped, LoggingEventType.TargetSkipped);
            VerifyLoggingPacket(projectStarted, LoggingEventType.ProjectStartedEvent);
            VerifyLoggingPacket(projectFinished, LoggingEventType.ProjectFinishedEvent);
            VerifyLoggingPacket(evaluationStarted, LoggingEventType.ProjectEvaluationStartedEvent);
            VerifyLoggingPacket(evaluationFinished, LoggingEventType.ProjectEvaluationFinishedEvent);
            VerifyLoggingPacket(externalStartedEvent, LoggingEventType.ExternalProjectStartedEvent);
            VerifyLoggingPacket(externalFinishedEvent, LoggingEventType.ExternalProjectFinishedEvent);
            VerifyLoggingPacket(assemblyLoad, LoggingEventType.AssemblyLoadEvent);
            VerifyLoggingPacket(extError, LoggingEventType.ExtendedBuildErrorEvent);
            VerifyLoggingPacket(extWarning, LoggingEventType.ExtendedBuildWarningEvent);
            VerifyLoggingPacket(extMessage, LoggingEventType.ExtendedBuildMessageEvent);
            VerifyLoggingPacket(extCustom, LoggingEventType.ExtendedCustomEvent);
            VerifyLoggingPacket(criticalMessage, LoggingEventType.CriticalBuildMessage);
            VerifyLoggingPacket(extCriticalMessage, LoggingEventType.ExtendedCriticalBuildMessageEvent);
            VerifyLoggingPacket(propInit, LoggingEventType.PropertyInitialValueSet);
            VerifyLoggingPacket(metaProjectGenerated, LoggingEventType.MetaprojectGenerated);
            VerifyLoggingPacket(propReassign, LoggingEventType.PropertyReassignment);
            VerifyLoggingPacket(responseFileUsed, LoggingEventType.ResponseFileUsedEvent);
            VerifyLoggingPacket(uninitializedPropertyRead, LoggingEventType.UninitializedPropertyRead);
            VerifyLoggingPacket(environmentVariableRead, LoggingEventType.EnvironmentVariableReadEvent);
            VerifyLoggingPacket(generatedFileUsed, LoggingEventType.GeneratedFileUsedEvent);
            VerifyLoggingPacket(buildSubmissionStarted, LoggingEventType.BuildSubmissionStartedEvent);
            VerifyLoggingPacket(buildCheckTracing, LoggingEventType.BuildCheckTracingEvent);
            VerifyLoggingPacket(buildCanceled, LoggingEventType.BuildCanceledEvent);
            VerifyLoggingPacket(workerNodeTelemetry, LoggingEventType.WorkerNodeTelemetryEvent);
        }

        private static BuildEventContext CreateBuildEventContext()
        {
            return new BuildEventContext(1, 2, 3, 4, 5, 6, 7);
        }

        private static ProjectEvaluationStartedEventArgs CreateProjectEvaluationStarted()
        {
            string projectFile = "test.csproj";
            var result = new ProjectEvaluationStartedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationStarted"),
                projectFile)
            {
                ProjectFile = projectFile
            };
            result.BuildEventContext = CreateBuildEventContext();

            return result;
        }

        private static ProjectEvaluationFinishedEventArgs CreateProjectEvaluationFinished()
        {
            string projectFile = "test.csproj";
            var result = new ProjectEvaluationFinishedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationFinished"),
                projectFile)
            {
                ProjectFile = projectFile,
                GlobalProperties = CreateProperties(),
                Properties = CreateProperties(),
                Items = new ArrayList
                {
                    new DictionaryEntry("Compile", new TaskItemData("a", null)),
                    new DictionaryEntry("Compile", new TaskItemData("b", CreateStringDictionary())),
                    new DictionaryEntry("Reference", new TaskItemData("c", CreateStringDictionary())),
                }
            };
            result.BuildEventContext = CreateBuildEventContext();

            return result;
        }

        private static IEnumerable CreateProperties()
        {
            return new ArrayList
            {
                new DictionaryEntry("a", "b"),
                new DictionaryEntry("c", "d")
            };
        }

        private static Dictionary<string, string> CreateStringDictionary()
        {
            return new Dictionary<string, string>
            {
                { "a", "b" },
                { "c", "d" }
            };
        }

        private static TaskItemData[] CreateTaskItems()
        {
            var items = new TaskItemData[]
            {
                new TaskItemData("ItemSpec1", null),
                new TaskItemData("ItemSpec1", CreateStringDictionary()),
                new TaskItemData("ItemSpec2", Enumerable.Range(1, 3).ToDictionary(i => i.ToString(), i => i.ToString() + "value"))
            };
            return items;
        }

        private static TaskParameterEventArgs CreateTaskParameter()
        {
            // touch ItemGroupLoggingHelper to ensure static constructor runs
            _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;

            var items = CreateTaskItems();
            var result = new TaskParameterEventArgs(
                TaskParameterMessageKind.TaskInput,
                "ItemName",
                items,
                logItemMetadata: true,
                DateTime.MinValue);
            result.LineNumber = 30000;
            result.ColumnNumber = 50;

            // normalize line endings as we can't rely on the line endings of NodePackets_Tests.cs
            Assert.Equal(@"Task Parameter:
    ItemName=
        ItemSpec1
        ItemSpec1
                a=b
                c=d
        ItemSpec2
                1=1value
                2=2value
                3=3value".Replace("\r\n", "\n"), result.Message);

            return result;
        }

        private static TargetSkippedEventArgs CreateTargetSkipped()
        {
            var result = new TargetSkippedEventArgs(message: null)
            {
                BuildReason = TargetBuiltReason.DependsOn,
                SkipReason = TargetSkipReason.PreviouslyBuiltSuccessfully,
                BuildEventContext = CreateBuildEventContext(),
                OriginalBuildEventContext = CreateBuildEventContext(),
                Condition = "$(Condition) == 'true'",
                EvaluatedCondition = "'true' == 'true'",
                Importance = MessageImportance.Normal,
                OriginallySucceeded = true,
                ProjectFile = "1.proj",
                TargetFile = "1.proj",
                TargetName = "Build",
                ParentTarget = "ParentTarget"
            };
            return result;
        }

        /// <summary>
        /// Tests serialization of LogMessagePacket with each kind of event type.
        /// </summary>
        [Fact]
        public void TestTranslation()
        {
            // need to touch the type so that the static constructor runs
            _ = ItemGroupLoggingHelper.OutputItemParameterMessagePrefix;

            TaskItem item = new TaskItem("Hello", "my.proj");
            List<TaskItem> targetOutputs = new List<TaskItem>();
            targetOutputs.Add(item);

            string _initialTargetOutputLogging = Environment.GetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING");
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "1");
            try
            {
                BuildEventArgs[] testArgs = new BuildEventArgs[]
                {
                    new ResponseFileUsedEventArgs("path"),
                    new UninitializedPropertyReadEventArgs("prop", "message", "help", "sender", MessageImportance.Normal),
                    new EnvironmentVariableReadEventArgs("env", "message", "file", 0, 0) { BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6) },
                    new PropertyReassignmentEventArgs("prop", "prevValue", "newValue", "loc", "message", "help", "sender", MessageImportance.Normal),
                    new PropertyInitialValueSetEventArgs("prop", "val", "propsource", "message", "help", "sender", MessageImportance.Normal),
                    new MetaprojectGeneratedEventArgs("metaName", "path", "message"),
                    new CriticalBuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", DateTime.Now, "arg1"),
                    new BuildFinishedEventArgs("Message", "Keyword", true),
                    new BuildStartedEventArgs("Message", "Help"),
                    new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low),
                    new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName")
                    {
                        LineNumber = 345,
                        ColumnNumber = 123
                    },
                    new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true),
                    new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low),
                    CreateTaskParameter(),
                    new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"),
                    new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"),
                    new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"),
                    new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true, targetOutputs),
                    new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null),
                    new ProjectFinishedEventArgs("message", "help", "ProjectFile", true),
                    new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames"),
                    new ExternalProjectFinishedEventArgs("message", "help", "senderName", "projectFile", true),
                    CreateProjectEvaluationStarted(),
                    CreateProjectEvaluationFinished(),
                    new AssemblyLoadBuildEventArgs(AssemblyLoadingContext.Evaluation, "init", "aname", "path", Guid.NewGuid(), "domain", MessageImportance.Normal),
                    CreateTargetSkipped(),
                    new ExtendedBuildErrorEventArgs("extError", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender", DateTime.UtcNow, "arg1")
                    {
                        ExtendedData = /*lang=json*/ "{'long-json':'mostly-strings'}",
                        ExtendedMetadata = new Dictionary<string, string> { { "m1", "v1" }, { "m2", "v2" } },
                        BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
                    },
                    new ExtendedBuildWarningEventArgs("extWarn", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender", DateTime.UtcNow, "arg1")
                    {
                        ExtendedData = /*lang=json*/ "{'long-json':'mostly-strings'}",
                        ExtendedMetadata = new Dictionary<string, string> { { "m1", "v1" }, { "m2", "v2" } },
                        BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
                    },
                    new ExtendedBuildMessageEventArgs("extWarn", "SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender", MessageImportance.Normal, DateTime.UtcNow, "arg1")
                    {
                        ExtendedData = /*lang=json*/ "{'long-json':'mostly-strings'}",
                        ExtendedMetadata = new Dictionary<string, string> { { "m1", "v1" }, { "m2", "v2" } },
                        BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
                    },
                    new ExtendedCustomBuildEventArgs("extCustom", "message", "help", "sender", DateTime.UtcNow, "arg1")
                    {
                        ExtendedData = /*lang=json*/ "{'long-json':'mostly-strings'}",
                        ExtendedMetadata = new Dictionary<string, string> { { "m1", "v1" }, { "m2", "v2" } },
                        BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
                    },
                    new ExtendedCriticalBuildMessageEventArgs("extCritMsg", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", DateTime.Now, "arg1")
                    {
                        ExtendedData = /*lang=json*/ "{'long-json':'mostly-strings'}",
                        ExtendedMetadata = new Dictionary<string, string> { { "m1", "v1" }, { "m2", "v2" } },
                        BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
                    },
                    new GeneratedFileUsedEventArgs("path", "some content"),
                };
                foreach (BuildEventArgs arg in testArgs)
                {
                    LogMessagePacket packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, arg));

                    ((ITranslatable)packet).Translate(TranslationHelpers.GetWriteTranslator());
                    INodePacket tempPacket = LogMessagePacket.FactoryForDeserialization(TranslationHelpers.GetReadTranslator()) as LogMessagePacket;

                    LogMessagePacket deserializedPacket = tempPacket as LogMessagePacket;

                    packet.Should().BeEquivalentTo(deserializedPacket, options => options
                        .PreferringRuntimeMemberTypes());

                    BuildEventArgs args = packet.NodeBuildEvent?.Value;
                    BuildEventArgs desArgs = deserializedPacket?.NodeBuildEvent?.Value;
                    desArgs.Should().BeEquivalentTo(args, options => options
                        .PreferringRuntimeMemberTypes()
                        // Since we use struct DictionaryEntry of class TaskItemData, generated DictionaryEntry.Equals compare TaskItemData by references.
                        // Bellow will instruct equivalency test to not use DictionaryEntry.Equals but its public members for equivalency tests.
                        .ComparingByMembers<DictionaryEntry>()
                        .WithTracing(), "Roundtrip deserialization of message type {0} should be equivalent", args.GetType().Name);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", _initialTargetOutputLogging);
            }
        }

        /// <summary>
        /// Verify the LoggingMessagePacket is properly created from a build event.
        /// This includes the packet type and the event type depending on which build event arg is passed in.
        /// </summary>
        /// <param name="buildEvent">Build event to put into a packet, and verify after packet creation</param>
        /// <param name="logEventType">What is the expected logging event type</param>
        private static void VerifyLoggingPacket(BuildEventArgs buildEvent, LoggingEventType logEventType)
        {
            LogMessagePacket packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, buildEvent));
            Assert.Equal(logEventType, packet.EventType);
            Assert.Equal(NodePacketType.LogMessage, packet.Type);
            Assert.True(Object.ReferenceEquals(buildEvent, packet.NodeBuildEvent.Value.Value)); // "Expected buildEvent to have the same object reference as packet.BuildEvent"
        }

        #endregion
    }
}
