// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for ForwardingTerminalLogger message handling.
    /// Regression test for issue #14345: coordinator messages with null context should be forwarded.
    /// </summary>
    public class ForwardingTerminalLogger_Tests
    {
        private sealed class MockEventRedirector : IEventRedirector
        {
            public List<BuildEventArgs> ForwardedEvents { get; } = [];

            public void ForwardEvent(BuildEventArgs buildEvent)
            {
                ForwardedEvents.Add(buildEvent);
            }
        }

        private sealed class MockEventSource : IEventSource
        {
            public event BuildMessageEventHandler? MessageRaised;

            public void RaiseMessage(BuildMessageEventArgs args)
            {
                MessageRaised?.Invoke(this, args);
            }

            public event BuildErrorEventHandler? ErrorRaised { add { } remove { } }
            public event BuildWarningEventHandler? WarningRaised { add { } remove { } }
            public event BuildStartedEventHandler? BuildStarted { add { } remove { } }
            public event BuildFinishedEventHandler? BuildFinished { add { } remove { } }
            public event ProjectStartedEventHandler? ProjectStarted { add { } remove { } }
            public event ProjectFinishedEventHandler? ProjectFinished { add { } remove { } }
            public event TargetStartedEventHandler? TargetStarted { add { } remove { } }
            public event TargetFinishedEventHandler? TargetFinished { add { } remove { } }
            public event TaskStartedEventHandler? TaskStarted { add { } remove { } }
            public event TaskFinishedEventHandler? TaskFinished { add { } remove { } }
            public event BuildStatusEventHandler? StatusEventRaised { add { } remove { } }
            public event CustomBuildEventHandler? CustomEventRaised { add { } remove { } }
            public event AnyEventHandler? AnyEventRaised { add { } remove { } }
        }

        [Fact]
        public void MessageRaised_WithNullContextAndHighImportance_IsForwarded()
        {
            // Arrange
            var mockEventRedirector = new MockEventRedirector();
            var forwardingLogger = new ForwardingTerminalLogger
            {
                BuildEventRedirector = mockEventRedirector,
                Verbosity = LoggerVerbosity.Normal
            };

            var mockEventSource = new MockEventSource();
            forwardingLogger.Initialize(mockEventSource);

            // Act: Raise a message with null context and HIGH importance (coordinator message)
            var coordinatorMessage = new BuildMessageEventArgs(
                "Waiting for coordinator to grant build resources...",
                null,
                "Coordinator",
                MessageImportance.High,
                DateTime.Now)
            {
                BuildEventContext = null
            };

            mockEventSource.RaiseMessage(coordinatorMessage);

            // Assert: Message should be forwarded
            mockEventRedirector.ForwardedEvents.ShouldHaveSingleItem();
            mockEventRedirector.ForwardedEvents[0].ShouldBe(coordinatorMessage);
        }

        [Fact]
        public void MessageRaised_WithNullContextAndNormalImportance_IsNotForwarded()
        {
            // Arrange
            var mockEventRedirector = new MockEventRedirector();
            var forwardingLogger = new ForwardingTerminalLogger
            {
                BuildEventRedirector = mockEventRedirector,
                Verbosity = LoggerVerbosity.Normal
            };

            var mockEventSource = new MockEventSource();
            forwardingLogger.Initialize(mockEventSource);

            // Act: Raise a message with null context and NORMAL importance
            var normalMessage = new BuildMessageEventArgs(
                "This is a normal importance global message",
                null,
                "Test",
                MessageImportance.Normal,
                DateTime.Now)
            {
                BuildEventContext = null
            };

            mockEventSource.RaiseMessage(normalMessage);

            // Assert: Message should NOT be forwarded (null context, not high importance)
            mockEventRedirector.ForwardedEvents.ShouldBeEmpty();
        }

        [Fact]
        public void MessageRaised_WithValidContextAndHighImportance_IsForwarded()
        {
            // Arrange
            var mockEventRedirector = new MockEventRedirector();
            var forwardingLogger = new ForwardingTerminalLogger
            {
                BuildEventRedirector = mockEventRedirector,
                Verbosity = LoggerVerbosity.Normal
            };

            var mockEventSource = new MockEventSource();
            forwardingLogger.Initialize(mockEventSource);

            var projectContext = new BuildEventContext(0, 1, BuildEventContext.InvalidEvaluationId, 1, 0, 0);

            // Act: Raise a message with valid context and HIGH importance
            var projectMessage = new BuildMessageEventArgs(
                "project.csproj -> bin/output.dll",
                null,
                "MSBuild",
                MessageImportance.High,
                DateTime.Now)
            {
                BuildEventContext = projectContext
            };

            mockEventSource.RaiseMessage(projectMessage);

            // Assert: Message should be forwarded (normal project message)
            mockEventRedirector.ForwardedEvents.ShouldHaveSingleItem();
            mockEventRedirector.ForwardedEvents[0].ShouldBe(projectMessage);
        }

        [Fact]
        public void MessageRaised_WithQuietVerbosity_GlobalMessageIsNotForwarded()
        {
            // Arrange
            var mockEventRedirector = new MockEventRedirector();
            var forwardingLogger = new ForwardingTerminalLogger
            {
                BuildEventRedirector = mockEventRedirector,
                Verbosity = LoggerVerbosity.Quiet
            };

            var mockEventSource = new MockEventSource();
            forwardingLogger.Initialize(mockEventSource);

            // Act: Raise a message with null context and HIGH importance in quiet mode
            var coordinatorMessage = new BuildMessageEventArgs(
                "Waiting for coordinator...",
                null,
                "Coordinator",
                MessageImportance.High,
                DateTime.Now)
            {
                BuildEventContext = null
            };

            mockEventSource.RaiseMessage(coordinatorMessage);

            // Assert: Even HIGH importance messages should be filtered in quiet mode
            mockEventRedirector.ForwardedEvents.ShouldBeEmpty();
        }

        [Fact]
        public void MessageRaised_MultipleCoordinatorMessages_AreAllForwarded()
        {
            // Arrange
            var mockEventRedirector = new MockEventRedirector();
            var forwardingLogger = new ForwardingTerminalLogger
            {
                BuildEventRedirector = mockEventRedirector,
                Verbosity = LoggerVerbosity.Normal
            };

            var mockEventSource = new MockEventSource();
            forwardingLogger.Initialize(mockEventSource);

            // Act: Raise multiple coordinator messages
            var waitMessage = new BuildMessageEventArgs(
                "Waiting for coordinator to grant build resources...",
                null,
                "Coordinator",
                MessageImportance.High,
                DateTime.Now)
            {
                BuildEventContext = null
            };

            var grantMessage = new BuildMessageEventArgs(
                "Coordinator granted 4 node(s) for this build.",
                null,
                "Coordinator",
                MessageImportance.High,
                DateTime.Now)
            {
                BuildEventContext = null
            };

            mockEventSource.RaiseMessage(waitMessage);
            mockEventSource.RaiseMessage(grantMessage);

            // Assert: Both messages should be forwarded
            mockEventRedirector.ForwardedEvents.Count.ShouldBe(2);
            mockEventRedirector.ForwardedEvents[0].ShouldBe(waitMessage);
            mockEventRedirector.ForwardedEvents[1].ShouldBe(grantMessage);
        }
    }
}
