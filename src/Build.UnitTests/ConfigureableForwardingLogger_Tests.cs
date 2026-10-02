// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

#nullable disable

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
        private readonly TaskParameterEventArgs _taskParameter = new TaskParameterEventArgs(TaskParameterMessageKind.TaskInput, "ItemName", null, true, DateTime.MinValue);
        private readonly BuildWarningEventArgs _warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private readonly BuildErrorEventArgs _error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
        private readonly TargetStartedEventArgs _targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");
        private readonly TargetFinishedEventArgs _targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);
        private readonly ProjectStartedEventArgs _projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);
        private readonly ProjectFinishedEventArgs _projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true);
        private readonly ExternalProjectStartedEventArgs _externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

        internal sealed class TestForwardingLogger : ConfigurableForwardingLogger
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
                        _taskParameter,
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
                        _taskParameter,
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

        /// <summary>
        /// A central logger describes a finished progress operation as an ordinary message, and it needs
        /// the started event to do it because only that event carries the title. Both are low importance,
        /// so they must be forwarded wherever ordinary messages are rather than only at detailed
        /// verbosity. The updates in between are high volume and follow the ordinary rules.
        /// </summary>
        [Theory]
        [InlineData(LoggerVerbosity.Minimal, false, false)]
        [InlineData(LoggerVerbosity.Normal, true, false)]
        [InlineData(LoggerVerbosity.Detailed, true, true)]
        [InlineData(LoggerVerbosity.Diagnostic, true, true)]
        public void ForwardsTheStartAndEndOfProgressOperations(LoggerVerbosity verbosity, bool expectOperation, bool expectUpdates)
        {
            var context = new BuildEventContext(1, 2, 3, 4);
            var started = new TaskProgressStartedEventArgs(7, "Downloading tools.zip", TaskProgressUnit.Bytes) { BuildEventContext = context };
            var updated = new TaskProgressUpdatedEventArgs(7, 1, 512, 1024, "halfway") { BuildEventContext = context };
            var finished = new TaskProgressFinishedEventArgs(7, 2, TaskProgressOutcome.Completed, 1024, 1024, null) { BuildEventContext = context };

            EventSourceSink source = new EventSourceSink();
            TestForwardingLogger logger = new TestForwardingLogger
            {
                BuildEventRedirector = null,
                Verbosity = verbosity,
            };
            logger.Initialize(source, 4);

            source.Consume(started);
            source.Consume(updated);
            source.Consume(finished);

            logger.ForwardedEvents.Contains(started).ShouldBe(expectOperation);
            logger.ForwardedEvents.Contains(finished).ShouldBe(expectOperation);
            logger.ForwardedEvents.Contains(updated).ShouldBe(expectUpdates);
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
            source.Consume(_taskParameter);
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
