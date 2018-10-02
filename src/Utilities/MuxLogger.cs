// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This is a multiplexing logger. The purpose of this logger is to allow the registration and deregistration of 
    /// multiple loggers during the build. This is to support the VS IDE scenario where loggers are registered and unregistered
    /// for each project system's build request. This means one physical build may have multiple logical builds
    /// each with their own set of loggers. 
    /// 
    /// The Mux logger will register itself with the build manager as a regular central /l style logger. 
    /// It will be responsible for receiving messages from the build manager and route them to the correct
    /// logger based on the logical build the message came from.
    /// 
    /// Requirements:
    ///     1) Multiplexing logger will be registered at the beginning of the build manager's Begin build
    ///         Any loggers registered before the build manager actually started building will get the build started event at the same time as the MUX logger
    ///         Any loggers registered after the build manager starts the build will get a synthesised build started event. The event cannot be cached because the 
    ///         timestamp of the build started event is determined when the event is created, caching the event would give incorrect build times in the loggers registered to the MUX.
    ///         
    ///     2) The MUX logger will be initialized by the build manager.
    ///         The mux will listen to all events on the event source from the build manager and will route events correctly to the registered loggers.
    ///     
    ///     3) The MUX logger will be shutdown when the build is finished in end build . At this time it will un-register any loggers attached to it.
    ///     
    ///     4) The MUX logger will log the build finished event when the project finished event for the first project started event is seen for each logger.
    ///    
    /// Registering Loggers:
    /// 
    /// The multiplexing logger will function in the following way:
    ///     A logger will be passed to the MUX Register logger method with a submission ID which will be used to route a the message to the correct logger.
    ///     A new event source will be created so that the logger passed in can be registered to that event source
    ///     If the build started event has already been logged the MUX logger will create a new BuildStartedEvent and send that to the event source.
    ///     
    /// UnregisterLoggers:
    ///     When a build submission is completed the UnregisterLoggers method will be called with the submission ID.
    ///     At this point we will look up the success state of the project finished event for the submission ID and log a build finished event to the logger.
    ///     The event source will be cleaned up.  This may be interesting because the unregister will come from a thread other than what is doing the logging.
    ///     This may create a Synchronization issue, if unregister is called while events are being logged.
    ///     
    /// UNDONE: If we can use ErrorUtilities, replace all InvalidOperation and Argument exceptions with the appropriate calls.
    /// 
    /// </summary>
    public class MuxLogger : INodeLogger
    {
        /// <summary>
        /// The mapping of submission IDs to the submission record.
        /// </summary>
        private readonly Dictionary<int, SubmissionRecord> _submissionRecords = new Dictionary<int, SubmissionRecord>();

        /// <summary>
        ///  Keep the build started event if it has been seen, we need the message off it.
        /// </summary>
        private BuildStartedEventArgs _buildStartedEvent;

        /// <summary>
        /// Event source which events from the build manager will be raised on.
        /// </summary>
        private IEventSource _eventSourceForBuild;

        /// <summary>
        /// The handler for the build started event
        /// </summary>
        private readonly BuildStartedEventHandler _buildStartedEventHandler;

        /// <summary>
        /// The handler for the build finished event.
        /// </summary>
        private readonly BuildFinishedEventHandler _buildFinishedEventHandler;

        /// <summary>
        /// The handler for the project started event.
        /// </summary>
        private readonly ProjectStartedEventHandler _projectStartedEventHandler;

        /// <summary>
        /// The handler for the project finished event.
        /// </summary>
        private readonly ProjectFinishedEventHandler _projectFinishedEventHandler;

        /// <summary>
        /// Dictionary mapping submission id to projects in progress.
        /// </summary>
        private readonly Dictionary<int, int> _submissionProjectsInProgress = new Dictionary<int, int>();

        /// <summary>
        /// The maximum node count as specified in the call to Initialize()
        /// </summary>
        private int _maxNodeCount = 1;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MuxLogger()
        {
            _buildStartedEventHandler = BuildStarted;
            _buildFinishedEventHandler = BuildFinished;
            _projectStartedEventHandler = ProjectStarted;
            _projectFinishedEventHandler = ProjectFinished;
        }

        /// <summary>
        /// Required for ILogger interface
        /// </summary>
        public LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// Required for the ILoggerInterface
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Should evaluation events include generated metaprojects?
        /// </summary>
        public bool IncludeEvaluationMetaprojects { get; set; }

        /// <summary>
        /// Should evaluation events include profiling information?
        /// </summary>
        public bool IncludeEvaluationProfiles { get; set; }

        /// <summary>
        /// Should task events include task inputs?
        /// </summary>
        public bool IncludeTaskInputs { get; set; }

        /// <summary>
        /// Initialize the logger.
        /// </summary>
        public void Initialize(IEventSource eventSource) => Initialize(eventSource, 1);

        /// <summary>
        /// Initialize the logger.
        /// </summary>
        public void Initialize(IEventSource eventSource, int maxNodeCount)
        {
            if (_eventSourceForBuild != null)
            {
                throw new InvalidOperationException("MuxLogger already initialized.");
            }

            _eventSourceForBuild = eventSource;
            _maxNodeCount = maxNodeCount;

            _eventSourceForBuild.BuildStarted += _buildStartedEventHandler;
            _eventSourceForBuild.BuildFinished += _buildFinishedEventHandler;
            _eventSourceForBuild.ProjectStarted += _projectStartedEventHandler;
            _eventSourceForBuild.ProjectFinished += _projectFinishedEventHandler;

            if (_eventSourceForBuild is IEventSource3 eventSource3)
            {
                if (IncludeEvaluationMetaprojects)
                {
                    eventSource3.IncludeEvaluationMetaprojects();
                }
                if (IncludeEvaluationProfiles)
                {
                    eventSource3.IncludeEvaluationProfiles();
                }

                if (IncludeTaskInputs)
                {
                    eventSource3.IncludeTaskInputs();
                }
            }
        }

        /// <summary>
        /// Shutdown the mux logger and clear out any state
        /// </summary>
        public void Shutdown()
        {
            if (_eventSourceForBuild == null)
            {
                throw new InvalidOperationException("MuxLogger not initialized.");
            }

            // Go through ALL loggers and shutdown any which remain.
            List<SubmissionRecord> recordsToShutdown;
            lock (_submissionRecords)
            {
                recordsToShutdown = new List<SubmissionRecord>(_submissionRecords.Values);
                _submissionRecords.Clear();
            }

            foreach (SubmissionRecord record in recordsToShutdown)
            {
                record.Shutdown();
            }

            _eventSourceForBuild.ProjectStarted -= _projectStartedEventHandler;
            _eventSourceForBuild.ProjectFinished -= _projectFinishedEventHandler;
            _eventSourceForBuild.BuildStarted -= _buildStartedEventHandler;
            _eventSourceForBuild.BuildFinished -= _buildFinishedEventHandler;

            _submissionProjectsInProgress.Clear();
            _buildStartedEvent = null;
            _eventSourceForBuild = null;
        }

        /// <summary>
        /// This method will register a logger on the MUX logger and then raise a build started event if the build started event has already been logged
        /// </summary>
        public void RegisterLogger(int submissionId, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (_eventSourceForBuild == null)
            {
                throw new InvalidOperationException("Cannot register a logger before the MuxLogger has been initialized.");
            }

            if (_submissionProjectsInProgress.ContainsKey(submissionId))
            {
                throw new InvalidOperationException("Cannot register a logger for a submission once it has started.");
            }

            // See if another logger has been registered already with the same submission ID
            SubmissionRecord record;
            lock (_submissionRecords)
            {
                if (!_submissionRecords.TryGetValue(submissionId, out record))
                {
                    record = new SubmissionRecord(submissionId, _eventSourceForBuild, _buildStartedEvent, _maxNodeCount);
                    _submissionRecords.Add(submissionId, record);
                }
            }

            record.AddLogger(logger);
        }

        /// <summary>
        /// Unregisters all the loggers for a given submission id.
        /// </summary>
        public bool UnregisterLoggers(int submissionId)
        {
            SubmissionRecord record;
            lock (_submissionRecords)
            {
                if (!_submissionRecords.TryGetValue(submissionId, out record))
                {
                    return false;
                }

                _submissionRecords.Remove(submissionId);
            }

            record.Shutdown();
            return true;
        }

        /// <summary>
        /// Receives the build started event for the whole build.
        /// </summary>
        private void BuildStarted(object sender, BuildStartedEventArgs e)
        {
            _buildStartedEvent = e;
            lock (_submissionRecords)
            {
                foreach (SubmissionRecord record in _submissionRecords.Values)
                {
                    record.SetGlobalBuildStartedEvent(e);
                }
            }
        }

        /// <summary>
        /// Receives the build finished event.
        /// </summary>
        private void BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            _buildStartedEvent = null;
        }

        /// <summary>
        /// Receives the project started event and records the submission as being in-progress.
        /// </summary>
        private void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            _submissionProjectsInProgress.TryGetValue(e.BuildEventContext.SubmissionId, out int value);
            _submissionProjectsInProgress[e.BuildEventContext.SubmissionId] = value + 1;
        }

        /// <summary>
        /// Receives the project finished event.
        /// </summary>
        private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            int value = _submissionProjectsInProgress[e.BuildEventContext.SubmissionId];

            if (value == 1)
            {
                _submissionProjectsInProgress.Remove(e.BuildEventContext.SubmissionId);
                lock (_submissionRecords)
                {
                    if (_submissionRecords.ContainsKey(e.BuildEventContext.SubmissionId))
                    {
                        _submissionRecords.Remove(e.BuildEventContext.SubmissionId);
                    }
                }
            }
            else
            {
                _submissionProjectsInProgress[e.BuildEventContext.SubmissionId] = value - 1;
            }
        }

        /// <summary>
        /// This class holds everything the logger needs to know about a particular submission, including the event source.
        /// </summary>
        private class SubmissionRecord :
#if FEATURE_APPDOMAIN
            MarshalByRefObject,
#endif
            IEventSource2
        {
            #region Fields
            /// <summary>
            /// Object used to synchronize access to internals.
            /// </summary>
            private readonly object _syncLock = new object();

            /// <summary>
            /// List of loggers
            /// </summary>
            private readonly List<ILogger> _loggers;

            /// <summary>
            /// The maximum node count
            /// </summary>
            private readonly int _maxNodeCount;

            /// <summary>
            /// The event source which will have events raised from the buld manager.
            /// </summary>
            private readonly IEventSource _eventSourceForBuild;

            /// <summary>
            /// The buildStartedEvent to use when synthesizing the build started event.
            /// </summary>
            private BuildStartedEventArgs _buildStartedEvent;

            /// <summary>
            /// The project build event coontext for the first project started event seen, this is the root of the submission.
            /// </summary>
            private BuildEventContext _firstProjectStartedEventContext;

            /// <summary>
            /// SubmissionId for this submission record
            /// </summary>
            private readonly int _submissionId;

            /// <summary>
            /// Has the record been shutdown yet.
            /// </summary>
            private bool _shutdown;
            #endregion

            // Keep instance of event handlers so they can be unregistered at the end of the submissionID. 
            // If we wait for the entire build to finish we will leak the handlers until we unregister ALL of the handlers from the 
            // event source on the build manager.
            #region RegisteredHandlers
            /// <summary>
            /// Even hander for "anyEvent" this is a handler which will be called from each of the other event handlers
            /// </summary>
            private AnyEventHandler _anyEventHandler;

            /// <summary>
            /// Handle the Build Finished event
            /// </summary>
            private BuildFinishedEventHandler _buildFinishedEventHandler;

            /// <summary>
            /// Handle the Build started event
            /// </summary>
            private BuildStartedEventHandler _buildStartedEventHandler;

            /// <summary>
            /// Handle custom build events
            /// </summary>
            private CustomBuildEventHandler _customBuildEventHandler;

            /// <summary>
            /// Handle error events
            /// </summary>
            private BuildErrorEventHandler _buildErrorEventHandler;

            /// <summary>
            /// Handle message events
            /// </summary>
            private BuildMessageEventHandler _buildMessageEventHandler;

            /// <summary>
            /// Handle project finished events
            /// </summary>
            private ProjectFinishedEventHandler _projectFinishedEventHandler;

            /// <summary>
            /// Handle project started events
            /// </summary>
            private ProjectStartedEventHandler _projectStartedEventHandler;

            /// <summary>
            /// Handle build sttus events
            /// </summary>
            private BuildStatusEventHandler _buildStatusEventHandler;

            /// <summary>
            /// Handle target finished events
            /// </summary>
            private TargetFinishedEventHandler _targetFinishedEventHandler;

            /// <summary>
            /// Handle target started events
            /// </summary>
            private TargetStartedEventHandler _targetStartedEventHandler;

            /// <summary>
            /// Handle task finished
            /// </summary>
            private TaskFinishedEventHandler _taskFinishedEventHandler;

            /// <summary>
            /// Handle task started
            /// </summary>
            private TaskStartedEventHandler _taskStartedEventHandler;

            /// <summary>
            /// Handle warning events
            /// </summary>
            private BuildWarningEventHandler _buildWarningEventHandler;

            /// <summary>
            /// Handle telemetry events.
            /// </summary>
            private TelemetryEventHandler _telemetryEventHandler;

            #endregion

            /// <summary>
            /// Constructor.
            /// </summary>
            internal SubmissionRecord(int submissionId, IEventSource buildEventSource, BuildStartedEventArgs buildStartedEvent, int maxNodeCount)
            {
                _maxNodeCount = maxNodeCount;
                _submissionId = submissionId;
                _buildStartedEvent = buildStartedEvent;
                _eventSourceForBuild = buildEventSource;
                _loggers = new List<ILogger>();
                InitializeInternalEventSource();
            }

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
            /// This event is raised when telemetry is sent.
            /// </summary>
            public event TelemetryEventHandler TelemetryLogged;

            #endregion

            #region Internal Methods
            /// <summary>
            /// Adds the specified logger to the set of loggers for this submission.
            /// </summary>
            internal void AddLogger(ILogger logger)
            {
                lock (_syncLock)
                {
                    if (_loggers.Contains(logger))
                    {
                        throw new InvalidOperationException("Cannot register the same logger twice.");
                    }

                    // Node loggers are central /l loggers which can understand how many CPU's the build is running with, they are only different in that
                    // they can take a number of CPU
                    if (logger is INodeLogger nodeLogger)
                    {
                        nodeLogger.Initialize(this, _maxNodeCount);
                    }
                    else
                    {
                        logger.Initialize(this);
                    }

                    _loggers.Add(logger);
                }
            }

            /// <summary>
            /// Shuts down the loggers and removes them
            /// </summary>
            internal void Shutdown()
            {
                lock (_syncLock)
                {
                    if (!_shutdown)
                    {
                        _shutdown = true;
                        _firstProjectStartedEventContext = null;

                        UnregisterAllEventHandlers();

                        foreach (ILogger logger in _loggers)
                        {
                            logger.Shutdown();
                        }

                        _loggers.Clear();
                    }
                }
            }

            /// <summary>
            /// Sets the build started event for this event source if it hasn't already been set.
            /// </summary>
            internal void SetGlobalBuildStartedEvent(BuildStartedEventArgs buildStartedEvent)
            {
                lock (_syncLock)
                {
                    if (_buildStartedEvent == null)
                    {
                        _buildStartedEvent = buildStartedEvent;
                    }
                }
            }

            /// <summary>
            /// Raises a message event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildMessageEventArgs</param>
            private void RaiseMessageEvent(object sender, BuildMessageEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises an error event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildErrorEventArgs</param>
            private void RaiseErrorEvent(object sender, BuildErrorEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    if (
                        buildEvent.BuildEventContext != null &&
                        (
                         buildEvent.BuildEventContext.SubmissionId != _submissionId && /* The build submission does not match the submissionId for this logger */
                         buildEvent.BuildEventContext.SubmissionId != BuildEventContext.InvalidSubmissionId /*We do not have a build submissionid this can happen if the error comes from the nodeloggingcontext*/
                        )
                       )
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a warning event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildWarningEventArgs</param>
            private void RaiseWarningEvent(object sender, BuildWarningEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    if (
                        buildEvent.BuildEventContext != null &&
                        (
                         buildEvent.BuildEventContext.SubmissionId != _submissionId && /* The build submission does not match the submissionId for this logger */
                         buildEvent.BuildEventContext.SubmissionId != BuildEventContext.InvalidSubmissionId /*We do not have a build submissionid this can happen if the error comes from the nodeloggingcontext*/
                        )
                       )
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "build started" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildStartedEventArgs</param>
            private void RaiseBuildStartedEvent(object sender, BuildStartedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If we receive a REAL build started event, ignore it.  We only want the one we get as a result of the project started event
                    if (_firstProjectStartedEventContext == null)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }

                    RaiseStatusEvent(sender, buildEvent, true /* cascade to AnyEvent */);
                }
            }

            /// <summary>
            /// Raises a "build finished" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildFinishedEventArgs</param>
            private void RaiseBuildFinishedEvent(object sender, BuildFinishedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If we already did the build finished event (synthesized from project finished), we don't want to do it again if we happen
                    // to still be registered when the REAL build finished event comes through.
                    if (_firstProjectStartedEventContext == null)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }

                    RaiseStatusEvent(sender, buildEvent, true /* cascade to AnyEvent */);
                }
            }

            /// <summary>
            /// Raises a "project build started" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">ProjectStartedEventArgs</param>
            private void RaiseProjectStartedEvent(object sender, ProjectStartedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

                    if (_firstProjectStartedEventContext == null)
                    {
                        // Capture the build event context for the first project started event so we can make sure we know when to fire the 
                        // build finished event (in the case of loggers on the mux logger this is on the last project finished event for the submission
                        _firstProjectStartedEventContext = buildEvent.BuildEventContext;

                        // We've never seen a project started event, so raise the build started event and save this project started event.
                        BuildStartedEventArgs startedEvent = new BuildStartedEventArgs(_buildStartedEvent.Message, _buildStartedEvent.HelpKeyword, _buildStartedEvent.BuildEnvironment);
                        RaiseBuildStartedEvent(sender, startedEvent);
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "project build finished" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">ProjectFinishedEventArgs</param>
            private void RaiseProjectFinishedEvent(object sender, ProjectFinishedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "target build started" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">TargetStartedEventArgs</param>
            private void RaiseTargetStartedEvent(object sender, TargetStartedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "target build finished" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">TargetFinishedEventArgs</param>
            private void RaiseTargetFinishedEvent(object sender, TargetFinishedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "task execution started" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">TaskStartedEventArgs</param>
            private void RaiseTaskStartedEvent(object sender, TaskStartedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a "task finished executing" event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">TaskFinishedEventArgs</param>
            private void RaiseTaskFinishedEvent(object sender, TaskFinishedEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a custom event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">CustomBuildEventArgs</param>
            private void RaiseCustomEvent(object sender, CustomBuildEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// Raises a catch-all build status event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">BuildStatusEventArgs</param>
            private void RaiseStatusEvent(object sender, BuildStatusEventArgs buildEvent) => RaiseStatusEvent(sender, buildEvent, false);

            /// <summary>
            /// Raises a status event, optionally cascading to an any event.
            /// </summary>
            private void RaiseStatusEvent(object sender, BuildStatusEventArgs buildEvent, bool cascade)
            {
                lock (_syncLock)
                {
                    // If the event does not have the submissionId for our loggers then drop it.
                    if (buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.SubmissionId != _submissionId)
                    {
                        return;
                    }

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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }

                    if (cascade)
                    {
                        RaiseAnyEvent(sender, buildEvent);
                    }
                }
            }

            /// <summary>
            /// Raises a catch-all build event to all registered loggers.
            /// </summary>
            /// <param name="sender">sender of the event</param>
            /// <param name="buildEvent">Build EventArgs</param>
            private void RaiseAnyEvent(object sender, BuildEventArgs buildEvent)
            {
                lock (_syncLock)
                {
                    bool eventIsErrorOrWarning = (buildEvent is BuildWarningEventArgs) || (buildEvent is BuildErrorEventArgs);

                    if (
                        buildEvent.BuildEventContext != null &&
                        (
                         buildEvent.BuildEventContext.SubmissionId != _submissionId && /* The build submission does not match the submissionId for this logger */
                         !( /* We do not have a build submissionid this can happen if the event comes from the nodeloggingcontext -- but we only want to raise it if it was an error or warning */
                           buildEvent.BuildEventContext.SubmissionId == BuildEventContext.InvalidSubmissionId && eventIsErrorOrWarning
                          )
                        )
                       )
                    {
                        return;
                    }

                    // If we receive a REAL build started or finished event, ignore it.  We only want the one we get as a result of the project started and finished events
                    if (_firstProjectStartedEventContext == null && (buildEvent is BuildStartedEventArgs || buildEvent is BuildFinishedEventArgs))
                    {
                        return;
                    }

                    if (AnyEventRaised != null)
                    {
                        try
                        {
                            AnyEventRaised(sender, buildEvent);
                        }
                        catch (LoggerException)
                        {
                            // if a logger has failed politely, abort immediately
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();

                            throw;
                        }
                    }

                    // If this project finished event matches our first project started event, then send build finished.
                    // Because of the way the event source works, we actually have to process this here rather than in project finished because if the 
                    // logger is registered without a ProjectFinished handler, but does have an Any handler (as the mock logger does) then we would end up
                    // sending the BuildFinished event before the ProjectFinished event got processed in the Any handler.
                    ProjectFinishedEventArgs projectFinishedEvent = buildEvent as ProjectFinishedEventArgs;
                    if (projectFinishedEvent != null && buildEvent.BuildEventContext != null && buildEvent.BuildEventContext.Equals(_firstProjectStartedEventContext))
                    {
                        string message = projectFinishedEvent.Succeeded ? ResourceUtilities.GetResourceString("MuxLogger_BuildFinishedSuccess") : ResourceUtilities.GetResourceString("MuxLogger_BuildFinishedFailure");
                        RaiseBuildFinishedEvent(sender, new BuildFinishedEventArgs(message, null, projectFinishedEvent.Succeeded));
                        Shutdown();
                    }
                }
            }

            /// <summary>
            /// Raises a telemetry event to all registered loggers.
            /// </summary>
            private void RaiseTelemetryEvent(object sender, TelemetryEventArgs buildEvent)
            {
                lock (_syncLock)
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
                            UnregisterAllEventHandlers();
                            throw;
                        }
                        catch (Exception)
                        {
                            // first unregister all loggers, since other loggers may receive remaining events in unexpected orderings
                            // if a fellow logger is throwing in an event handler.
                            UnregisterAllEventHandlers();
                        }
                    }
                }
            }
            #endregion

            #region private methods
            /// <summary>
            /// Initialize the internal event source which is used to raise events on loggers registered to this submission
            /// </summary>
            private void InitializeInternalEventSource()
            {
                _anyEventHandler = RaiseAnyEvent;
                _buildFinishedEventHandler = RaiseBuildFinishedEvent;
                _buildStartedEventHandler = RaiseBuildStartedEvent;
                _customBuildEventHandler = RaiseCustomEvent;
                _buildErrorEventHandler = RaiseErrorEvent;
                _buildMessageEventHandler = RaiseMessageEvent;
                _projectFinishedEventHandler = RaiseProjectFinishedEvent;
                _projectStartedEventHandler = RaiseProjectStartedEvent;
                _buildStatusEventHandler = RaiseStatusEvent;
                _targetFinishedEventHandler = RaiseTargetFinishedEvent;
                _targetStartedEventHandler = RaiseTargetStartedEvent;
                _taskFinishedEventHandler = RaiseTaskFinishedEvent;
                _taskStartedEventHandler = RaiseTaskStartedEvent;
                _buildWarningEventHandler = RaiseWarningEvent;
                _telemetryEventHandler = RaiseTelemetryEvent;

                _eventSourceForBuild.AnyEventRaised += _anyEventHandler;
                _eventSourceForBuild.BuildFinished += _buildFinishedEventHandler;
                _eventSourceForBuild.BuildStarted += _buildStartedEventHandler;
                _eventSourceForBuild.CustomEventRaised += _customBuildEventHandler;
                _eventSourceForBuild.ErrorRaised += _buildErrorEventHandler;
                _eventSourceForBuild.MessageRaised += _buildMessageEventHandler;
                _eventSourceForBuild.ProjectFinished += _projectFinishedEventHandler;
                _eventSourceForBuild.ProjectStarted += _projectStartedEventHandler;
                _eventSourceForBuild.StatusEventRaised += _buildStatusEventHandler;
                _eventSourceForBuild.TargetFinished += _targetFinishedEventHandler;
                _eventSourceForBuild.TargetStarted += _targetStartedEventHandler;
                _eventSourceForBuild.TaskFinished += _taskFinishedEventHandler;
                _eventSourceForBuild.TaskStarted += _taskStartedEventHandler;
                _eventSourceForBuild.WarningRaised += _buildWarningEventHandler;

                if (_eventSourceForBuild is IEventSource2 eventSource2)
                {
                    eventSource2.TelemetryLogged += _telemetryEventHandler;
                }
            }

            /// <summary>
            /// Clears out all events.
            /// </summary>
            private void UnregisterAllEventHandlers()
            {
                _eventSourceForBuild.AnyEventRaised -= _anyEventHandler;
                _eventSourceForBuild.BuildFinished -= _buildFinishedEventHandler;
                _eventSourceForBuild.BuildStarted -= _buildStartedEventHandler;
                _eventSourceForBuild.CustomEventRaised -= _customBuildEventHandler;
                _eventSourceForBuild.ErrorRaised -= _buildErrorEventHandler;
                _eventSourceForBuild.MessageRaised -= _buildMessageEventHandler;
                _eventSourceForBuild.ProjectFinished -= _projectFinishedEventHandler;
                _eventSourceForBuild.ProjectStarted -= _projectStartedEventHandler;
                _eventSourceForBuild.StatusEventRaised -= _buildStatusEventHandler;
                _eventSourceForBuild.TargetFinished -= _targetFinishedEventHandler;
                _eventSourceForBuild.TargetStarted -= _targetStartedEventHandler;
                _eventSourceForBuild.TaskFinished -= _taskFinishedEventHandler;
                _eventSourceForBuild.TaskStarted -= _taskStartedEventHandler;
                _eventSourceForBuild.WarningRaised -= _buildWarningEventHandler;

                if (_eventSourceForBuild is IEventSource2 eventSource2)
                {
                    eventSource2.TelemetryLogged -= _telemetryEventHandler;
                }

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
        }
    }
}
