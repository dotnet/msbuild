// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit test the base class BuildEventArgs
    /// </summary>
    public class EventArgs_Tests
    {
        #region BaseClass Equals Tests

        /// <summary>
        /// Base instance of a BuildEventArgs some default data, this is used during the tests
        /// to verify the equals operators.
        /// </summary>
        private static GenericBuildEventArgs s_baseGenericEvent = null;

        /// <summary>
        /// Setup the test, this method is run ONCE for the entire test fixture
        /// </summary>
        public EventArgs_Tests()
        {
            s_baseGenericEvent = new GenericBuildEventArgs("Message", "HelpKeyword", "senderName");
            s_baseGenericEvent.BuildEventContext = new BuildEventContext(9, 8, 7, 6);
        }

        /// <summary>
        /// Trivially exercise getHashCode.
        /// </summary>
        [Fact]
        public void TestGetHashCode()
        {
            s_baseGenericEvent.GetHashCode();
        }

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            GenericBuildEventArgs genericEventTest = new GenericBuildEventArgs();
        }
        #endregion

        /// <summary>
        /// Verify a whidbey project started event can be deserialized, the whidbey event is stored in a serialized base64 string.
        /// </summary>
        [Fact]
        public void TestDeserialization()
        {
            string base64OldProjectStarted = "AAEAAAD/////AQAAAAAAAAAMAgAAAFxNaWNyb3NvZnQuQnVpbGQuRnJhbWV3b3JrLCBWZXJzaW9uPTIuMC4wLjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49YjAzZjVmN2YxMWQ1MGEzYQUBAAAAMU1pY3Jvc29mdC5CdWlsZC5GcmFtZXdvcmsuUHJvamVjdFN0YXJ0ZWRFdmVudEFyZ3MHAAAAC3Byb2plY3RGaWxlC3RhcmdldE5hbWVzFkJ1aWxkRXZlbnRBcmdzK21lc3NhZ2UaQnVpbGRFdmVudEFyZ3MraGVscEtleXdvcmQZQnVpbGRFdmVudEFyZ3Mrc2VuZGVyTmFtZRhCdWlsZEV2ZW50QXJncyt0aW1lc3RhbXAXQnVpbGRFdmVudEFyZ3MrdGhyZWFkSWQBAQEBAQAADQgCAAAABgMAAAALcHJvamVjdEZpbGUGBAAAAAt0YXJnZXROYW1lcwYFAAAAB21lc3NhZ2UGBgAAAAtoZWxwS2V5d29yZAYHAAAAB01TQnVpbGQBl5vjTYvIiAsAAAAL";
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            byte[] binaryObject = Convert.FromBase64String(base64OldProjectStarted);
            ms.Write(binaryObject, 0, binaryObject.Length);
            ms.Position = 0;
            ProjectStartedEventArgs pse = (ProjectStartedEventArgs)bf.Deserialize(ms);
            pse.Message.ShouldBe("message", StringCompareShould.IgnoreCase);
            pse.ProjectFile.ShouldBe("projectFile", StringCompareShould.IgnoreCase);
            pse.ProjectId.ShouldBe(-1);
            pse.TargetNames.ShouldBe("targetNames", StringCompareShould.IgnoreCase);
            pse.BuildEventContext.ShouldBe(BuildEventContext.Invalid);
            pse.ParentProjectBuildEventContext.ShouldBe(BuildEventContext.Invalid);
        }

        /// <summary>
        /// Verify the BuildEventContext is exercised
        /// </summary>
        [Fact]
        public void ExerciseBuildEventContext()
        {
            BuildEventContext parentBuildEventContext = new BuildEventContext(0, 0, 0, 0, 0, 0, 0);

            BuildEventContext currentBuildEventContext = new BuildEventContext(0, 1, 2, 3, 4, 5, 6);

            BuildEventContext currentBuildEventContextSubmission = new BuildEventContext(1, 0, 0, 0, 0, 0, 0);
            BuildEventContext currentBuildEventContextNode = new BuildEventContext(0, 1, 0, 0, 0, 0, 0);
            BuildEventContext currentBuildEventContextEvaluation = new BuildEventContext(0, 0, 1, 0, 0, 0, 0);
            BuildEventContext currentBuildEventContextProjectInstance = new BuildEventContext(0, 0, 0, 1, 0, 0, 0);
            BuildEventContext currentBuildEventProjectContext = new BuildEventContext(0, 0, 0, 0, 1, 0, 0);
            BuildEventContext currentBuildEventContextTarget = new BuildEventContext(0, 0, 0, 0, 0, 1, 0);
            BuildEventContext currentBuildEventContextTask = new BuildEventContext(0, 0, 0, 0, 0, 0, 1);
            BuildEventContext allDifferent = new BuildEventContext(1, 1, 1, 1, 1, 1, 1);
            BuildEventContext allSame = new BuildEventContext(0, 0, 0, 0, 0, 0, 0);

            ProjectStartedEventArgs startedEvent = new ProjectStartedEventArgs(-1, "Message", "HELP", "File", "Targets", null, null, parentBuildEventContext);
            startedEvent.BuildEventContext = currentBuildEventContext;

            // submissison ID does not partake into equality
            currentBuildEventContextSubmission.GetHashCode().ShouldBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContext.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContextNode.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContextEvaluation.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContextProjectInstance.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventProjectContext.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContextTarget.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            currentBuildEventContextTask.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            allDifferent.GetHashCode().ShouldNotBe(parentBuildEventContext.GetHashCode());
            parentBuildEventContext.GetHashCode().ShouldBe(allSame.GetHashCode());

            // submissison ID does not partake into equality
            currentBuildEventContextSubmission.ShouldBe(parentBuildEventContext);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContext);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContextNode);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContextEvaluation);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContextProjectInstance);
            parentBuildEventContext.ShouldNotBe(currentBuildEventProjectContext);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContextTarget);
            parentBuildEventContext.ShouldNotBe(currentBuildEventContextTask);
            parentBuildEventContext.ShouldNotBe(allDifferent);
            parentBuildEventContext.ShouldBe(allSame);

            currentBuildEventContext.ShouldBe(currentBuildEventContext);
            parentBuildEventContext.ShouldNotBeNull();
            currentBuildEventContext.ShouldNotBe(new object());

            startedEvent.BuildEventContext.ShouldNotBeNull();

            startedEvent.ParentProjectBuildEventContext.SubmissionId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.NodeId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.EvaluationId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.ProjectInstanceId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.ProjectContextId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.TargetId.ShouldBe(0);
            startedEvent.ParentProjectBuildEventContext.TaskId.ShouldBe(0);

            startedEvent.BuildEventContext.SubmissionId.ShouldBe(0);
            startedEvent.BuildEventContext.NodeId.ShouldBe(1);
            startedEvent.BuildEventContext.EvaluationId.ShouldBe(2);
            startedEvent.BuildEventContext.ProjectInstanceId.ShouldBe(3);
            startedEvent.BuildEventContext.ProjectContextId.ShouldBe(4);
            startedEvent.BuildEventContext.TargetId.ShouldBe(5);
            startedEvent.BuildEventContext.TaskId.ShouldBe(6);
        }

        /// <summary>
        /// A generic buildEvent arg to test the equals method
        /// </summary>
        internal class GenericBuildEventArgs : BuildEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public GenericBuildEventArgs()
                : base()
            {
            }

            /// <summary>
            /// This constructor allows all event data to be initialized
            /// </summary>
            /// <param name="message">text message</param>
            /// <param name="helpKeyword">help keyword </param>
            /// <param name="senderName">name of event sender</param>
            public GenericBuildEventArgs(string message, string helpKeyword, string senderName)
                : base(message, helpKeyword, senderName)
            {
            }

            /// <summary>
            /// This constructor allows all data including timeStamps to be initialized
            /// </summary>
            /// <param name="message">text message</param>
            /// <param name="helpKeyword">help keyword </param>
            /// <param name="senderName">name of event sender</param>
            /// <param name="eventTimeStamp">TimeStamp of when the event was created</param>
            public GenericBuildEventArgs(string message, string helpKeyword, string senderName, DateTime eventTimeStamp)
                : base(message, helpKeyword, senderName, eventTimeStamp)
            {
            }
        }
    }
}
