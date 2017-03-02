// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ConfigureableForwardingLogger_Tests
    {
        private BuildFinishedEventArgs buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
        private BuildStartedEventArgs buildStarted = new BuildStartedEventArgs("Message", "Help");
        private BuildMessageEventArgs lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
        private BuildMessageEventArgs normalMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);
        private BuildMessageEventArgs highMessage = new BuildMessageEventArgs("Message3", "help", "sender", MessageImportance.High);
        private TaskStartedEventArgs taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
        private TaskFinishedEventArgs taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
        private TaskCommandLineEventArgs commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
        private BuildWarningEventArgs warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private BuildErrorEventArgs error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private TargetStartedEventArgs targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private TargetFinishedEventArgs targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private ProjectStartedEventArgs projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private ProjectFinishedEventArgs projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private ExternalProjectStartedEventArgs externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

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

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            BuildEventContext context = new BuildEventContext(1, 2, 3, 4);
            error.BuildEventContext = context;
            warning.BuildEventContext = context;
            targetStarted.BuildEventContext = context;
            targetFinished.BuildEventContext = context;
        }

        [Test]
        public void ForwardingLoggingEventsBasedOnVerbosity()
        {

            EventSource source = new EventSource();
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
            Assert.IsTrue(logger.forwardedEvents.Count == 2);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Minimal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 3);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 8);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Detailed;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 12);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(lowMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(commandLine));

            logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Diagnostic;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 13);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(lowMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(externalStartedEvent));
            Assert.IsTrue(logger.forwardedEvents.Contains(commandLine));
        }

        [Test]
        public void ForwardingLoggingPerformanceSummary()
        {

            EventSource source = new EventSource();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Parameters = "PERFORMANCESUMMARY";
            logger.Verbosity = LoggerVerbosity.Quiet;
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 8);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(taskFinished));

        }

        [Test]
        public void ForwardingLoggingNoSummary()
        {

            EventSource source = new EventSource();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
             logger.Parameters = "NOSUMMARY";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 8);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));
        }

        [Test]
        public void ForwardingLoggingShowCommandLine()
        {

            EventSource source = new EventSource();
            TestForwardingLogger logger = new TestForwardingLogger();
            logger.BuildEventRedirector = null;
            logger.Verbosity = LoggerVerbosity.Normal;
            logger.Parameters = "SHOWCOMMANDLINE";
            logger.Initialize(source, 4);
            RaiseEvents(source);
            Assert.IsTrue(logger.forwardedEvents.Count == 9);
            Assert.IsTrue(logger.forwardedEvents.Contains(error));
            Assert.IsTrue(logger.forwardedEvents.Contains(warning));
            Assert.IsTrue(logger.forwardedEvents.Contains(highMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(normalMessage));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(projectFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetStarted));
            Assert.IsTrue(logger.forwardedEvents.Contains(targetFinished));
            Assert.IsTrue(logger.forwardedEvents.Contains(commandLine));
        }

        private void RaiseEvents(EventSource source)
        {
            source.RaiseBuildStartedEvent(null, buildStarted);
            source.RaiseProjectStartedEvent(null, projectStarted);
            source.RaiseTargetStartedEvent(null, targetStarted);
            source.RaiseTaskStartedEvent(null, taskStarted);
            source.RaiseMessageEvent(null, lowMessage);
            source.RaiseMessageEvent(null, normalMessage);
            source.RaiseMessageEvent(null, highMessage);
            source.RaiseMessageEvent(null, commandLine);
            source.RaiseCustomEvent(null, externalStartedEvent);
            source.RaiseWarningEvent(null, warning);
            source.RaiseErrorEvent(null, error);
            source.RaiseTaskFinishedEvent(null, taskFinished);
            source.RaiseTargetFinishedEvent(null, targetFinished);
            source.RaiseProjectFinishedEvent(null, projectFinished);
            source.RaiseBuildFinishedEvent(null, buildFinished);
        }

    }
}
