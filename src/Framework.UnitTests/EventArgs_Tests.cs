// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable disable

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
        internal sealed class GenericBuildEventArgs : BuildEventArgs
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
