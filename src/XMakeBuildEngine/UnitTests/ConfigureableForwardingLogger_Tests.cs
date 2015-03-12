// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ConfigureableForwardingLogger_Tests
    {
        private static BuildFinishedEventArgs s_buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
        private static BuildStartedEventArgs s_buildStarted = new BuildStartedEventArgs("Message", "Help");
        private static BuildMessageEventArgs s_lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
        private static BuildMessageEventArgs s_normalMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);
        private static BuildMessageEventArgs s_highMessage = new BuildMessageEventArgs("Message3", "help", "sender", MessageImportance.High);
        private static TaskStartedEventArgs s_taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
        private static TaskFinishedEventArgs s_taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
        private static TaskCommandLineEventArgs s_commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
        private static BuildWarningEventArgs s_warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private static BuildErrorEventArgs s_error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private static TargetStartedEventArgs s_targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private static TargetFinishedEventArgs s_targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private static ProjectStartedEventArgs s_projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private static ProjectFinishedEventArgs s_projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private static ExternalProjectStartedEventArgs s_externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

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

        [ClassInitialize]
        public static void FixtureSetup(TestContext testContext)
        {
            BuildEventContext context = new BuildEventContext(1, 2, 3, 4);
            s_error.BuildEventContext = context;
            s_warning.BuildEventContext = context;
            s_targetStarted.BuildEventContext = context;
            s_targetFinished.BuildEventContext = context;
        }

        [TestMethod]
        public void ForwardingLoggingEventsBasedOnVerbosity()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Parameters = "BUILDSTARTEDEVENT";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 1);

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Quiet;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 4);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Minimal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 5);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 11);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Detailed;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 14);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_lowMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Diagnostic;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 15);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_lowMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_externalStartedEvent));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_commandLine));
        }

        [TestMethod]
        public void ForwardingLoggingPerformanceSummary()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Parameters = "PERFORMANCESUMMARY";
            logger.Verbosity = LoggerVerbosity.Quiet;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 10);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_taskFinished));
        }

        [TestMethod]
        public void ForwardingLoggingNoSummary()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Parameters = "NOSUMMARY";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 11);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_commandLine));
        }

        [TestMethod]
        public void ForwardingLoggingShowCommandLine()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Parameters = "SHOWCOMMANDLINE";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 11);
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_buildFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_error));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(s_commandLine));
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
