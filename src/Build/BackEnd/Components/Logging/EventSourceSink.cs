// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Sink which will take in a build event and raise it on its internal event source</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// This class raises events on behalf of the build engine to all registered loggers.
    /// </summary>
    internal sealed class EventSourceSink :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        IEventSource2, IBuildEventSink
    {
        #region Events

        /// <summary>
        /// This event is raised to log a message.
        /// </summary>
        public event BuildMessageEventHandler MessageRaised;

        /// <summary>
        /// This event is raised to log an error.
        /// </summary>
        public event BuildErrorEventHandler ErrorRaised;

        /// <summary>
        /// This event is raised to log a warning.
        /// </summary>
        public event BuildWarningEventHandler WarningRaised;

        /// <summary>
        /// this event is raised to log the start of a build
        /// </summary>
        public event BuildStartedEventHandler BuildStarted;

        /// <summary>
        /// this event is raised to log the end of a build
        /// </summary>
        public event BuildFinishedEventHandler BuildFinished;

        /// <summary>
        /// this event is raised to log the start of a project build
        /// </summary>
        public event ProjectStartedEventHandler ProjectStarted;

        /// <summary>
        /// this event is raised to log the end of a project build
        /// </summary>
        public event ProjectFinishedEventHandler ProjectFinished;

        /// <summary>
        /// this event is raised to log the start of a target build
        /// </summary>
        public event TargetStartedEventHandler TargetStarted;

        /// <summary>
        /// this event is raised to log the end of a target build
        /// </summary>
        public event TargetFinishedEventHandler TargetFinished;

        /// <summary>
        /// this event is raised to log the start of task execution
        /// </summary>
        public event TaskStartedEventHandler TaskStarted;

        /// <summary>
        /// this event is raised to log the end of task execution
        /// </summary>
        public event TaskFinishedEventHandler TaskFinished;

        /// <summary>
        /// this event is raised to log a custom event
        /// </summary>
        public event CustomBuildEventHandler CustomEventRaised;

        /// <summary>
        /// this event is raised to log build status events, such as 
        /// build/project/target/task started/stopped 
        /// </summary>
        public event BuildStatusEventHandler StatusEventRaised;

        /// <summary>
        /// This event is raised to log that some event has
        /// occurred.  It is raised on every event.
        /// </summary>
        public event AnyEventHandler AnyEventRaised;
        
        /// <summary>
        /// This event is raised to log telemetry.
        /// </summary>
        public event TelemetryEventHandler TelemetryLogged;
        #endregion

        #region Properties
        /// <summary>
        /// Provide a friendly name for the sink to make it easier to differentiate during 
        /// debugging and display
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Has the sink logged the BuildStartedEvent. This is important to know because we only want to log the build started event once
        /// </summary>
        public bool HaveLoggedBuildStartedEvent
        {
            get;
            set;
        }

        /// <summary>
        /// Has the sink logged the BuildFinishedEvent. This is important to know because we only want to log the build finished event once
        /// </summary>
        public bool HaveLoggedBuildFinishedEvent
        {
            get;
            set;
        }

        /// <summary>
        /// A list of warnings to treat as errors.  If null, nothing is treated as an error.  If an empty set, all warnings are treated as errors.
        /// </summary>
        public ISet<string> WarningsAsErrors
        {
            get;
            set;
        }

        /// <summary>
        /// A list of warnings to treat as errors for an associated <see cref="BuildEventContext.ProjectInstanceId"/>.  If the set associated with a ProjectInstanceId is null, nothing is treated as an error.  If an empty set, all warnings are treated as errors.
        /// </summary>
        public IDictionary<int, ISet<string>> WarningsAsErrorsByProject
        {
            get;
            set;
        }

        /// <summary>
        /// A list of warnings to treat as low importance messages.
        /// </summary>
        public ISet<string> WarningsAsMessages
        {
            get;
            set;
        }

        /// <summary>
        /// A list of warnings to treat as low importance messages for an associated <see cref="BuildEventContext.ProjectInstanceId"/>.
        /// </summary>
        public IDictionary<int, ISet<string>> WarningsAsMessagesByProject
        {
            get;
            set;
        }

        /// <summary>
        /// A list of build submission IDs that have logged errors.  If an error is logged outside of a submission, the submission ID is <see cref="BuildEventContext.InvalidSubmissionId"/>.
        /// </summary>
        public ISet<int> BuildSubmissionIdsThatHaveLoggedErrors
        {
            get;
        } = new HashSet<int>();
        
        #endregion

        #region Methods

        #region IEventSink Methods

        /// <summary>
        /// Raises the given event to all registered loggers. This method up-cast the events
        /// extracted from the queue.
        /// </summary>
        /// <param name="buildEvent">BuildEventArgs</param>
        /// <param name="sinkId">Note this is not used in the eventsource sink</param>
        public void Consume(BuildEventArgs buildEvent, int sinkId)
        {
            Consume(buildEvent);
        }

        /// <summary>
        /// Raises the given event to all registered loggers. This method up-cast the events
        /// extracted from the queue.
        /// </summary>
        public void Consume(BuildEventArgs buildEvent)
        {
            // FXCop may complain that there are unecessary casts here, and there are, but
            // using "as" and allocating another variable for each event is extremely costly
            // and is much slower then this approach even with the additional casts
            if (buildEvent is BuildMessageEventArgs)
            {
                this.RaiseMessageEvent(null, (BuildMessageEventArgs)buildEvent);
            }
            else if (buildEvent is TaskStartedEventArgs)
            {
                this.RaiseTaskStartedEvent(null, (TaskStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TaskFinishedEventArgs)
            {
                this.RaiseTaskFinishedEvent(null, (TaskFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetStartedEventArgs)
            {
                this.RaiseTargetStartedEvent(null, (TargetStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetFinishedEventArgs)
            {
                this.RaiseTargetFinishedEvent(null, (TargetFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectStartedEventArgs)
            {
                this.RaiseProjectStartedEvent(null, (ProjectStartedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectFinishedEventArgs)
            {
                this.RaiseProjectFinishedEvent(null, (ProjectFinishedEventArgs)buildEvent);

                if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.ProjectInstanceId != BuildEventContext.InvalidProjectInstanceId)
                {
                    WarningsAsErrorsByProject?.Remove(buildEvent.BuildEventContext.ProjectInstanceId);
                    WarningsAsMessagesByProject?.Remove(buildEvent.BuildEventContext.ProjectInstanceId);
                }
            }
            else if (buildEvent is BuildStartedEventArgs)
            {
                HaveLoggedBuildStartedEvent = true;
                this.RaiseBuildStartedEvent(null, (BuildStartedEventArgs)buildEvent);
            }
            else if (buildEvent is BuildFinishedEventArgs)
            {
                HaveLoggedBuildFinishedEvent = true;
                this.RaiseBuildFinishedEvent(null, (BuildFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is CustomBuildEventArgs)
            {
                this.RaiseCustomEvent(null, (CustomBuildEventArgs)buildEvent);
            }
            else if (buildEvent is BuildStatusEventArgs)
            {
                this.RaiseStatusEvent(null, (BuildStatusEventArgs)buildEvent);
            }
            else if (buildEvent is BuildWarningEventArgs)
            {
                BuildWarningEventArgs warningEvent = (BuildWarningEventArgs) buildEvent;

                if (ShouldTreatWarningAsMessage(warningEvent))
                {
                    // Treat this warning as a message with low importance if its in the list
                    BuildMessageEventArgs errorEvent = new BuildMessageEventArgs(
                        warningEvent.Subcategory,
                        warningEvent.Code,
                        warningEvent.File,
                        warningEvent.LineNumber,
                        warningEvent.ColumnNumber,
                        warningEvent.EndLineNumber,
                        warningEvent.EndColumnNumber,
                        warningEvent.Message,
                        warningEvent.HelpKeyword,
                        warningEvent.SenderName,
                        MessageImportance.Low,
                        warningEvent.Timestamp)
                    {
                        BuildEventContext = warningEvent.BuildEventContext,
                        ProjectFile = warningEvent.ProjectFile,
                    };

                    this.RaiseMessageEvent(null, errorEvent);

                }
                else if (ShouldTreatWarningAsError(warningEvent))
                {
                    // Treat this warning as an error if an empty set of warnings was specified or this code was specified
                    BuildErrorEventArgs errorEvent = new BuildErrorEventArgs(
                        warningEvent.Subcategory,
                        warningEvent.Code,
                        warningEvent.File,
                        warningEvent.LineNumber,
                        warningEvent.ColumnNumber,
                        warningEvent.EndLineNumber,
                        warningEvent.EndColumnNumber,
                        warningEvent.Message,
                        warningEvent.HelpKeyword,
                        warningEvent.SenderName,
                        warningEvent.Timestamp)
                    {
                        BuildEventContext = warningEvent.BuildEventContext,
                        ProjectFile = warningEvent.ProjectFile,
                    };

                    this.RaiseErrorEvent(null, errorEvent);
                }
                else
                {
                    this.RaiseWarningEvent(null, warningEvent);
                }
            }
            else if (buildEvent is BuildErrorEventArgs)
            {
                this.RaiseErrorEvent(null, (BuildErrorEventArgs)buildEvent);
            }
            else if (buildEvent is TelemetryEventArgs)
            {
                this.RaiseTelemetryEvent(null, (TelemetryEventArgs) buildEvent);
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Unknown event args type.");
            }
        }

        /// <summary>
        /// Shutdown and displose of any resource this object is holding onto.
        /// </summary>
        public void ShutDown()
        {
            this.UnregisterAllEventHandlers();
        }
        #endregion

        #region Internal Methods

        /// <summary>
        /// Clears out all events.
        /// </summary>
        internal void UnregisterAllEventHandlers()
        {
            MessageRaised = null;
            ErrorRaised = null;
            WarningRaised = null;
            BuildStarted = null;
            BuildFinished = null;
            ProjectStarted = null;
            ProjectFinished = null;
            TargetStarted = null;
            TargetFinished = null;
            TaskStarted = null;
            TaskFinished = null;
            CustomEventRaised = null;
            StatusEventRaised = null;
            AnyEventRaised = null;
            TelemetryLogged = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Raises a message event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildMessageEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception> 
        private void RaiseMessageEvent(object sender, BuildMessageEventArgs buildEvent)
        {
            if (MessageRaised != null)
            {
                try
                {
                    MessageRaised(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises an error event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildErrorEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseErrorEvent(object sender, BuildErrorEventArgs buildEvent)
        {
            // Keep track of build submissions that have logged errors.  If there is no build context, add BuildEventContext.InvalidSubmissionId.
            BuildSubmissionIdsThatHaveLoggedErrors.Add(buildEvent?.BuildEventContext?.SubmissionId ?? BuildEventContext.InvalidSubmissionId);

            if (ErrorRaised != null)
            {
                try
                {
                    ErrorRaised(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a warning event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildWarningEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseWarningEvent(object sender, BuildWarningEventArgs buildEvent)
        {
            if (WarningRaised != null)
            {
                try
                {
                    WarningRaised(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "build started" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildStartedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseBuildStartedEvent(object sender, BuildStartedEventArgs buildEvent)
        {
            if (BuildStarted != null)
            {
                try
                {
                    BuildStarted(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "build finished" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildFinishedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseBuildFinishedEvent(object sender, BuildFinishedEventArgs buildEvent)
        {
            if (BuildFinished != null)
            {
                try
                {
                    BuildFinished(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "project build started" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">ProjectStartedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseProjectStartedEvent(object sender, ProjectStartedEventArgs buildEvent)
        {
            if (ProjectStarted != null)
            {
                try
                {
                    ProjectStarted(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "project build finished" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">ProjectFinishedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseProjectFinishedEvent(object sender, ProjectFinishedEventArgs buildEvent)
        {
            if (ProjectFinished != null)
            {
                try
                {
                    ProjectFinished(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "target build started" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">TargetStartedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseTargetStartedEvent(object sender, TargetStartedEventArgs buildEvent)
        {
            if (TargetStarted != null)
            {
                try
                {
                    TargetStarted(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "target build finished" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">TargetFinishedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseTargetFinishedEvent(object sender, TargetFinishedEventArgs buildEvent)
        {
            if (TargetFinished != null)
            {
                try
                {
                    TargetFinished(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "task execution started" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">TaskStartedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseTaskStartedEvent(object sender, TaskStartedEventArgs buildEvent)
        {
            if (TaskStarted != null)
            {
                try
                {
                    TaskStarted(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a "task finished executing" event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">TaskFinishedEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseTaskFinishedEvent(object sender, TaskFinishedEventArgs buildEvent)
        {
            if (TaskFinished != null)
            {
                try
                {
                    TaskFinished(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a custom event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">CustomBuildEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseCustomEvent(object sender, CustomBuildEventArgs buildEvent)
        {
            if (CustomEventRaised != null)
            {
                try
                {
                    CustomEventRaised(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a catch-all build status event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">BuildStatusEventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseStatusEvent(object sender, BuildStatusEventArgs buildEvent)
        {
            if (StatusEventRaised != null)
            {
                try
                {
                    StatusEventRaised(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, buildEvent);
        }

        /// <summary>
        /// Raises a catch-all build event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">Build EventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseAnyEvent(object sender, BuildEventArgs buildEvent)
        {
            if (AnyEventRaised != null)
            {
                try
                {
                    AnyEventRaised(sender, buildEvent);
                }
                catch (LoggerException exception)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    // We ought to dump this farther up the stack, but if for example a task is logging an event within a 
                    // catch(Exception) block and not rethrowing it, there's the possibility that this exception could 
                    // just get silently eaten.  So better to have duplicates than to not log the problem at all. :) 
                    ExceptionHandling.DumpExceptionToFile(exception);

                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    // We ought to dump this farther up the stack, but if for example a task is logging an event within a 
                    // catch(Exception) block and not rethrowing it, there's the possibility that this exception could 
                    // just get silently eaten.  So better to have duplicates than to not log the problem at all. :) 
                    ExceptionHandling.DumpExceptionToFile(exception);

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }
        }

        /// <summary>
        /// Raises the a telemetry event to all registered loggers.
        /// </summary>
        private void RaiseTelemetryEvent(object sender, TelemetryEventArgs buildEvent)
        {
            if (TelemetryLogged != null)
            {
                try
                {
                    TelemetryLogged(sender, buildEvent);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllEventHandlers();

                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
                }
            }
        }

        /// <summary>
        /// Determines if the specified warning should be treated as an error.
        /// </summary>
        /// <param name="warningEvent">A <see cref="BuildWarningEventArgs"/> that specifies the warning.</param>
        /// <returns><code>true</code> if the warning should be treated as an error, otherwise <code>false</code>.</returns>
        private bool ShouldTreatWarningAsError(BuildWarningEventArgs warningEvent)
        {
            // This only applies if the user specified /warnaserror from the command-line or added an empty set through the object model
            //
            if (WarningsAsErrors != null)
            {
                // Global warnings as errors apply to all projects.  If the list is empty or contains the code, the warning should be treated as an error
                //
                if (WarningsAsErrors.Count == 0 || WarningsAsErrors.Contains(warningEvent.Code))
                {
                    return true;
                }
            }

            // This only applies if the user specified <MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors or <MSBuildWarningsAsErrors />
            // and there is a valid ProjectInstanceId for the warning.
            //
            if (WarningsAsErrorsByProject != null && warningEvent.BuildEventContext != null && warningEvent.BuildEventContext.ProjectInstanceId != BuildEventContext.InvalidProjectInstanceId)
            {
                ISet<string> codesByProject;

                // Attempt to get the list of warnings to treat as errors for the current project
                //
                if (WarningsAsErrorsByProject.TryGetValue(warningEvent.BuildEventContext.ProjectInstanceId, out codesByProject) && codesByProject != null)
                {
                    // We create an empty set if all warnings should be treated as errors so that should be checked first.
                    // If the set is not empty, check the specific code.
                    //
                    return codesByProject.Count == 0 || codesByProject.Contains(warningEvent.Code);
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the specified warning should be treated as a low importance message.
        /// </summary>
        /// <param name="warningEvent">A <see cref="BuildWarningEventArgs"/> that specifies the warning.</param>
        /// <returns><code>true</code> if the warning should be treated as a low importance message, otherwise <code>false</code>.</returns>
        private bool ShouldTreatWarningAsMessage(BuildWarningEventArgs warningEvent)
        {
            // This only applies if the user specified /nowarn at the command-line or added the warning code through the object model
            //
            if (WarningsAsMessages != null && WarningsAsMessages.Contains(warningEvent.Code))
            {
                return true;
            }

            // This only applies if the user specified <MSBuildWarningsAsMessages /> and there is a valid ProjectInstanceId
            //
            if (WarningsAsMessagesByProject != null && warningEvent.BuildEventContext != null && warningEvent.BuildEventContext.ProjectInstanceId != BuildEventContext.InvalidProjectInstanceId)
            {
                ISet<string> codesByProject;

                if (WarningsAsMessagesByProject.TryGetValue(warningEvent.BuildEventContext.ProjectInstanceId, out codesByProject) && codesByProject != null)
                {
                    return codesByProject.Contains(warningEvent.Code);
                }
            }

            return false;
        }

        #endregion
        #endregion
    }
}
