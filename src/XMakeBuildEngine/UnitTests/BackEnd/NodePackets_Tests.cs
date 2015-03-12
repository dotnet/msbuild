// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test the node packets are created properly</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Each packet is split up into a region, the region contains the tests for 
    /// a given packet type.
    /// </summary>
    [TestClass]
    public class NodePackets_Tests
    {
        #region LogMessagePacket Tests

        /// <summary>
        /// Verify a null build event throws an exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void LogMessageConstructorNullBuildEvent()
        {
            LogMessagePacket packet = new LogMessagePacket(null);
        }

        /// <summary>
        /// Verify when creating a LogMessagePacket
        /// that the correct Event Type is set.
        /// </summary>
        [TestMethod]
        public void VerifyEventType()
        {
            BuildFinishedEventArgs buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
            BuildStartedEventArgs buildStarted = new BuildStartedEventArgs("Message", "Help");
            BuildMessageEventArgs lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
            TaskStartedEventArgs taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
            TaskFinishedEventArgs taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
            TaskCommandLineEventArgs commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
            BuildWarningEventArgs warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            BuildErrorEventArgs error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            TargetStartedEventArgs targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
            TargetFinishedEventArgs targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
            ProjectStartedEventArgs projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
            ProjectFinishedEventArgs projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
            ExternalProjectStartedEventArgs externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

            VerifyLoggingPacket(buildFinished, LoggingEventType.BuildFinishedEvent);
            VerifyLoggingPacket(buildStarted, LoggingEventType.BuildStartedEvent);
            VerifyLoggingPacket(lowMessage, LoggingEventType.BuildMessageEvent);
            VerifyLoggingPacket(taskStarted, LoggingEventType.TaskStartedEvent);
            VerifyLoggingPacket(taskFinished, LoggingEventType.TaskFinishedEvent);
            VerifyLoggingPacket(commandLine, LoggingEventType.TaskCommandLineEvent);
            VerifyLoggingPacket(warning, LoggingEventType.BuildWarningEvent);
            VerifyLoggingPacket(error, LoggingEventType.BuildErrorEvent);
            VerifyLoggingPacket(targetStarted, LoggingEventType.TargetStartedEvent);
            VerifyLoggingPacket(targetFinished, LoggingEventType.TargetFinishedEvent);
            VerifyLoggingPacket(projectStarted, LoggingEventType.ProjectStartedEvent);
            VerifyLoggingPacket(projectFinished, LoggingEventType.ProjectFinishedEvent);
            VerifyLoggingPacket(externalStartedEvent, LoggingEventType.CustomEvent);
        }

        /// <summary>
        /// Tests serialization of LogMessagePacket with each kind of event type.
        /// </summary>
        [TestMethod]
        public void TestTranslation()
        {
            TaskItem item = new TaskItem("Hello", "my.proj");
            List<TaskItem> targetOutputs = new List<TaskItem>();
            targetOutputs.Add(item);

            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "1");
            BuildEventArgs[] testArgs = new BuildEventArgs[]
            {
                new BuildFinishedEventArgs("Message", "Keyword", true),
                new BuildStartedEventArgs("Message", "Help"),
                new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low),
                new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName"),
                new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true),
                new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low),
                new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"),
                new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"),
                new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"),
                new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true, targetOutputs),
                new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null),
                new ProjectFinishedEventArgs("message", "help", "ProjectFile", true),
                new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames")
            };

            foreach (BuildEventArgs arg in testArgs)
            {
                LogMessagePacket packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, arg));

                ((INodePacketTranslatable)packet).Translate(TranslationHelpers.GetWriteTranslator());
                INodePacket tempPacket = LogMessagePacket.FactoryForDeserialization(TranslationHelpers.GetReadTranslator()) as LogMessagePacket;

                LogMessagePacket deserializedPacket = tempPacket as LogMessagePacket;

                CompareLogMessagePackets(packet, deserializedPacket);
            }

            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", null);
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
            Assert.AreEqual(logEventType, packet.EventType);
            Assert.AreEqual(NodePacketType.LogMessage, packet.Type);
            Assert.IsTrue(Object.ReferenceEquals(buildEvent, packet.NodeBuildEvent.Value.Value), "Expected buildEvent to have the same object reference as packet.BuildEvent");
        }

        /// <summary>
        /// Compares two BuildEventArgs objects for equivalence.
        /// </summary>
        private void CompareNodeBuildEventArgs(KeyValuePair<int, BuildEventArgs> leftTuple, KeyValuePair<int, BuildEventArgs> rightTuple, bool expectInvalidBuildEventContext)
        {
            BuildEventArgs left = leftTuple.Value;
            BuildEventArgs right = rightTuple.Value;

            if (expectInvalidBuildEventContext)
            {
                Assert.AreEqual(BuildEventContext.Invalid, right.BuildEventContext);
            }
            else
            {
                Assert.AreEqual(left.BuildEventContext, right.BuildEventContext);
            }

            Assert.AreEqual(leftTuple.Key, rightTuple.Key);
            Assert.AreEqual(left.HelpKeyword, right.HelpKeyword);
            Assert.AreEqual(left.Message, right.Message);
            Assert.AreEqual(left.SenderName, right.SenderName);
            Assert.AreEqual(left.ThreadId, right.ThreadId);
            Assert.AreEqual(left.Timestamp, right.Timestamp);
        }

        /// <summary>
        /// Compares two LogMessagePacket objects for equivalence.
        /// </summary>
        private void CompareLogMessagePackets(LogMessagePacket left, LogMessagePacket right)
        {
            Assert.AreEqual(left.EventType, right.EventType);
            Assert.AreEqual(left.NodeBuildEvent.Value.Value.GetType(), right.NodeBuildEvent.Value.Value.GetType());

            CompareNodeBuildEventArgs(left.NodeBuildEvent.Value, right.NodeBuildEvent.Value, left.EventType == LoggingEventType.CustomEvent /* expectInvalidBuildEventContext */);

            switch (left.EventType)
            {
                case LoggingEventType.BuildErrorEvent:
                    BuildErrorEventArgs leftError = left.NodeBuildEvent.Value.Value as BuildErrorEventArgs;
                    BuildErrorEventArgs rightError = right.NodeBuildEvent.Value.Value as BuildErrorEventArgs;
                    Assert.IsNotNull(leftError);
                    Assert.IsNotNull(rightError);
                    Assert.AreEqual(leftError.Code, rightError.Code);
                    Assert.AreEqual(leftError.ColumnNumber, rightError.ColumnNumber);
                    Assert.AreEqual(leftError.EndColumnNumber, rightError.EndColumnNumber);
                    Assert.AreEqual(leftError.EndLineNumber, rightError.EndLineNumber);
                    Assert.AreEqual(leftError.File, rightError.File);
                    Assert.AreEqual(leftError.LineNumber, rightError.LineNumber);
                    Assert.AreEqual(leftError.Message, rightError.Message);
                    Assert.AreEqual(leftError.Subcategory, rightError.Subcategory);
                    break;

                case LoggingEventType.BuildFinishedEvent:
                    BuildFinishedEventArgs leftFinished = left.NodeBuildEvent.Value.Value as BuildFinishedEventArgs;
                    BuildFinishedEventArgs rightFinished = right.NodeBuildEvent.Value.Value as BuildFinishedEventArgs;
                    Assert.IsNotNull(leftFinished);
                    Assert.IsNotNull(rightFinished);
                    Assert.AreEqual(leftFinished.Succeeded, rightFinished.Succeeded);
                    break;

                case LoggingEventType.BuildMessageEvent:
                    BuildMessageEventArgs leftMessage = left.NodeBuildEvent.Value.Value as BuildMessageEventArgs;
                    BuildMessageEventArgs rightMessage = right.NodeBuildEvent.Value.Value as BuildMessageEventArgs;
                    Assert.IsNotNull(leftMessage);
                    Assert.IsNotNull(rightMessage);
                    Assert.AreEqual(leftMessage.Importance, rightMessage.Importance);
                    break;

                case LoggingEventType.BuildStartedEvent:
                    BuildStartedEventArgs leftBuildStart = left.NodeBuildEvent.Value.Value as BuildStartedEventArgs;
                    BuildStartedEventArgs rightBuildStart = right.NodeBuildEvent.Value.Value as BuildStartedEventArgs;
                    Assert.IsNotNull(leftBuildStart);
                    Assert.IsNotNull(rightBuildStart);
                    break;

                case LoggingEventType.BuildWarningEvent:
                    BuildWarningEventArgs leftBuildWarn = left.NodeBuildEvent.Value.Value as BuildWarningEventArgs;
                    BuildWarningEventArgs rightBuildWarn = right.NodeBuildEvent.Value.Value as BuildWarningEventArgs;
                    Assert.IsNotNull(leftBuildWarn);
                    Assert.IsNotNull(rightBuildWarn);
                    Assert.AreEqual(leftBuildWarn.Code, rightBuildWarn.Code);
                    Assert.AreEqual(leftBuildWarn.ColumnNumber, rightBuildWarn.ColumnNumber);
                    Assert.AreEqual(leftBuildWarn.EndColumnNumber, rightBuildWarn.EndColumnNumber);
                    Assert.AreEqual(leftBuildWarn.EndLineNumber, rightBuildWarn.EndLineNumber);
                    Assert.AreEqual(leftBuildWarn.File, rightBuildWarn.File);
                    Assert.AreEqual(leftBuildWarn.LineNumber, rightBuildWarn.LineNumber);
                    Assert.AreEqual(leftBuildWarn.Subcategory, rightBuildWarn.Subcategory);
                    break;

                case LoggingEventType.CustomEvent:
                    ExternalProjectStartedEventArgs leftCustom = left.NodeBuildEvent.Value.Value as ExternalProjectStartedEventArgs;
                    ExternalProjectStartedEventArgs rightCustom = right.NodeBuildEvent.Value.Value as ExternalProjectStartedEventArgs;
                    Assert.IsNotNull(leftCustom);
                    Assert.IsNotNull(rightCustom);
                    Assert.AreEqual(leftCustom.ProjectFile, rightCustom.ProjectFile);
                    Assert.AreEqual(leftCustom.TargetNames, rightCustom.TargetNames);
                    break;

                case LoggingEventType.ProjectFinishedEvent:
                    ProjectFinishedEventArgs leftProjectFinished = left.NodeBuildEvent.Value.Value as ProjectFinishedEventArgs;
                    ProjectFinishedEventArgs rightProjectFinished = right.NodeBuildEvent.Value.Value as ProjectFinishedEventArgs;
                    Assert.IsNotNull(leftProjectFinished);
                    Assert.IsNotNull(rightProjectFinished);
                    Assert.AreEqual(leftProjectFinished.ProjectFile, rightProjectFinished.ProjectFile);
                    Assert.AreEqual(leftProjectFinished.Succeeded, rightProjectFinished.Succeeded);
                    break;

                case LoggingEventType.ProjectStartedEvent:
                    ProjectStartedEventArgs leftProjectStarted = left.NodeBuildEvent.Value.Value as ProjectStartedEventArgs;
                    ProjectStartedEventArgs rightProjectStarted = right.NodeBuildEvent.Value.Value as ProjectStartedEventArgs;
                    Assert.IsNotNull(leftProjectStarted);
                    Assert.IsNotNull(rightProjectStarted);
                    Assert.AreEqual(leftProjectStarted.ParentProjectBuildEventContext, rightProjectStarted.ParentProjectBuildEventContext);
                    Assert.AreEqual(leftProjectStarted.ProjectFile, rightProjectStarted.ProjectFile);
                    Assert.AreEqual(leftProjectStarted.ProjectId, rightProjectStarted.ProjectId);
                    Assert.AreEqual(leftProjectStarted.TargetNames, rightProjectStarted.TargetNames);

                    // UNDONE: (Serialization.) We don't actually serialize the items at this time.
                    // Assert.AreEqual(leftProjectStarted.Items, rightProjectStarted.Items);
                    // UNDONE: (Serialization.) We don't actually serialize properties at this time.
                    // Assert.AreEqual(leftProjectStarted.Properties, rightProjectStarted.Properties);
                    break;

                case LoggingEventType.TargetFinishedEvent:
                    TargetFinishedEventArgs leftTargetFinished = left.NodeBuildEvent.Value.Value as TargetFinishedEventArgs;
                    TargetFinishedEventArgs rightTargetFinished = right.NodeBuildEvent.Value.Value as TargetFinishedEventArgs;
                    Assert.IsNotNull(leftTargetFinished);
                    Assert.IsNotNull(rightTargetFinished);
                    Assert.AreEqual(leftTargetFinished.ProjectFile, rightTargetFinished.ProjectFile);
                    Assert.AreEqual(leftTargetFinished.Succeeded, rightTargetFinished.Succeeded);
                    Assert.AreEqual(leftTargetFinished.TargetFile, rightTargetFinished.TargetFile);
                    Assert.AreEqual(leftTargetFinished.TargetName, rightTargetFinished.TargetName);
                    break;

                case LoggingEventType.TargetStartedEvent:
                    TargetStartedEventArgs leftTargetStarted = left.NodeBuildEvent.Value.Value as TargetStartedEventArgs;
                    TargetStartedEventArgs rightTargetStarted = right.NodeBuildEvent.Value.Value as TargetStartedEventArgs;
                    Assert.IsNotNull(leftTargetStarted);
                    Assert.IsNotNull(rightTargetStarted);
                    Assert.AreEqual(leftTargetStarted.ProjectFile, rightTargetStarted.ProjectFile);
                    Assert.AreEqual(leftTargetStarted.TargetFile, rightTargetStarted.TargetFile);
                    Assert.AreEqual(leftTargetStarted.TargetName, rightTargetStarted.TargetName);
                    break;

                case LoggingEventType.TaskCommandLineEvent:
                    TaskCommandLineEventArgs leftCommand = left.NodeBuildEvent.Value.Value as TaskCommandLineEventArgs;
                    TaskCommandLineEventArgs rightCommand = right.NodeBuildEvent.Value.Value as TaskCommandLineEventArgs;
                    Assert.IsNotNull(leftCommand);
                    Assert.IsNotNull(rightCommand);
                    Assert.AreEqual(leftCommand.CommandLine, rightCommand.CommandLine);
                    Assert.AreEqual(leftCommand.Importance, rightCommand.Importance);
                    Assert.AreEqual(leftCommand.TaskName, rightCommand.TaskName);
                    break;

                case LoggingEventType.TaskFinishedEvent:
                    TaskFinishedEventArgs leftTaskFinished = left.NodeBuildEvent.Value.Value as TaskFinishedEventArgs;
                    TaskFinishedEventArgs rightTaskFinished = right.NodeBuildEvent.Value.Value as TaskFinishedEventArgs;
                    Assert.IsNotNull(leftTaskFinished);
                    Assert.IsNotNull(rightTaskFinished);
                    Assert.AreEqual(leftTaskFinished.ProjectFile, rightTaskFinished.ProjectFile);
                    Assert.AreEqual(leftTaskFinished.Succeeded, rightTaskFinished.Succeeded);
                    Assert.AreEqual(leftTaskFinished.TaskFile, rightTaskFinished.TaskFile);
                    Assert.AreEqual(leftTaskFinished.TaskName, rightTaskFinished.TaskName);
                    break;

                case LoggingEventType.TaskStartedEvent:
                    TaskStartedEventArgs leftTaskStarted = left.NodeBuildEvent.Value.Value as TaskStartedEventArgs;
                    TaskStartedEventArgs rightTaskStarted = right.NodeBuildEvent.Value.Value as TaskStartedEventArgs;
                    Assert.IsNotNull(leftTaskStarted);
                    Assert.IsNotNull(rightTaskStarted);
                    Assert.AreEqual(leftTaskStarted.ProjectFile, rightTaskStarted.ProjectFile);
                    Assert.AreEqual(leftTaskStarted.TaskFile, rightTaskStarted.TaskFile);
                    Assert.AreEqual(leftTaskStarted.TaskName, rightTaskStarted.TaskName);
                    break;

                default:
                    Assert.Fail("Unexpected logging event type {0}", left.EventType);
                    break;
            }
        }

        #endregion
    }
}