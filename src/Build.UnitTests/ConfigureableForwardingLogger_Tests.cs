// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.BackEnd.Logging;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ConfigureableForwardingLogger_Tests
    {
        private BuildFinishedEventArgs _buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
        private BuildStartedEventArgs _buildStarted = new BuildStartedEventArgs("Message", "Help");
        private BuildMessageEventArgs _lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
        private BuildMessageEventArgs _normalMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);
        private BuildMessageEventArgs _highMessage = new BuildMessageEventArgs("Message3", "help", "sender", MessageImportance.High);
        private TaskStartedEventArgs _taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
        private TaskFinishedEventArgs _taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
        private TaskCommandLineEventArgs _commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
        private BuildWarningEventArgs _warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private BuildErrorEventArgs _error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private TargetStartedEventArgs _targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private TargetFinishedEventArgs _targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private ProjectStartedEventArgs _projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private ProjectFinishedEventArgs _projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private ExternalProjectStartedEventArgs _externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

        internal class TestForwardingLogger : ConfigurableForwardingLogger
        {
            internal TestForwardingLogger()
            {
                forwardedEvents = new List<BuildEventArgs>();
            }
            internal List<BuildEventArgs> forwardedEvents;
            protected override void ForwardToCentralLogger(BuildEventArgs e)
            {
                forwardedEvents.Add(e);
            }
        }

        public ConfigureableForwardingLogger_Tests()
        {
            BuildEventContext context = new BuildEventContext(1, 2, 3, 4);
            _error.BuildEventContext = context;
            _warning.BuildEventContext = context;
            _targetStarted.BuildEventContext = context;
            _targetFinished.BuildEventContext = context;
        }

        [Fact]
        public void ForwardingLoggingEventsBasedOnVerbosity()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Parameters = "BUILDSTARTEDEVENT";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(1, logger.forwardedEvents.Count);

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Quiet;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(4, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Minimal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(5, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(11, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));
            Assert.True(logger.forwardedEvents.Contains(_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Detailed;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(14, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));
            Assert.True(logger.forwardedEvents.Contains(_lowMessage));
            Assert.True(logger.forwardedEvents.Contains(_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(_taskFinished));
            Assert.True(logger.forwardedEvents.Contains(_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Diagnostic;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(15, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));
            Assert.True(logger.forwardedEvents.Contains(_lowMessage));
            Assert.True(logger.forwardedEvents.Contains(_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(_taskFinished));
            Assert.True(logger.forwardedEvents.Contains(_externalStartedEvent));
            Assert.True(logger.forwardedEvents.Contains(_commandLine));
        }

        [Fact]
        public void ForwardingLoggingPerformanceSummary()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Parameters = "PERFORMANCESUMMARY";
            logger.Verbosity = LoggerVerbosity.Quiet;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(10, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(_taskFinished));
        }

        [Fact]
        public void ForwardingLoggingNoSummary()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Parameters = "NOSUMMARY";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(11, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));
            Assert.True(logger.forwardedEvents.Contains(_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_commandLine));
        }

        [Fact]
        public void ForwardingLoggingShowCommandLine()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Parameters = "SHOWCOMMANDLINE";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(11, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(_error));
            Assert.True(logger.forwardedEvents.Contains(_warning));
            Assert.True(logger.forwardedEvents.Contains(_highMessage));
            Assert.True(logger.forwardedEvents.Contains(_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(_commandLine));
        }

        private void RaiseEvents(EventSourceSink source)
        {
            source.Consume(_buildStarted);
            source.Consume(_projectStarted);
            source.Consume(_targetStarted);
            source.Consume(_taskStarted);
            source.Consume(_lowMessage);
            source.Consume(_normalMessage);
            source.Consume(_highMessage);
            source.Consume(_commandLine);
            source.Consume(_externalStartedEvent);
            source.Consume(_warning);
            source.Consume(_error);
            source.Consume(_taskFinished);
            source.Consume(_targetFinished);
            source.Consume(_projectFinished);
            source.Consume(_buildFinished);
        }
    }
}
