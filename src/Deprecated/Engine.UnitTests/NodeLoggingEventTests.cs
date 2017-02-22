// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.IO;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using System.Collections;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class NodeLoggingEventTests
    {
        [Serializable]
        internal class GenericBuildEventArg : BuildEventArgs
        {
            internal GenericBuildEventArg
        (
            string message,
            string helpKeyword,
            string senderName
        )
                : base(message, helpKeyword, senderName)
            {
            }
        }

        [Serializable]
        internal class GenericCustomBuildEventArg : CustomBuildEventArgs
        {
            public string customField;

            internal GenericCustomBuildEventArg
        (
            string customValue
        )
            {
                this.customField = customValue;
            }
        }

        MemoryStream stream;
        BinaryWriter writer;
        BinaryReader reader;

        [SetUp]
        public void SetUp()
        {
            stream = new MemoryStream();
            writer = new BinaryWriter(stream);
            reader = new BinaryReader(stream);
        }

        [TearDown]
        public void TearDown()
        {
            writer.Close();
            reader = null;
            stream = null;
            writer = null;
        }

        [Test]
        public void TestLoggingEventCustomerSerialization()
        {
            Hashtable loggingTypeCacheWrites = new Hashtable();
            stream.Position = 0;
            BuildEventContext context = new BuildEventContext(1,3,5,7);
            GenericBuildEventArg genericBuildEvent = new GenericBuildEventArg("Message","Help","Sender");
            genericBuildEvent.BuildEventContext = context;
            NodeLoggingEvent genericBuildEventLoggingEvent = new NodeLoggingEvent(genericBuildEvent);
            genericBuildEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            GenericCustomBuildEventArg genericCustomEvent = new GenericCustomBuildEventArg("FooFighter");
            genericCustomEvent.BuildEventContext = context;
            NodeLoggingEvent genericCustomEventLoggingEvent = new NodeLoggingEvent(genericCustomEvent);
            genericCustomEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            BuildErrorEventArgs errorEvent = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            errorEvent.BuildEventContext = context;
            NodeLoggingEvent errorEventLoggingEvent = new NodeLoggingEvent(errorEvent);
            errorEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("Message", "HelpKeyword", "SenderName",MessageImportance.High);
            messageEvent.BuildEventContext = context;
            NodeLoggingEvent messageEventLoggingEvent = new NodeLoggingEvent(messageEvent);
            messageEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            BuildWarningEventArgs warningEvent = new BuildWarningEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            warningEvent.BuildEventContext = context;
            NodeLoggingEvent warningEventLoggingEvent = new NodeLoggingEvent(warningEvent);
            warningEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            ProjectStartedEventArgs projectStartedEvent = new ProjectStartedEventArgs( 8,"Message", "HelpKeyword", "ProjectFile", "TargetNames", null, null, new BuildEventContext(7,8,9,10));
            projectStartedEvent.BuildEventContext = context;
            NodeLoggingEvent projectStartedEventLoggingEvent = new NodeLoggingEvent(projectStartedEvent);
            projectStartedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            ProjectFinishedEventArgs projectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword","ProjectFile",true);
            projectFinishedEvent.BuildEventContext = context;
            NodeLoggingEvent projectFinishedEventLoggingEvent = new NodeLoggingEvent(projectFinishedEvent);
            projectFinishedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            TargetStartedEventArgs targetStartedEvent = new TargetStartedEventArgs("Message", "HelpKeyword", "TargetName", "ProjectFile", "TargetFile");
            targetStartedEvent.BuildEventContext = context;
            NodeLoggingEvent targetStartedEventLoggingEvent = new NodeLoggingEvent(targetStartedEvent);
            targetStartedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            TargetFinishedEventArgs targetFinished = new TargetFinishedEventArgs("Message", "HelpKeyword","TargetName", "ProjectFile", "TargetFile", true);
            targetFinished.BuildEventContext = context;
            NodeLoggingEvent targetFinishedEventLoggingEvent = new NodeLoggingEvent(targetFinished);
            targetFinishedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            TaskStartedEventArgs taskStartedEvent = new TaskStartedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName");
            taskStartedEvent.BuildEventContext = context;
            NodeLoggingEvent taskStartedEventLoggingEvent = new NodeLoggingEvent(taskStartedEvent);
            taskStartedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            TaskFinishedEventArgs taskFinishedEvent = new TaskFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName", true);
            taskFinishedEvent.BuildEventContext = context;
            NodeLoggingEvent taskFinishedEventLoggingEvent = new NodeLoggingEvent(taskFinishedEvent);
            taskFinishedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            BuildFinishedEventArgs buildFinishedEvent = new BuildFinishedEventArgs("Message","Help",true);
            buildFinishedEvent.BuildEventContext = context;
            NodeLoggingEvent buildFinishedEventEventLoggingEvent = new NodeLoggingEvent(buildFinishedEvent);
            buildFinishedEventEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            BuildStartedEventArgs buildStartedEvent = new BuildStartedEventArgs("Message","Help");
            buildStartedEvent.BuildEventContext = context;
            NodeLoggingEvent buildStartedEventLoggingEvent = new NodeLoggingEvent(buildStartedEvent);
            buildStartedEventLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            TaskCommandLineEventArgs commandlineEventArg = new TaskCommandLineEventArgs("CommandLine","TaskName", MessageImportance.High);
            commandlineEventArg.BuildEventContext = context;
            NodeLoggingEvent commandlineEventArgLoggingEvent = new NodeLoggingEvent(commandlineEventArg);
            commandlineEventArgLoggingEvent.WriteToStream(writer, loggingTypeCacheWrites);

            Hashtable loggingTypeCacheReads = new Hashtable();
            long streamWriteEndPosition = stream.Position;
            stream.Position = 0;

            NodeLoggingEvent nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(GenericBuildEventArg));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(GenericCustomBuildEventArg));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(BuildErrorEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(BuildMessageEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(BuildWarningEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(ProjectStartedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(ProjectFinishedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(TargetStartedEventArgs));


            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(TargetFinishedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(TaskStartedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(TaskFinishedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(BuildFinishedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() == typeof(BuildStartedEventArgs));

            nodeLoggingEvent = new NodeLoggingEvent(null);
            nodeLoggingEvent.CreateFromStream(reader, loggingTypeCacheReads);
            Assert.IsTrue(nodeLoggingEvent.BuildEvent.GetType() ==typeof( TaskCommandLineEventArgs));
            
            long streamReadEndPosition = stream.Position;
            Assert.AreEqual(streamWriteEndPosition, streamReadEndPosition, "Expected Read and Write Positions to match");
        }

        /// <summary>
        /// Test the case where a nodeLoggingEvent with loggerId is serialized
        /// </summary>
        [Test]
        public void TestLoggingEventWithIdCustomerSerialization()
        {
            Hashtable loggingTypeCacheWrites = new Hashtable();
            GenericCustomBuildEventArg genericEvent = new GenericCustomBuildEventArg("FooFighter");
            NodeLoggingEventWithLoggerId nodeEvent = new NodeLoggingEventWithLoggerId(genericEvent, 4);
            stream.Position = 0;
            nodeEvent.WriteToStream(writer, loggingTypeCacheWrites);
            long streamWriteEndPosition = stream.Position;

            stream.Position = 0;
            Hashtable loggingTypeCacheReads = new Hashtable();

            NodeLoggingEventWithLoggerId nodeEvent2 = new NodeLoggingEventWithLoggerId(null,-1);
            nodeEvent2.CreateFromStream(reader, loggingTypeCacheReads);
            long streamReadEndPosition = stream.Position;
            Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream end positions should be equal");

            // Only checking loggingId because the serialization of the events are checked in the above test
            Assert.AreEqual(nodeEvent.LoggerId, nodeEvent2.LoggerId, "Expected LoggerId to match");
            Assert.IsTrue(string.Compare(((GenericCustomBuildEventArg)nodeEvent.BuildEvent).customField, ((GenericCustomBuildEventArg)nodeEvent2.BuildEvent).customField, StringComparison.OrdinalIgnoreCase) == 0);
        }

    }
}
