// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        private BuildFinishedEventArgs s_buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
        private BuildStartedEventArgs s_buildStarted = new BuildStartedEventArgs("Message", "Help");
        private BuildMessageEventArgs s_lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
        private BuildMessageEventArgs s_normalMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);
        private BuildMessageEventArgs s_highMessage = new BuildMessageEventArgs("Message3", "help", "sender", MessageImportance.High);
        private TaskStartedEventArgs s_taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
        private TaskFinishedEventArgs s_taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
        private TaskCommandLineEventArgs s_commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
        private BuildWarningEventArgs s_warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private BuildErrorEventArgs s_error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private TargetStartedEventArgs s_targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private TargetFinishedEventArgs s_targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private ProjectStartedEventArgs s_projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private ProjectFinishedEventArgs s_projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private ExternalProjectStartedEventArgs s_externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

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
            s_error.BuildEventContext = context;
            s_warning.BuildEventContext = context;
            s_targetStarted.BuildEventContext = context;
            s_targetFinished.BuildEventContext = context;
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
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Minimal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(5, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(11, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));
            Assert.True(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Detailed;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(14, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));
            Assert.True(logger.forwardedEvents.Contains(s_lowMessage));
            Assert.True(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(s_taskFinished));
            Assert.True(logger.forwardedEvents.Contains(s_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Diagnostic;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.Equal(15, logger.forwardedEvents.Count);
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));
            Assert.True(logger.forwardedEvents.Contains(s_lowMessage));
            Assert.True(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(s_taskFinished));
            Assert.True(logger.forwardedEvents.Contains(s_externalStartedEvent));
            Assert.True(logger.forwardedEvents.Contains(s_commandLine));
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
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.True(logger.forwardedEvents.Contains(s_taskFinished));
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
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));
            Assert.True(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_commandLine));
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
            Assert.True(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.True(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.True(logger.forwardedEvents.Contains(s_error));
            Assert.True(logger.forwardedEvents.Contains(s_warning));
            Assert.True(logger.forwardedEvents.Contains(s_highMessage));
            Assert.True(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.True(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.True(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.True(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.True(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.True(logger.forwardedEvents.Contains(s_commandLine));
        }

        private void RaiseEvents(EventSourceSink source)
        {
            source.Consume(s_buildStarted);
            source.Consume(s_projectStarted);
            source.Consume(s_targetStarted);
            source.Consume(s_taskStarted);
            source.Consume(s_lowMessage);
            source.Consume(s_normalMessage);
            source.Consume(s_highMessage);
            source.Consume(s_commandLine);
            source.Consume(s_externalStartedEvent);
            source.Consume(s_warning);
            source.Consume(s_error);
            source.Consume(s_taskFinished);
            source.Consume(s_targetFinished);
            source.Consume(s_projectFinished);
            source.Consume(s_buildFinished);
        }
    }
}
