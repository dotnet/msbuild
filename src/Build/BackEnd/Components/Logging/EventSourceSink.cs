
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;

using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// This class raises events on behalf of the build engine to all registered loggers.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    internal sealed class EventSourceSink :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        IEventSource5, IBuildEventSink
    {
        #region Events

        /// <summary>
        /// This event is raised to log a message.
        /// </summary>
        public event BuildMessageEventHandler? MessageRaised;

        /// <summary>
        /// This event is raised to log an error.
        /// </summary>
        public event BuildErrorEventHandler? ErrorRaised;

        /// <summary>
        /// This event is raised to log a warning.
        /// </summary>
        public event BuildWarningEventHandler? WarningRaised;

        /// <summary>
        /// this event is raised to log the start of a build
        /// </summary>
        public event BuildStartedEventHandler? BuildStarted;

        /// <summary>
        /// this event is raised to log the end of a build
        /// </summary>
        public event BuildFinishedEventHandler? BuildFinished;

        /// <summary>
        /// this event is raised to log the start of a project build
        /// </summary>
        public event ProjectStartedEventHandler? ProjectStarted;

        /// <summary>
        /// this event is raised to log the end of a project build
        /// </summary>
        public event ProjectFinishedEventHandler? ProjectFinished;

        /// <summary>
        /// this event is raised to log the start of a target build
        /// </summary>
        public event TargetStartedEventHandler? TargetStarted;

        /// <summary>
        /// this event is raised to log the end of a target build
        /// </summary>
        public event TargetFinishedEventHandler? TargetFinished;

        /// <summary>
        /// this event is raised to log the start of task execution
        /// </summary>
        public event TaskStartedEventHandler? TaskStarted;

        /// <summary>
        /// this event is raised to log the end of task execution
        /// </summary>
        public event TaskFinishedEventHandler? TaskFinished;

        /// <summary>
        /// this event is raised to log a custom event
        /// </summary>
        public event CustomBuildEventHandler? CustomEventRaised;

        /// <summary>
        /// this event is raised to log build status events, such as
        /// build/project/target/task started/stopped
        /// </summary>
        public event BuildStatusEventHandler? StatusEventRaised;

        /// <summary>
        /// This event is raised to log that some event has
        /// occurred.  It is raised on every event.
        /// </summary>
        public event AnyEventHandler? AnyEventRaised;

        /// <summary>
        /// This event is raised to log telemetry.
        /// </summary>
        public event TelemetryEventHandler? TelemetryLogged;

        /// <summary>
        /// This event is raised to log BuildCheck events.
        /// </summary>
        internal event BuildCheckEventHandler? BuildCheckEventRaised;

        /// <summary>
        /// this event is raised to log internal telemetry data from worker nodes.
        /// </summary>
        public event WorkerNodeTelemetryEventHandler? WorkerNodeTelemetryLogged;
        #endregion

        #region Properties
        /// <summary>
        /// Provide a friendly name for the sink to make it easier to differentiate during
        /// debugging and display
        /// </summary>
        public string? Name
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
        /// Raises the given event to all registered loggers.
        /// This method casts the events extracted from the queue to a more specific type.
        /// </summary>
        /// <param name="buildEvent">BuildEventArgs</param>
        /// <param name="sinkId">Note this is not used in the eventsource sink</param>
        public void Consume(BuildEventArgs buildEvent, int sinkId)
        {
            Consume(buildEvent);
        }

        /// <summary>
        /// Raises the given event to all registered loggers.
        /// This method casts the events extracted from the queue to a more specific type.
        /// </summary>
        /// <param name="buildEvent">event args</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        public void Consume(BuildEventArgs buildEvent)
        {
            try
            {
                switch (buildEvent)
                {
                    case BuildMessageEventArgs buildMessageEvent:
                        MessageRaised?.Invoke(null, buildMessageEvent);
                        break;
                    case TaskStartedEventArgs taskStartedEvent:
                        TaskStarted?.Invoke(null, taskStartedEvent);
                        StatusEventRaised?.Invoke(null, taskStartedEvent);
                        break;
                    case TaskFinishedEventArgs taskFinishedEvent:
                        TaskFinished?.Invoke(null, taskFinishedEvent);
                        StatusEventRaised?.Invoke(null, taskFinishedEvent);
                        break;
                    case TargetStartedEventArgs targetStartedEvent:
                        TargetStarted?.Invoke(null, targetStartedEvent);
                        StatusEventRaised?.Invoke(null, targetStartedEvent);
                        break;
                    case TargetFinishedEventArgs targetFinishedEvent:
                        TargetFinished?.Invoke(null, targetFinishedEvent);
                        StatusEventRaised?.Invoke(null, targetFinishedEvent);
                        break;
                    case ProjectStartedEventArgs projectStartedEvent:
                        ProjectStarted?.Invoke(null, projectStartedEvent);
                        StatusEventRaised?.Invoke(null, projectStartedEvent);
                        break;
                    case ProjectFinishedEventArgs projectFinishedEvent:
                        ProjectFinished?.Invoke(null, projectFinishedEvent);
                        StatusEventRaised?.Invoke(null, projectFinishedEvent);
                        break;
                    case BuildStartedEventArgs buildStartedEvent:
                        HaveLoggedBuildStartedEvent = true;
                        BuildStarted?.Invoke(null, buildStartedEvent);
                        StatusEventRaised?.Invoke(null, buildStartedEvent);
                        break;
                    case BuildFinishedEventArgs buildFinishedEvent:
                        HaveLoggedBuildFinishedEvent = true;
                        BuildFinished?.Invoke(null, buildFinishedEvent);
                        StatusEventRaised?.Invoke(null, buildFinishedEvent);
                        break;
                    case BuildCanceledEventArgs buildCanceledEvent:
                        StatusEventRaised?.Invoke(null, buildCanceledEvent);
                        break;
                    case CustomBuildEventArgs customBuildEvent:
                        CustomEventRaised?.Invoke(null, customBuildEvent);
                        break;
                    case BuildStatusEventArgs buildStatusEvent:
                        StatusEventRaised?.Invoke(null, buildStatusEvent);
                        break;
                    case BuildWarningEventArgs buildWarningEvent:
                        WarningRaised?.Invoke(null, buildWarningEvent);
                        break;
                    case BuildErrorEventArgs buildErrorEvent:
                        ErrorRaised?.Invoke(null, buildErrorEvent);
                        break;
                    case TelemetryEventArgs telemetryEvent:
                        TelemetryLogged?.Invoke(null, telemetryEvent);
                        break;
                    case BuildCheckEventArgs buildCheckEvent:
                        BuildCheckEventRaised?.Invoke(null, buildCheckEvent);
                        break;
                    case WorkerNodeTelemetryEventArgs workerNodeTelemetryEvent:
                        WorkerNodeTelemetryLogged?.Invoke(null, workerNodeTelemetryEvent);
                        break;

                    default:
                        ErrorUtilities.ThrowInternalError("Unknown event args type.");
                        break;
                }
            }
            catch (LoggerException)
            {
                // if a logger has failed politely, abort immediately
                // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                // if a fellow logger is throwing in an event handler.
                UnregisterAllEventHandlers();
                throw;
            }
            catch (Exception exception) when (exception is not InternalErrorException)
            {
                // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                // if a fellow logger is throwing in an event handler.
                UnregisterAllEventHandlers();

                if (ExceptionHandling.IsCriticalException(exception))
                {
                    throw;
                }

                InternalLoggerException.Throw(exception, buildEvent, "FatalErrorWhileLogging", false);
            }

            switch (buildEvent)
            {
                case BuildMessageEventArgs:
                case TaskStartedEventArgs:
                case TaskFinishedEventArgs:
                case TargetStartedEventArgs:
                case TargetFinishedEventArgs:
                case ProjectStartedEventArgs:
                case ProjectFinishedEventArgs:
                case BuildStartedEventArgs:
                case BuildFinishedEventArgs:
                case BuildCanceledEventArgs:
                case CustomBuildEventArgs:
                case BuildStatusEventArgs:
                case BuildWarningEventArgs:
                case BuildErrorEventArgs:
                case BuildCheckEventArgs:
                    RaiseAnyEvent(buildEvent);
                    break;
                case TelemetryEventArgs:
                case WorkerNodeTelemetryEventArgs:
                    break;
                default:
                    ErrorUtilities.ThrowInternalError("Unknown event args type.");
                    break;
            }
        }

        /// <summary>
        /// Shutdown and displose of any resource this object is holding onto.
        /// </summary>
        public void ShutDown()
        {
            UnregisterAllEventHandlers();
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
            BuildCheckEventRaised = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Raises a catch-all build event to all registered loggers.
        /// Keeping it separate since it also dumps the Exception to file as opposed to all other events.
        /// </summary>
        /// <param name="buildEvent">Build EventArgs</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseAnyEvent(BuildEventArgs buildEvent)
        {
            if (AnyEventRaised != null)
            {
                try
                {
                    AnyEventRaised(null, buildEvent);
                }
                catch (LoggerException exception)
                {
                    if (ExceptionHandling.IsCriticalException(exception))
                    {
                        // if a logger has failed politely, abort immediately
                        // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                        // if a fellow logger is throwing in an event handler.
                        UnregisterAllEventHandlers();

                        // We ought to dump this further up the stack, but if for example a task is logging an event within a
                        // catch(Exception) block and not rethrowing it, there's the possibility that this exception could
                        // just get silently eaten.  So better to have duplicates than to not log the problem at all. :)
                        ExceptionHandling.DumpExceptionToFile(exception);

                        throw;
                    }
                }
                catch (Exception exception)
                {
                    // We ought to dump this further up the stack, but if for example a task is logging an event within a
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

        #endregion
        #endregion
    }
}

