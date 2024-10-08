// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;
using static Microsoft.Build.BackEnd.Logging.EventSourceSink;

#nullable disable

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

        /// <summary>
        /// This event is raised to log BuildCheck events.
        /// </summary>
        internal event BuildCheckEventHandler BuildCheckEventRaised;
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
        public void Consume(BuildEventArgs buildEvent)
        {

            switch (buildEvent)
            {
                case BuildMessageEventArgs buildMessageEvent:
                    RaiseEvent(null, buildMessageEvent, (o, args) => MessageRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case TaskStartedEventArgs taskStartedEvent:
                    RaiseEvent(null, taskStartedEvent, (o, args) => TaskStarted?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case TaskFinishedEventArgs taskFinishedEvent:
                    RaiseEvent(null, taskFinishedEvent, (o, args) => TaskFinished?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case TargetStartedEventArgs targetStartedEvent:
                    RaiseEvent(null, targetStartedEvent, (o, args) => TargetStarted?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case TargetFinishedEventArgs targetFinishedEvent:
                    RaiseEvent(null, targetFinishedEvent, (o, args) => TargetFinished?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case ProjectStartedEventArgs projectStartedEvent:
                    RaiseEvent(null, projectStartedEvent, (o, args) => ProjectStarted?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case ProjectFinishedEventArgs projectFinishedEvent:
                    RaiseEvent(null, projectFinishedEvent, (o, args) => ProjectFinished?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case BuildStartedEventArgs buildStartedEvent:
                    HaveLoggedBuildStartedEvent = true;
                    RaiseEvent(null, buildStartedEvent, (o, args) => BuildStarted?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case BuildFinishedEventArgs buildFinishedEvent:
                    HaveLoggedBuildFinishedEvent = true;
                    RaiseEvent(null, buildFinishedEvent, (o, args) => BuildFinished?.Invoke(o, args), (o, args) => StatusEventRaised?.Invoke(o, args));
                    break;
                case BuildCanceledEventArgs buildCanceledEvent:

                    RaiseEvent(null, buildCanceledEvent, (o, args) => StatusEventRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case CustomBuildEventArgs customBuildEvent:
                    RaiseEvent(null, customBuildEvent, (o, args) => CustomEventRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case BuildStatusEventArgs buildStatusEvent:
                    RaiseEvent(null, buildStatusEvent, (o, args) => StatusEventRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case BuildWarningEventArgs buildWarningEvent:
                    RaiseEvent(null, buildWarningEvent, (o, args) => WarningRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case BuildErrorEventArgs buildErrorEvent:
                    RaiseEvent(null, buildErrorEvent, (o, args) => ErrorRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
                    break;
                case TelemetryEventArgs telemetryEvent:
                    RaiseEvent(null, telemetryEvent, (o, args) => TelemetryLogged?.Invoke(o, args), null);
                    break;
                case BuildCheckEventArgs buildCheckEvent:
                    RaiseEvent(null, buildCheckEvent, (o, args) => BuildCheckEventRaised?.Invoke(o, args), (o, args) => AnyEventRaised?.Invoke(o, args));
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
            BuildCheckEventRaised = null;
        }

        #endregion

        #region Private Methods

        public delegate void ArgsHandler<in TArgs>(object sender, TArgs e) where TArgs : BuildEventArgs;

        /// <summary>
        /// Raises a message event to all registered loggers.
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="buildEvent">event args</param>
        /// <param name="handler">argument handler that invokes the respective event</param>
        /// <param name="followUp"> either anyEvent, customEvent or statusEvent</param>
        /// <exception cref="LoggerException">When EventHandler raises an logger exception the LoggerException is rethrown</exception>
        /// <exception cref="InternalLoggerException">Any exceptions which are not LoggerExceptions are wrapped in an InternalLoggerException</exception>
        /// <exception cref="Exception">ExceptionHandling.IsCriticalException exceptions will not be wrapped</exception>
        private void RaiseEvent<TArgs>(object sender, TArgs buildEvent, ArgsHandler<TArgs> handler, ArgsHandler<TArgs> followUp)
            where TArgs : BuildEventArgs
        {
            try
            {
                handler(sender, buildEvent);
            }
            catch (LoggerException)
            {
                // if a logger has failed politely, abort immediately
                // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                // if a fellow logger is throwing in an event handler.
                UnregisterAllEventHandlers();
                throw;
            }
            catch (Exception exception)
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

            followUp?.Invoke(sender, buildEvent);
        }

        #endregion
        #endregion
    }
}
