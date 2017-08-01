// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.BackEnd.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ConfigureableForwardingLogger_Tests
    {
        private readonly BuildFinishedEventArgs _buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);
        private readonly BuildStartedEventArgs _buildStarted = new BuildStartedEventArgs("Message", "Help");
        private readonly BuildMessageEventArgs _lowMessage = new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low);
        private readonly BuildMessageEventArgs _normalMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);
        private readonly BuildMessageEventArgs _highMessage = new BuildMessageEventArgs("Message3", "help", "sender", MessageImportance.High);
        private readonly TaskStartedEventArgs _taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");
        private readonly TaskFinishedEventArgs _taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);
        private readonly TaskCommandLineEventArgs _commandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);
        private readonly BuildWarningEventArgs _warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private readonly BuildErrorEventArgs _error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private readonly TargetStartedEventArgs _targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private readonly TargetFinishedEventArgs _targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private readonly ProjectStartedEventArgs _projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private readonly ProjectFinishedEventArgs _projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private readonly ExternalProjectStartedEventArgs _externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

        internal class TestForwardingLogger : ConfigurableForwardingLogger
        {
            internal TestForwardingLogger()
            {
                ForwardedEvents = new List<BuildEventArgs>();
            }

            internal List<BuildEventArgs> ForwardedEvents;

            protected override void ForwardToCentralLogger(BuildEventArgs e)
            {
                ForwardedEvents.Add(e);
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

        [InlineData(null)]
        [InlineData(LoggerVerbosity.Quiet)]
        [InlineData(LoggerVerbosity.Minimal)]
        [InlineData(LoggerVerbosity.Normal)]
        [InlineData(LoggerVerbosity.Detailed)]
        [InlineData(LoggerVerbosity.Diagnostic)]
        [Theory]
        public void ForwardingLoggingEventsBasedOnVerbosity(LoggerVerbosity? loggerVerbosity)
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger
            {
                BuildEventRedirector = null
            };

            if (loggerVerbosity.HasValue)
            {
                logger.Verbosity = loggerVerbosity.Value;
            }
            else
            {
                // Testing a single event when verbosity is not set
                logger.Parameters = "BUILDSTARTEDEVENT";
            }

            logger.Initialize(source, 4);
            RaiseEvents(source);

            switch (loggerVerbosity)
            {
                case null:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[] { _buildStarted });
                    break;
                case LoggerVerbosity.Quiet:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
                    {
                        _buildStarted,
                        _warning,
                        _error,
                        _buildFinished
                    });
                    break;
                case LoggerVerbosity.Minimal:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
                    {
                        _buildStarted,
                        _highMessage,
                        _warning,
                        _error,
                        _buildFinished
                    });
                    break;
                case LoggerVerbosity.Normal:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
                    {
                        _buildStarted,
                        _projectStarted,
                        _targetStarted,
                        _normalMessage,
                        _highMessage,
                        _commandLine,
                        _warning,
                        _error,
                        _targetFinished,
                        _projectFinished,
                        _buildFinished,
                    });
                    break;
                case LoggerVerbosity.Detailed:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
                    {
                        _buildStarted,
                        _projectStarted,
                        _targetStarted,
                        _taskStarted,
                        _lowMessage,
                        _normalMessage,
                        _highMessage,
                        _commandLine,
                        _warning,
                        _error,
                        _taskFinished,
                        _targetFinished,
                        _projectFinished,
                        _buildFinished,
                    });
                    break;
                case LoggerVerbosity.Diagnostic:
                    logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
                    {
                        _buildStarted,
                        _projectStarted,
                        _targetStarted,
                        _taskStarted,
                        _lowMessage,
                        _normalMessage,
                        _highMessage,
                        _commandLine,
                        _externalStartedEvent,
                        _warning,
                        _error,
                        _taskFinished,
                        _targetFinished,
                        _projectFinished,
                        _buildFinished,
                    });
                    break;
            }
        }

        [Fact]
        public void ForwardingLoggingPerformanceSummary()
        {
            EventSourceSink source = new EventSourceSink();

            TestForwardingLogger logger = new TestForwardingLogger
            {
                BuildEventRedirector = null,
                Parameters = "PERFORMANCESUMMARY",
                Verbosity = LoggerVerbosity.Quiet
            };

            logger.Initialize(source, 4);

            RaiseEvents(source);

            logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
            {
                _buildStarted,
                _projectStarted,
                _targetStarted,
                _taskStarted,
                _warning,
                _error,
                _taskFinished,
                _targetFinished,
                _projectFinished,
                _buildFinished,
            });
        }

        [Fact]
        public void ForwardingLoggingNoSummary()
        {
            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger
            {
                BuildEventRedirector = null,
                Verbosity = LoggerVerbosity.Normal,
                Parameters = "NOSUMMARY"
            };

            logger.Initialize(source, 4);

            RaiseEvents(source);

            logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
            {
                _buildStarted,
                _projectStarted,
                _targetStarted,
                _normalMessage,
                _highMessage,
                _commandLine,
                _warning,
                _error,
                _targetFinished,
                _projectFinished,
                _buildFinished,
            });
        }

        [Fact]
        public void ForwardingLoggingShowCommandLine()
        {
            EventSourceSink source = new EventSourceSink();

            TestForwardingLogger logger = new TestForwardingLogger
            {
                BuildEventRedirector = null,
                Verbosity = LoggerVerbosity.Normal,
                Parameters = "SHOWCOMMANDLINE"
            };

            logger.Initialize(source, 4);

            RaiseEvents(source);

            logger.ForwardedEvents.ShouldBe(new BuildEventArgs[]
            {
                _buildStarted,
                _projectStarted,
                _targetStarted,
                _normalMessage,
                _highMessage,
                _commandLine,
                _warning,
                _error,
                _targetFinished,
                _projectFinished,
                _buildFinished,
            });
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
