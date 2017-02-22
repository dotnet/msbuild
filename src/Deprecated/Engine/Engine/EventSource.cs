// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Raises the given event to all registered loggers. This method up-cast the events
        /// extracted from the queue.
        /// </summary>
        /// <param name="e"></param>
        internal void RaiseStronglyTypedEvent(BuildEventArgs e)
        {
            // FXCop may complain that there are unecessary casts here, and there are, but
            // using "as" and allocating another variable for each event is extremely costly
            // and is much slower then this approach even with the additional casts
            if (e is BuildMessageEventArgs)
                this.RaiseMessageEvent(null, (BuildMessageEventArgs)e);
            else if (e is TaskStartedEventArgs)
                this.RaiseTaskStartedEvent(null, (TaskStartedEventArgs)e);
            else if (e is TaskFinishedEventArgs)
                this.RaiseTaskFinishedEvent(null, (TaskFinishedEventArgs)e);
            else if (e is TargetStartedEventArgs)
                this.RaiseTargetStartedEvent(null, (TargetStartedEventArgs)e);
            else if (e is TargetFinishedEventArgs)
                this.RaiseTargetFinishedEvent(null, (TargetFinishedEventArgs)e);
            else if (e is ProjectStartedEventArgs)
                this.RaiseProjectStartedEvent(null, (ProjectStartedEventArgs)e);
            else if (e is ProjectFinishedEventArgs)
                this.RaiseProjectFinishedEvent(null, (ProjectFinishedEventArgs)e);
            else if (e is BuildStartedEventArgs)
                this.RaiseBuildStartedEvent(null, (BuildStartedEventArgs)e);
            else if (e is BuildFinishedEventArgs)
                this.RaiseBuildFinishedEvent(null, (BuildFinishedEventArgs)e);
            else if (e is CustomBuildEventArgs)
                this.RaiseCustomEvent(null, (CustomBuildEventArgs)e);
            else if (e is BuildStatusEventArgs)
                this.RaiseStatusEvent(null, (BuildStatusEventArgs)e);
            else if (e is BuildWarningEventArgs)
                this.RaiseWarningEvent(null, (BuildWarningEventArgs)e);
            else if (e is BuildErrorEventArgs)
                this.RaiseErrorEvent(null, (BuildErrorEventArgs)e);
            else
                ErrorUtilities.VerifyThrow(false, "Unknown event args type.");
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
