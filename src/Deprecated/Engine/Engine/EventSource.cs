// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class raises events on behalf of the build engine to all registered loggers.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class EventSource : MarshalByRefObject, IEventSource
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal EventSource()
        {
            // do nothing
        }

        /// <summary>
        /// Clears out all events.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal void UnregisterAllLoggers()
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
        }

        /// <summary>
        /// Raises a message event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseMessageEvent(object sender, BuildMessageEventArgs e)
        {
            if (MessageRaised != null)
            {
                try
                {
                    MessageRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, e);
        }

        /// <summary>
        /// Raises an error event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseErrorEvent(object sender, BuildErrorEventArgs e)
        {
            if (ErrorRaised != null)
            {
                try
                {
                    ErrorRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, e);
        }

        /// <summary>
        /// Raises a warning event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseWarningEvent(object sender, BuildWarningEventArgs e)
        {
            if (WarningRaised != null)
            {
                try
                {
                    WarningRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, e);
        }

        /// <summary>
        /// Raises a "build started" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseBuildStartedEvent(object sender, BuildStartedEventArgs e)
        {
            if (BuildStarted != null)
            {
                try
                {
                    BuildStarted(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "build finished" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseBuildFinishedEvent(object sender, BuildFinishedEventArgs e)
        {
            if (BuildFinished != null)
            {
                try
                {
                    BuildFinished(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "project build started" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseProjectStartedEvent(object sender, ProjectStartedEventArgs e)
        {
            if (ProjectStarted != null)
            {
                try
                {
                    ProjectStarted(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "project build finished" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseProjectFinishedEvent(object sender, ProjectFinishedEventArgs e)
        {
            if (ProjectFinished != null)
            {
                try
                {
                    ProjectFinished(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "target build started" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseTargetStartedEvent(object sender, TargetStartedEventArgs e)
        {
            if (TargetStarted != null)
            {
                try
                {
                    TargetStarted(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "target build finished" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseTargetFinishedEvent(object sender, TargetFinishedEventArgs e)
        {
            if (TargetFinished != null)
            {
                try
                {
                    TargetFinished(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "task execution started" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseTaskStartedEvent(object sender, TaskStartedEventArgs e)
        {
            if (TaskStarted != null)
            {
                try
                {
                    TaskStarted(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a "task finished executing" event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseTaskFinishedEvent(object sender, TaskFinishedEventArgs e)
        {
            if (TaskFinished != null)
            {
                try
                {
                    TaskFinished(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseStatusEvent(sender, e);
        }

        /// <summary>
        /// Raises a custom event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseCustomEvent(object sender, CustomBuildEventArgs e)
        {
            if (CustomEventRaised != null)
            {
                try
                {
                    CustomEventRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, e);
        }

        /// <summary>
        /// Raises a catch-all build status event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseStatusEvent(object sender, BuildStatusEventArgs e)
        {
            if (StatusEventRaised != null)
            {
                try
                {
                    StatusEventRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }

            RaiseAnyEvent(sender, e);
        }

        /// <summary>
        /// Raises a catch-all build event to all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void RaiseAnyEvent(object sender, BuildEventArgs e)
        {
            if (AnyEventRaised != null)
            {
                try
                {
                    AnyEventRaised(sender, e);
                }
                catch (LoggerException)
                {
                    // if a logger has failed politely, abort immediately
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    throw;
                }
                catch (Exception exception)
                {
                    // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                    // if a fellow logger is throwing in an event handler.
                    this.UnregisterAllLoggers();
                    InternalLoggerException.Throw(exception, e, "FatalErrorWhileLogging", false);
                }
            }
        }

        /// <summary>
        /// Raises the given event to all registered loggers.
        /// This method casts the events extracted from the queue to a more specific type.
        /// </summary>
        /// <param name="e"></param>
        internal void RaiseStronglyTypedEvent(BuildEventArgs e)
        {
            switch (e)
            {
                case BuildMessageEventArgs buildMessageEvent:
                    RaiseMessageEvent(null, buildMessageEvent);
                    break;
                case TaskStartedEventArgs taskStartedEvent:
                    RaiseTaskStartedEvent(null, taskStartedEvent);
                    break;
                case TaskFinishedEventArgs taskFinishedEvent:
                    RaiseTaskFinishedEvent(null, taskFinishedEvent);
                    break;
                case TargetStartedEventArgs targetStartedEvent:
                    RaiseTargetStartedEvent(null, targetStartedEvent);
                    break;
                case TargetFinishedEventArgs targetFinishedEvent:
                    RaiseTargetFinishedEvent(null, targetFinishedEvent);
                    break;
                case ProjectStartedEventArgs projectStartedEvent:
                    RaiseProjectStartedEvent(null, projectStartedEvent);
                    break;
                case ProjectFinishedEventArgs projectFinishedEvent:
                    RaiseProjectFinishedEvent(null, projectFinishedEvent);
                    break;
                case BuildStartedEventArgs buildStartedEvent:
                    RaiseBuildStartedEvent(null, buildStartedEvent);
                    break;
                case BuildFinishedEventArgs buildFinishedEvent:
                    RaiseBuildFinishedEvent(null, buildFinishedEvent);
                    break;
                case CustomBuildEventArgs customBuildEvent:
                    RaiseCustomEvent(null, customBuildEvent);
                    break;
                case BuildStatusEventArgs buildStatusEvent:
                    RaiseStatusEvent(null, buildStatusEvent);
                    break;
                case BuildWarningEventArgs buildWarningEvent:
                    RaiseWarningEvent(null, buildWarningEvent);
                    break;
                case BuildErrorEventArgs buildErrorEvent:
                    RaiseErrorEvent(null, buildErrorEvent);
                    break;
                default:
                    ErrorUtilities.VerifyThrow(false, "Unknown event args type.");
                    break;
            }
        }

        /// <summary>
        /// This event is raised to log a message.
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildMessageEventHandler MessageRaised;

        /// <summary>
        /// This event is raised to log an error.
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildErrorEventHandler ErrorRaised;

        /// <summary>
        /// This event is raised to log a warning.
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildWarningEventHandler WarningRaised;

        /// <summary>
        /// this event is raised to log the start of a build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildStartedEventHandler BuildStarted;

        /// <summary>
        /// this event is raised to log the end of a build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildFinishedEventHandler BuildFinished;

        /// <summary>
        /// this event is raised to log the start of a project build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event ProjectStartedEventHandler ProjectStarted;

        /// <summary>
        /// this event is raised to log the end of a project build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event ProjectFinishedEventHandler ProjectFinished;

        /// <summary>
        /// this event is raised to log the start of a target build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event TargetStartedEventHandler TargetStarted;

        /// <summary>
        /// this event is raised to log the end of a target build
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event TargetFinishedEventHandler TargetFinished;

        /// <summary>
        /// this event is raised to log the start of task execution
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event TaskStartedEventHandler TaskStarted;

        /// <summary>
        /// this event is raised to log the end of task execution
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event TaskFinishedEventHandler TaskFinished;

        /// <summary>
        /// this event is raised to log a custom event
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event CustomBuildEventHandler CustomEventRaised;

        /// <summary>
        /// this event is raised to log build status events, such as
        /// build/project/target/task started/stopped
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event BuildStatusEventHandler StatusEventRaised;

        /// <summary>
        /// This event is raised to log that some event has
        /// occurred.  It is raised on every event.
        /// </summary>
        /// <owner> t-jeffv, sumedhk </owner>
        public event AnyEventHandler AnyEventRaised;
    }
}
