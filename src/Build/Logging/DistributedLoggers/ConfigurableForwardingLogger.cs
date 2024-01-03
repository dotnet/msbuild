// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Logger that forwards events to a central logger (e.g ConsoleLogger)
    /// residing on the parent node.
    /// </summary>
    public class ConfigurableForwardingLogger : IForwardingLogger
    {
        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConfigurableForwardingLogger()
        { }
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the level of detail to show in the event log.
        /// </summary>
        /// <value>Verbosity level.</value>
        public LoggerVerbosity Verbosity
        {
            get { return _verbosity; }
            set { _verbosity = value; }
        }

        /// <summary>
        /// The console logger takes a single parameter to suppress the output of the errors
        /// and warnings summary at the end of a build.
        /// </summary>
        /// <value>null</value>
        public string Parameters
        {
            get { return _loggerParameters; }
            set { _loggerParameters = value; }
        }

        /// <summary>
        /// This property is set by the build engine to allow a node loggers to forward messages to the
        /// central logger
        /// </summary>
        public IEventRedirector BuildEventRedirector
        {
            get { return _buildEventRedirector; }
            set { _buildEventRedirector = value; }
        }

        /// <summary>
        /// The identifier of the node.
        /// </summary>
        public int NodeId
        {
            get { return _nodeId; }
            set { _nodeId = value; }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        private void ParseParameters(IEventSource eventSource)
        {
            if (_loggerParameters != null)
            {
                string[] parameterComponents = _loggerParameters.Split(s_parameterDelimiters);
                for (int param = 0; param < parameterComponents.Length; param++)
                {
                    if (parameterComponents[param].Length > 0)
                    {
                        ApplyParameter(eventSource, parameterComponents[param]);
                    }
                }
                // Setting events to forward on the commandline will override the verbosity and other switches such as
                // showPerfSummary and ShowSummary
                if (_forwardingSetFromParameters)
                {
                    _showPerfSummary = false;
                    _showSummary = true;
                }

                if (_forwardProjectContext)
                {
                    // We can't know whether the project items needed to find ForwardProjectContextDescription
                    // will be set on ProjectStarted or ProjectEvaluationFinished because we don't know
                    // all of the other loggers that will be attached. So turn both on.
                    eventSource.StatusEventRaised -= BuildStatusHandler;
                    eventSource.StatusEventRaised += BuildStatusHandler;
                    eventSource.ProjectStarted -= ForwardEvent;
                    eventSource.ProjectStarted += ForwardEvent;
                }
            }
        }

        /// <summary>
        /// Logger parameters can be used to enable and disable specific event types.
        /// Otherwise, the verbosity is used to choose which events to forward.
        /// </summary>
        private void ApplyParameter(IEventSource eventSource, string parameterName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parameterName, nameof(parameterName));

            bool isEventForwardingParameter = true;

            // Careful - we need to brace before double specified parameters - hence the unsubscriptions before subscriptions
            switch (parameterName.ToUpperInvariant())
            {
                case BuildStartedEventDescription:
                    eventSource.BuildStarted -= ForwardEvent;
                    eventSource.BuildStarted += ForwardEvent;
                    break;
                case BuildFinishedEventDescription:
                    eventSource.BuildFinished -= ForwardEvent;
                    eventSource.BuildFinished += ForwardEvent;
                    break;
                case ProjectStartedEventDescription:
                    eventSource.ProjectStarted -= ForwardEvent;
                    eventSource.ProjectStarted += ForwardEvent;
                    break;
                case ProjectFinishedEventDescription:
                    eventSource.ProjectFinished -= ForwardEvent;
                    eventSource.ProjectFinished += ForwardEvent;
                    break;
                case TargetStartedEventDescription:
                    eventSource.TargetStarted -= ForwardEvent;
                    eventSource.TargetStarted += ForwardEvent;
                    break;
                case TargetFinishedEventDescription:
                    eventSource.TargetFinished -= ForwardEvent;
                    eventSource.TargetFinished += ForwardEvent;
                    break;
                case TaskStartedEventDescription:
                    eventSource.TaskStarted -= ForwardEvent;
                    eventSource.TaskStarted += ForwardEvent;
                    break;
                case TaskFinishedEventDescription:
                    eventSource.TaskFinished -= ForwardEvent;
                    eventSource.TaskFinished += ForwardEvent;
                    break;
                case ErrorEventDescription:
                    eventSource.ErrorRaised -= ForwardEvent;
                    eventSource.ErrorRaised += ForwardEvent;
                    break;
                case WarningEventDescription:
                    eventSource.WarningRaised -= ForwardEvent;
                    eventSource.WarningRaised += ForwardEvent;
                    break;
                case CustomEventDescription:
                    eventSource.CustomEventRaised -= ForwardEvent;
                    eventSource.CustomEventRaised += ForwardEvent;
                    break;
                case HighMessageEventDescription:
                    eventSource.MessageRaised -= MessageHandler;
                    eventSource.MessageRaised += MessageHandler;
                    _forwardHighImportanceMessages = true;
                    break;
                case NormalMessageEventDescription:
                    eventSource.MessageRaised -= MessageHandler;
                    eventSource.MessageRaised += MessageHandler;
                    _forwardNormalImportanceMessages = true;
                    break;
                case LowMessageEventDescription:
                    eventSource.MessageRaised -= MessageHandler;
                    eventSource.MessageRaised += MessageHandler;
                    _forwardLowImportanceMessages = true;
                    break;
                case CommandLineDescription:
                    eventSource.MessageRaised -= MessageHandler;
                    eventSource.MessageRaised += MessageHandler;
                    _forwardTaskCommandLine = true;
                    break;
                case ProjectEvaluationStartedEventDescription:
                case ProjectEvaluationFinishedEventDescription:
                case ProjectEvaluationEventDescription:
                    eventSource.StatusEventRaised -= BuildStatusHandler;
                    eventSource.StatusEventRaised += BuildStatusHandler;
                    break;
                case PerformanceSummaryDescription:
                    _showPerfSummary = true;
                    isEventForwardingParameter = false;
                    break;
                case NoSummaryDescription:
                    _showSummary = false;
                    isEventForwardingParameter = false;
                    break;
                case ShowCommandLineDescription:
                    _showCommandLine = true;
                    isEventForwardingParameter = false;
                    break;
                case ForwardProjectContextDescription:
                    _forwardProjectContext = true;
                    isEventForwardingParameter = false;
                    break;
                default:
                    isEventForwardingParameter = false;
                    break;
            }

            if (isEventForwardingParameter)
            {
                _forwardingSetFromParameters = true;
            }
        }

        /// <summary>
        /// Signs up the console logger for all build events.
        /// </summary>
        public virtual void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, nameof(eventSource));

            ParseParameters(eventSource);

            ResetLoggerState();

            if (!_forwardingSetFromParameters)
            {
                SetForwardingBasedOnVerbosity(eventSource);
            }
        }

        /// <summary>
        /// Signs up the console logger for all build events.
        /// </summary>
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        private void SetForwardingBasedOnVerbosity(IEventSource eventSource)
        {
            if (IsVerbosityAtLeast(LoggerVerbosity.Quiet))
            {
                eventSource.ErrorRaised += ForwardEvent;
                eventSource.WarningRaised += ForwardEvent;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Minimal))
            {
                eventSource.MessageRaised += MessageHandler;
                _forwardHighImportanceMessages = true;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                // MessageHandler already subscribed
                _forwardNormalImportanceMessages = true;

                eventSource.ProjectStarted += ForwardEvent;
                eventSource.ProjectFinished += ForwardEvent;
                eventSource.TargetStarted += ForwardEvent;
                eventSource.TargetFinished += ForwardEvent;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                eventSource.TaskStarted += ForwardEvent;
                eventSource.TaskFinished += ForwardEvent;

                // MessageHandler already subscribed
                _forwardLowImportanceMessages = true;
                _forwardTaskCommandLine = true;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic))
            {
                eventSource.CustomEventRaised += ForwardEvent;
                eventSource.StatusEventRaised += BuildStatusHandler;
            }

            if (_showSummary)
            {
                // Prevent double subscribe
                eventSource.ErrorRaised -= ForwardEvent;
                eventSource.WarningRaised -= ForwardEvent;
                eventSource.ErrorRaised += ForwardEvent;
                eventSource.WarningRaised += ForwardEvent;
            }

            if (_showPerfSummary)
            {
                // Prevent double subscribe
                eventSource.TaskStarted -= ForwardEvent;
                eventSource.TaskFinished -= ForwardEvent;
                eventSource.TargetStarted -= ForwardEvent;
                eventSource.TargetFinished -= ForwardEvent;
                eventSource.ProjectStarted -= ForwardEvent;
                eventSource.ProjectFinished -= ForwardEvent;
                eventSource.StatusEventRaised -= BuildStatusHandler;

                eventSource.TaskStarted += ForwardEvent;
                eventSource.TaskFinished += ForwardEvent;
                eventSource.TargetStarted += ForwardEvent;
                eventSource.TargetFinished += ForwardEvent;
                eventSource.ProjectStarted += ForwardEvent;
                eventSource.ProjectFinished += ForwardEvent;
                eventSource.StatusEventRaised += BuildStatusHandler;
            }

            if (_showCommandLine)
            {
                // Prevent double subscribe
                eventSource.MessageRaised -= MessageHandler;
                eventSource.MessageRaised += MessageHandler;

                _forwardTaskCommandLine = true;
            }
        }

        /// <summary>
        /// Returns the minimum importance of messages logged by this logger.
        /// </summary>
        /// <returns>
        /// The minimum message importance corresponding to this logger's verbosity or (MessageImportance.High - 1)
        /// if this logger does not log messages of any importance.
        /// </returns>
        internal MessageImportance GetMinimumMessageImportance()
        {
            return _verbosity switch
            {
                LoggerVerbosity.Minimal => MessageImportance.High,
                LoggerVerbosity.Normal => MessageImportance.Normal,
                LoggerVerbosity.Detailed => MessageImportance.Low,
                LoggerVerbosity.Diagnostic => MessageImportance.Low,

                // The logger does not log messages of any importance.
                LoggerVerbosity.Quiet => MessageImportance.High - 1,
                _ => MessageImportance.High - 1,
            };
        }

        /// <summary>
        /// Reset the states of per-build member variables.
        /// Used when a build is finished, but the logger might be needed for the next build.
        /// </summary>
        private void ResetLoggerState()
        {
            // No state needs resetting
        }

        /// <summary>
        /// Called when Engine is done with this logger
        /// </summary>
        public virtual void Shutdown()
        {
            // Nothing to do
        }

        /// <summary>
        /// Handler for build events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void ForwardEvent(object sender, BuildEventArgs e)
        {
            ForwardToCentralLogger(e);
        }

        private void BuildStatusHandler(object sender, BuildStatusEventArgs e)
        {
            if (e is ProjectEvaluationStartedEventArgs || e is ProjectEvaluationFinishedEventArgs)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Tailored handler for BuildMessageEventArgs - fine tunes forwarding of messages.
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void MessageHandler(object sender, BuildMessageEventArgs e)
        {
            bool forwardEvent =
                (_forwardLowImportanceMessages && e.Importance == MessageImportance.Low) ||
                (_forwardNormalImportanceMessages && e.Importance == MessageImportance.Normal) ||
                (_forwardHighImportanceMessages && e.Importance == MessageImportance.High) ||
                (_forwardTaskCommandLine && e is TaskCommandLineEventArgs);

            if (forwardEvent)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Forwards the specified event.
        /// </summary>
        /// <param name="e">The <see cref="BuildEventArgs"/> to forward.</param>
        protected virtual void ForwardToCentralLogger(BuildEventArgs e)
        {
            _buildEventRedirector.ForwardEvent(e);
        }

        /// <summary>
        /// Determines whether the current verbosity setting is at least the value
        /// passed in.
        /// </summary>
        private bool IsVerbosityAtLeast(LoggerVerbosity checkVerbosity)
        {
            return _verbosity >= checkVerbosity;
        }
        #endregion

        #region Private member data

        /// <summary>
        /// Controls the amount of text displayed by the logger
        /// </summary>
        private LoggerVerbosity _verbosity = LoggerVerbosity.Normal;

        /// <summary>
        /// Console logger parameters.
        /// </summary>
        private string _loggerParameters = null;

        /// <summary>
        /// Console logger parameters delimiters.
        /// </summary>
        private static readonly char[] s_parameterDelimiters = MSBuildConstants.SemicolonChar;

        /// <summary>
        /// Strings that users of this logger can pass in to enable specific events or logger output.
        /// Also used as keys into our dictionary.
        /// </summary>
        private const string BuildStartedEventDescription = "BUILDSTARTEDEVENT";
        private const string BuildFinishedEventDescription = "BUILDFINISHEDEVENT";
        private const string ProjectStartedEventDescription = "PROJECTSTARTEDEVENT";
        private const string ProjectFinishedEventDescription = "PROJECTFINISHEDEVENT";
        private const string ProjectEvaluationEventDescription = "PROJECTEVALUATIONEVENT";
        private const string ProjectEvaluationStartedEventDescription = "PROJECTEVALUATIONSTARTEDEVENT";
        private const string ProjectEvaluationFinishedEventDescription = "PROJECTEVALUATIONFINISHEDEVENT";
        private const string TargetStartedEventDescription = "TARGETSTARTEDEVENT";
        private const string TargetFinishedEventDescription = "TARGETFINISHEDEVENT";
        private const string TaskStartedEventDescription = "TASKSTARTEDEVENT";
        private const string TaskFinishedEventDescription = "TASKFINISHEDEVENT";
        private const string ErrorEventDescription = "ERROREVENT";
        private const string WarningEventDescription = "WARNINGEVENT";
        private const string HighMessageEventDescription = "HIGHMESSAGEEVENT";
        private const string NormalMessageEventDescription = "NORMALMESSAGEEVENT";
        private const string LowMessageEventDescription = "LOWMESSAGEEVENT";
        private const string CustomEventDescription = "CUSTOMEVENT";
        private const string CommandLineDescription = "COMMANDLINE";
        private const string PerformanceSummaryDescription = "PERFORMANCESUMMARY";
        private const string NoSummaryDescription = "NOSUMMARY";
        private const string ShowCommandLineDescription = "SHOWCOMMANDLINE";
        private const string ForwardProjectContextDescription = "FORWARDPROJECTCONTEXTEVENTS";

        #region Per-build Members

        /// <summary>
        /// A pointer to the central logger
        /// </summary>
        private IEventRedirector _buildEventRedirector;

        /// <summary>
        /// Indicates if the events to forward are being set by the parameters sent to the logger
        /// if this is false the events to forward are based on verbosity else verbosity settings will be ignored
        /// </summary>
        private bool _forwardingSetFromParameters;

        /// <summary>
        /// Indicates if the events to forward should include project context events, if not
        /// overridden by individual-event forwarding in <see cref="_forwardingSetFromParameters"/>.
        /// </summary>
        private bool _forwardProjectContext = false;

        /// <summary>
        /// Console logger should show error and warning summary at the end of build?
        /// </summary>
        private bool _showSummary = true;

        /// <summary>
        /// When true, accumulate performance numbers.
        /// </summary>
        private bool _showPerfSummary = false;

        /// <summary>
        /// When true the commandline message is sent
        /// </summary>
        private bool _showCommandLine = false;

        /// <summary>
        /// Fine-tuning of BuildMessageEventArgs forwarding
        /// </summary>
        private bool _forwardLowImportanceMessages;

        /// <summary>
        /// Fine-tuning of BuildMessageEventArgs forwarding
        /// </summary>
        private bool _forwardNormalImportanceMessages;

        /// <summary>
        /// Fine-tuning of BuildMessageEventArgs forwarding
        /// </summary>
        private bool _forwardHighImportanceMessages;

        /// <summary>
        /// Fine-tuning of BuildMessageEventArgs forwarding
        /// </summary>
        private bool _forwardTaskCommandLine;

        /// <summary>
        /// Id of the node the logger is attached to
        /// </summary>
        private int _nodeId;

        #endregion
        #endregion
    }
}
