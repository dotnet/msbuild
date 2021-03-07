// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Sink which will take in a build event and raise it on its internal event source</summary>

using System;
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
        IEventSource4, IBuildEventSink
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
        /// Should evaluation events include generated metaprojects?
        /// </summary>
        public bool IncludeEvaluationMetaprojects
        {
            get;
            private set;
        }

        /// <summary>
        /// Should evaluation events include profiling information?
        /// </summary>
        public bool IncludeEvaluationProfiles
        {
            get;
            private set;
        }

        /// <summary>
        /// Should task events include task inputs?
        /// </summary>
        public bool IncludeTaskInputs
        {
            get;
            private set;
        }

        /// <summary>
        /// Determines whether properties and items should be logged on <see cref="ProjectEvaluationFinishedEventArgs"/>
        /// instead of <see cref="ProjectStartedEventArgs"/>
        /// </summary>
        public bool IncludeEvaluationPropertiesAndItems
        {
            get;
            private set;
        }

        #endregion

        #region Methods

        #region IEventSource3 Methods

        void IEventSource3.IncludeEvaluationMetaprojects()
        {
            IncludeEvaluationMetaprojects = true;
        }

        void IEventSource3.IncludeEvaluationProfiles()
        {
            IncludeEvaluationProfiles = true;
        }

        void IEventSource3.IncludeTaskInputs()
        {
            IncludeTaskInputs = true;
        }

        #endregion

        #region IEventSource4 Methods

        void IEventSource4.IncludeEvaluationPropertiesAndItems()
        {
            IncludeEvaluationPropertiesAndItems = true;
        }

        #endregion

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
                this.RaiseWarningEvent(null, (BuildWarningEventArgs)buildEvent);
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

        #endregion
        #endregion
    }
}
