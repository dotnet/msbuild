// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;


namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Logger that forwards events to a central logger (e.g ConsoleLogger)
    /// residing on the parent node.
    /// </summary>
    public class ConfigurableForwardingLogger: IForwardingLogger
    {
        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConfigurableForwardingLogger()
        {
            InitializeForwardingTable();
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the level of detail to show in the event log.
        /// </summary>
        /// <value>Verbosity level.</value>
        public LoggerVerbosity Verbosity
        {
            get { return verbosity; }
            set { verbosity = value; }
        }

        /// <summary>
        /// The console logger takes a single parameter to suppress the output of the errors
        /// and warnings summary at the end of a build.
        /// </summary>
        /// <value>null</value>
        public string Parameters
        {
            get { return loggerParameters; }
            set { loggerParameters = value; }
        }

        /// <summary>
        /// This property is set by the build engine to allow a node loggers to forward messages to the
        /// central logger
        /// </summary>
        public IEventRedirector BuildEventRedirector
        {
            get { return this.buildEventRedirector; }
            set { this.buildEventRedirector = value; }
        }

        public int NodeId
        {
            get { return nodeId; }
            set { nodeId = value; }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Initialize the Forwarding Table with the default values
        /// </summary>
        private void InitializeForwardingTable()
        {
            forwardingTable = new Dictionary<string, int>(15, StringComparer.OrdinalIgnoreCase);
            forwardingTable[BuildStartedEventDescription] = 0;
            forwardingTable[BuildFinishedEventDescription] = 0;
            forwardingTable[ProjectStartedEventDescription] = 0;
            forwardingTable[ProjectFinishedEventDescription] = 0;
            forwardingTable[TargetStartedEventDescription] = 0;
            forwardingTable[TargetFinishedEventDescription] = 0;
            forwardingTable[TaskStartedEventDescription] = 0;
            forwardingTable[TaskFinishedEventDescription] = 0;
            forwardingTable[ErrorEventDescription] = 0;
            forwardingTable[WarningEventDescription] = 0;
            forwardingTable[HighMessageEventDescription] = 0;
            forwardingTable[NormalMessageEventDescription] = 0;
            forwardingTable[LowMessageEventDescription] = 0;
            forwardingTable[CustomEventDescription] = 0;
            forwardingTable[CommandLineDescription] = 0;
            forwardingSetFromParameters = false;
        }

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        private void ParseParameters()
        {
            if (loggerParameters != null)
            {
                string[] parameterComponents;

                parameterComponents = loggerParameters.Split(parameterDelimiters);
                for (int param = 0; param < parameterComponents.Length; param++)
                {
                    if (parameterComponents[param].Length > 0)
                    {
                        ApplyParameter(parameterComponents[param]);
                    }
                }
                // Setting events to forward on the commandline will override the verbosity and other switches such as
                // showPerfSummand and ShowSummary
                if (forwardingSetFromParameters)
                {
                    this.showPerfSummary = false;
                    this.showSummary = true;
                }
            }
        }

        /// <summary>
        /// Logger parameters can be used to enable and disable specific event types.
        /// Otherwise, the verbosity is used to choose which events to forward.
        /// </summary>
        private void ApplyParameter(string parameterName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parameterName, "parameterName");

            if (forwardingTable.ContainsKey(parameterName))
            {
                forwardingSetFromParameters = true;
                forwardingTable[parameterName] = 1;
            }

            // If any of the following parameters are set, we will make sure we forward the events
            // necessary for the central logger to emit the requested information
            if (0 == String.Compare(parameterName, PerformanceSummaryDescription, StringComparison.OrdinalIgnoreCase))
            {
                this.showPerfSummary = true;
            }
            else if (0 == String.Compare(parameterName, NoSummaryDescription, StringComparison.OrdinalIgnoreCase))
            {
                this.showSummary = false;
            }
            else if (0 == String.Compare(parameterName, ShowCommandLineDescription, StringComparison.OrdinalIgnoreCase))
            {
                showCommandLine = true;
            }
        }

        /// <summary>
        /// Signs up the console logger for all build events.
        /// </summary>
        public virtual void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, "eventSource");

            ParseParameters();

            ResetLoggerState();

            if (!forwardingSetFromParameters)
            {
                SetForwardingBasedOnVerbosity();
            }

            eventSource.BuildStarted += new BuildStartedEventHandler(BuildStartedHandler);
            eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinishedHandler);
            eventSource.ProjectStarted += new ProjectStartedEventHandler(ProjectStartedHandler);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(ProjectFinishedHandler);
            eventSource.TargetStarted += new TargetStartedEventHandler(TargetStartedHandler);
            eventSource.TargetFinished += new TargetFinishedEventHandler(TargetFinishedHandler);
            eventSource.TaskStarted += new TaskStartedEventHandler(TaskStartedHandler);
            eventSource.TaskFinished += new TaskFinishedEventHandler(TaskFinishedHandler);
            eventSource.ErrorRaised += new BuildErrorEventHandler(ErrorHandler);
            eventSource.WarningRaised += new BuildWarningEventHandler(WarningHandler);
            eventSource.MessageRaised += new BuildMessageEventHandler(MessageHandler);
            eventSource.CustomEventRaised += new CustomBuildEventHandler(CustomEventHandler);
        }

        /// <summary>
        /// Signs up the console logger for all build events.
        /// </summary>
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        private void SetForwardingBasedOnVerbosity()
        {

            forwardingTable[BuildStartedEventDescription] = 0;
            forwardingTable[BuildFinishedEventDescription] = 0;

            if (IsVerbosityAtLeast(LoggerVerbosity.Quiet))
            {
                forwardingTable[ErrorEventDescription] = 1;
                forwardingTable[WarningEventDescription] = 1;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Minimal))
            {
                forwardingTable[HighMessageEventDescription] = 1;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                forwardingTable[NormalMessageEventDescription] = 1;
                forwardingTable[ProjectStartedEventDescription] = 1;
                forwardingTable[ProjectFinishedEventDescription] = 1;
                forwardingTable[TargetStartedEventDescription] = 1;
                forwardingTable[TargetFinishedEventDescription] = 1;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                forwardingTable[TargetStartedEventDescription] = 1;
                forwardingTable[TargetFinishedEventDescription] = 1;
                forwardingTable[TaskStartedEventDescription] = 1;
                forwardingTable[TaskFinishedEventDescription] = 1;
                forwardingTable[LowMessageEventDescription] = 1;
                forwardingTable[CommandLineDescription] = 1;
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic))
            {
                forwardingTable[CustomEventDescription] = 1;
            }

            if (showSummary)
            {
                forwardingTable[ErrorEventDescription] = 1;
                forwardingTable[WarningEventDescription] = 1;
            }

            if (this.showPerfSummary)
            {
                forwardingTable[TargetStartedEventDescription] = 1;
                forwardingTable[TargetFinishedEventDescription] = 1;
                forwardingTable[TaskStartedEventDescription] = 1;
                forwardingTable[TaskFinishedEventDescription] = 1;
                forwardingTable[TargetStartedEventDescription] = 1;
                forwardingTable[TargetFinishedEventDescription] = 1;
                forwardingTable[ProjectStartedEventDescription] = 1;
                forwardingTable[ProjectFinishedEventDescription] = 1;
            }

            if (this.showCommandLine)
            {
                forwardingTable[CommandLineDescription] = 1;
            }
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
        /// Handler for build started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void BuildStartedHandler(object sender, BuildStartedEventArgs e)
        {
            // This is false by default
            if (forwardingTable[BuildStartedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for build finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
        {
            // This is false by default
            if (forwardingTable[BuildFinishedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
            ResetLoggerState();
        }

        /// <summary>
        /// Handler for project started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
        {
            if (forwardingTable[ProjectStartedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for project finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e)
        {
            if (forwardingTable[ProjectFinishedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for target started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void TargetStartedHandler(object sender, TargetStartedEventArgs e)
        {
            if (forwardingTable[TargetStartedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for target finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void TargetFinishedHandler(object sender, TargetFinishedEventArgs e)
        {
            if (forwardingTable[TargetFinishedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for task started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void TaskStartedHandler(object sender, TaskStartedEventArgs e)
        {
            if (forwardingTable[TaskStartedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Handler for task finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        private void TaskFinishedHandler(object sender, TaskFinishedEventArgs e)
        {
            if (forwardingTable[TaskFinishedEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Prints an error event
        /// </summary>
        private void ErrorHandler(object sender, BuildErrorEventArgs e)
        {
            if (forwardingTable[ErrorEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Prints a warning event
        /// </summary>
        private void WarningHandler(object sender, BuildWarningEventArgs e)
        {
            if (forwardingTable[WarningEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Prints a message event
        /// </summary>
        private void MessageHandler(object sender, BuildMessageEventArgs e)
        {
            bool forwardEvent = false;

            if (forwardingTable[LowMessageEventDescription] == 1 && e.Importance == MessageImportance.Low)
            {
                forwardEvent = true;
            }
            else if (forwardingTable[NormalMessageEventDescription] == 1 && e.Importance == MessageImportance.Normal)
            {
                forwardEvent = true;
            }
            else if (forwardingTable[HighMessageEventDescription] == 1 && e.Importance == MessageImportance.High)
            {
                forwardEvent = true;
            }
            else if (forwardingTable[CommandLineDescription] == 1 && e is TaskCommandLineEventArgs)
            {
                forwardEvent = true;
            }

            if (forwardEvent)
            {
                ForwardToCentralLogger(e);
            }
        }

        /// <summary>
        /// Prints a custom event
        /// </summary>
        private void CustomEventHandler(object sender, CustomBuildEventArgs e)
        {
            if (forwardingTable[CustomEventDescription] == 1)
            {
                ForwardToCentralLogger(e);
            }
        }

        protected virtual void ForwardToCentralLogger(BuildEventArgs e)
        {
            buildEventRedirector.ForwardEvent(e);
        }

        /// <summary>
        /// Determines whether the current verbosity setting is at least the value
        /// passed in.
        /// </summary>
        private bool IsVerbosityAtLeast(LoggerVerbosity checkVerbosity)
        {
            return (this.verbosity >= checkVerbosity);
        }
        #endregion

        #region Private member data

        /// <summary>
        /// Controls the amount of text displayed by the logger
        /// </summary>
        private LoggerVerbosity verbosity = LoggerVerbosity.Normal;

        /// <summary>
        /// Console logger parameters.
        /// </summary>
        private string loggerParameters = null;

        /// <summary>
        /// Console logger parameters delimiters.
        /// </summary>
        private static readonly char[] parameterDelimiters = { ';' };

        /// <summary>
        /// Strings that users of this logger can pass in to enable specific events or logger output.
        /// Also used as keys into our dictionary.
        /// </summary>
        private const string BuildStartedEventDescription = "BUILDSTARTEDEVENT";
        private const string BuildFinishedEventDescription = "BUILDFINISHEDEVENT";
        private const string ProjectStartedEventDescription = "PROJECTSTARTEDEVENT";
        private const string ProjectFinishedEventDescription = "PROJECTFINISHEDEVENT";
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

        #region Per-build Members

        /// <summary>
        /// A table indicating if a particular event type should be forwarded
        /// The value is type int rather than bool to avoid the problem of JITting generics.
        /// Dictionary<string, int> is already compiled into mscorlib.
        /// </summary>
        private Dictionary<string, int> forwardingTable;

        /// <summary>
        /// A pointer to the central logger
        /// </summary>
        private IEventRedirector buildEventRedirector;

        /// <summary>
        /// Indicates if the events to forward are being set by the parameters sent to the logger
        /// if this is false the events to forward are based on verbosity else verbosity settings will be ignored
        /// </summary>
        private bool forwardingSetFromParameters;

        /// <summary>
        /// Console logger should show error and warning summary at the end of build?
        /// </summary>
        private bool showSummary = true;

        /// <summary>
        /// When true, accumulate performance numbers.
        /// </summary>
        private bool showPerfSummary = false;

        /// <summary>
        /// When true the commandline message is sent
        /// </summary>
        private bool showCommandLine = false;

        /// <summary>
        /// Id of the node the logger is attached to
        /// </summary>
        private int nodeId;
        
        #endregion
        #endregion

    }
}
