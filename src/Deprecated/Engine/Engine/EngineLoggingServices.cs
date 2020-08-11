// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This abstract class defines a logging service, provided by the engine, for internal logging 
    /// purposes. This class is very different from the IBuildEngine interface which provides logging
    /// services for tasks. This class allows for better encapsulation by making it clear when only
    /// logging services are needed and not the rest of the engine data and methods. This class allows
    /// us to provide different implementations of logging services for engine components that are
    /// either in-proc (engine process) or out-of-proc (node process).
    /// </summary>
    /// <remarks>
    /// We have made this an abstract class and not an interface to avoid forcing the "public" access
    /// modifier on the implementation of the internal logging services.
    /// </remarks>
    internal abstract class EngineLoggingServices
    {
        #region Initialization/Shutdown methods

        /// <summary>
        /// Initializes the base class data. Sub-classes must call this method in their constructor.
        /// </summary>
        protected void Initialize(ManualResetEvent flushRequestEventIn)
        {
            this.loggingQueueOfBuildEvents = new DualQueue<BuildEventArgs>();
            this.loggingQueueOfNodeEvents  = new DualQueue<NodeLoggingEvent>();
            this.lastFlushTime = DateTime.Now.Ticks;
            this.flushRequestEvent = flushRequestEventIn;
            this.requestedQueueFlush = false;
        }

        /// <summary>
        /// Causes all events to be discarded until EndEatingEvents() is called.
        /// </summary>
        internal void BeginEatingEvents()
        {
            paused = true;
        }

        /// <summary>
        /// Ensures events are no longer discarded if BeginEatingEvents() had been called.
        /// </summary>
        internal void EndEatingEvents()
        {
            paused = false;
        }

        /// <summary>
        /// Shutdown the logging service as appropriate
        /// </summary>
        virtual internal void Shutdown()
        {
            ErrorUtilities.VerifyThrow(false, "This method should be defined in a subclass");
        }

        #endregion

        #region Queue handling methods

        /// <summary>
        /// Called to add a logging event to the posting queue.
        /// </summary>
        /// <param name="e"></param>
        internal void PostLoggingEvent(BuildEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));

            if (paused)
            {
                // Throw out the event
                return;
            }

            if (flushBuildEventsImmediatly)
            {
                ProcessBuildEvent(e);
            }
            else
            {
                // queue the event
                loggingQueueOfBuildEvents.Enqueue(e);

                if (!requestedQueueFlush && loggingQueueOfBuildEvents.WritingQueueCount > flushQueueSize)
                {
                    requestedQueueFlush = true;
                    flushRequestEvent.Set();
                }
            }
        }

        /// <summary>
        /// Called to add logging events to the posting queue.
        /// </summary>
        /// <param name="eventArray"></param>
        internal void PostLoggingEvents(BuildEventArgs[] eventArray)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventArray, nameof(eventArray));

            if (paused)
            {
                // Throw out the event
                return;
            }

            if (flushBuildEventsImmediatly)
            {
                for (int i = 0; i < eventArray.Length; i++)
                {
                    ProcessBuildEvent(eventArray[i]);
                }
            }
            else
            {
                loggingQueueOfBuildEvents.EnqueueArray(eventArray);

                if (!requestedQueueFlush && loggingQueueOfBuildEvents.WritingQueueCount > flushQueueSize)
                {
                    requestedQueueFlush = true;
                    flushRequestEvent.Set();
                }
            }
        }

        /// <summary>
        /// Called to add a logging event to the posting queue.
        /// </summary>
        /// <param name="e"></param>
        internal void PostLoggingEvent(NodeLoggingEvent e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));

            if (paused)
            {
                // Throw out the event
                return;
            }

            // queue the event
            loggingQueueOfNodeEvents.Enqueue(e);

            if (!requestedQueueFlush && loggingQueueOfNodeEvents.WritingQueueCount > flushQueueSize)
            {
                requestedQueueFlush = true;
                flushRequestEvent.Set();
            }
        }

        /// <summary>
        /// Called to add logging events to the posting queue.
        /// </summary>
        /// <param name="eventArray"></param>
        internal void PostLoggingEvents(NodeLoggingEvent[] eventArray)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventArray, nameof(eventArray));

            if (paused)
            {
                // Throw out the event
                return;
            }

            loggingQueueOfNodeEvents.EnqueueArray(eventArray);

            if (!requestedQueueFlush && loggingQueueOfNodeEvents.WritingQueueCount > flushQueueSize)
            {
                requestedQueueFlush = true;
                flushRequestEvent.Set();
            }
        }

        /// <summary>
        /// Abstract method that must be implemented for either in-proc or out-of-proc logging.
        /// This method is called to process the events batched up in the reading queue. In the
        /// in-proc implementation, this will actually raise the events. In the out-of-proc
        /// implementation, this will send the events from the node process to the engine.
        /// </summary>
        abstract internal bool ProcessPostedLoggingEvents();
        
        /// <summary>
        /// This method is to process a single build event, by default if this method is used
        /// the event should be posted and processed
        /// </summary>
        /// <param name="buildEventArgs"></param>
        virtual internal void ProcessBuildEvent(BuildEventArgs buildEventArgs)
        {
            PostLoggingEvent(buildEventArgs);
            ProcessPostedLoggingEvents();
        }
        
        /// <summary>
        /// Return true if the queue needs to be flushed
        /// </summary>
        virtual internal bool NeedsFlush(long currentTickCount)
        {
            if (this.flushBuildEventsImmediatly)
            {
                return false;
            }

            // Force a flush if there are many events accumulated
            if (loggingQueueOfBuildEvents.Count > flushQueueSize || loggingQueueOfNodeEvents.Count > flushQueueSize)
            {
                return true;
            }
            // Only obtain current time if the timestamp is not passed in to avoid extra native calls
            if (currentTickCount == 0)
            {
                currentTickCount = DateTime.Now.Ticks;
            }

            return (currentTickCount - lastFlushTime) > flushTimeoutInTicks;
        }

        #endregion

        #region Event based logging methods
        /// <summary>
        /// Raises ErrorEvent and AnyEvent at all registered loggers.
        /// </summary>
        /// <owner>t-jeffv, SumedhK</owner>
        /// <param name="e"></param> 
        internal void LogErrorEvent(BuildErrorEventArgs e)
        {
            // We are intentionally passing in null for the "sender" object, even when
            // the event is coming from task.  This is because we don't want to allow
            // tight coupling between loggers and tasks in this way.  It's not good
            // for a logger to be able to call back into a task.  It could be a security
            // issue, and it apparently will also cause us problems if we adopt the
            // new Longhorn Add-In Programming Model.  

            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            PostLoggingEvent(e);
        }

        /// <summary>
        /// Raises MessageEvent and AnyEvent at all registered loggers.
        /// </summary>
        /// <owner>t-jeffv, SumedhK</owner>
        /// <param name="e"></param> 
        internal void LogMessageEvent(BuildMessageEventArgs e)
        {
            if (!OnlyLogCriticalEvents)
            {
                // We are intentionally passing in null for the "sender" object, even when
                // the event is coming from task.  This is because we don't want to allow
                // tight coupling between loggers and tasks in this way.  It's not good
                // for a logger to be able to call back into a task.  It could be a security
                // issue, and it apparently will also cause us problems if we adopt the
                // new Longhorn Add-In Programming Model.

                ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Raises WarningEvent and AnyEvent at all registered loggers.
        /// </summary>
        /// <owner>t-jeffv, SumedhK</owner>
        /// <param name="e"></param> 
        internal void LogWarningEvent(BuildWarningEventArgs e)
        {
            // We are intentionally passing in null for the "sender" object, even when
            // the event is coming from task.  This is because we don't want to allow
            // tight coupling between loggers and tasks in this way.  It's not good
            // for a logger to be able to call back into a task.  It could be a security
            // issue, and it apparently will also cause us problems if we adopt the
            // new Longhorn Add-In Programming Model.

            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            PostLoggingEvent(e);
        }

        /// <summary>
        /// Raises CustomEvent and AnyEvent at all registered loggers.
        /// </summary>
        /// <owner>t-jeffv, SumedhK</owner>
        /// <param name="e"></param> 
        internal void LogCustomEvent(CustomBuildEventArgs e)
        {
            // We are intentionally passing in null for the "sender" object, even when
            // the event is coming from task.  This is because we don't want to allow
            // tight coupling between loggers and tasks in this way.  It's not good
            // for a logger to be able to call back into a task.  It could be a security
            // issue, and it apparently will also cause us problems if we adopt the
            // new Longhorn Add-In Programming Model.

            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            PostLoggingEvent(e);
        }
        #endregion

        #region Log comments
        /**************************************************************************************************************************
         * WARNING: Do not add overloads that allow raising events without specifying a file. In general ALL events should have a
         * file associated with them. We've received a LOT of feedback from dogfooders about the lack of information in our
         * events. If an event TRULY does not have an associated file, then String.Empty can be passed in for the file. However,
         * that burden should lie on the caller -- these wrapper methods should NOT make it easy to skip the filename.
         *************************************************************************************************************************/
        /// <summary>
        /// Logs a low-priority comment with all registered loggers using the specified resource string.
        /// </summary>
        virtual internal void LogComment(BuildEventContext buildEventContext, string messageResourceName, params object[] messageArgs)
        {
            if (!OnlyLogCriticalEvents)
            {
                LogComment(buildEventContext, MessageImportance.Low, messageResourceName, messageArgs);
            }
        }

        /// <summary>
        /// Logs a custom-priority comment with all registered loggers using the specified resource string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="importance"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        virtual internal void LogComment(BuildEventContext buildEventContext, MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
            if (!OnlyLogCriticalEvents)
            {
                ErrorUtilities.VerifyThrow(messageResourceName != null, "Need resource string for comment message.");

                LogCommentFromText(buildEventContext, importance, ResourceUtilities.FormatResourceString(messageResourceName, messageArgs));
            }
        }

        /// <summary>
        /// Logs a custom-priority comment with all registered loggers using the given text.
        /// </summary>
        virtual internal void LogCommentFromText(BuildEventContext buildEventContext,MessageImportance importance, string message)
        {
            if (!OnlyLogCriticalEvents)
            {
                ErrorUtilities.VerifyThrow(message != null,
                                        "Need comment message.");

                BuildMessageEventArgs e = new BuildMessageEventArgs
                    (
                        message,
                        null,
                        "MSBuild",
                        importance
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        #endregion

        #region Log errors
        /**************************************************************************************************************************
         * WARNING: Do not add overloads that allow raising events without specifying a file. In general ALL events should have a
         * file associated with them. We've received a LOT of feedback from dogfooders about the lack of information in our
         * events. If an event TRULY does not have an associated file, then String.Empty can be passed in for the file. However,
         * that burden should lie on the caller -- these wrapper methods should NOT make it easy to skip the filename.
         *************************************************************************************************************************/

        /// <summary>
        /// Logs an error with all registered loggers using the specified resource string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="file"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        virtual internal void LogError(BuildEventContext location, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            LogError(location, null, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs an error with all registered loggers using the specified resource string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="subcategoryResourceName">Can be null.</param>
        /// <param name="file"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        virtual internal void LogError(BuildEventContext buildEventContext, string subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need resource string for error message.");

            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, messageResourceName, messageArgs);

            LogErrorFromText(buildEventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Logs an error with all registered loggers using the given text.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="subcategoryResourceName">Can be null.</param>
        /// <param name="errorCode">Can be null.</param>
        /// <param name="helpKeyword">Can be null.</param>
        /// <param name="file"></param>
        /// <param name="message"></param>
        virtual internal void LogErrorFromText(BuildEventContext buildEventContext, string subcategoryResourceName, string errorCode, string helpKeyword, BuildEventFileInfo file, string message)
        {
            ErrorUtilities.VerifyThrow(file != null, "Must specify the associated file.");
            ErrorUtilities.VerifyThrow(message != null, "Need error message.");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = AssemblyResources.GetString(subcategoryResourceName);
            }

            BuildErrorEventArgs e =
                new BuildErrorEventArgs
                (
                    subcategory,
                    errorCode,
                    file.File,
                    file.Line,
                    file.Column,
                    file.EndLine,
                    file.EndColumn,
                    message,
                    helpKeyword,
                    "MSBuild"
                );
            e.BuildEventContext = buildEventContext;
            PostLoggingEvent(e);
        }

        /// <summary>
        /// Logs an error regarding an invalid project file with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="invalidProjectFileException"></param>
        virtual internal void LogInvalidProjectFileError(BuildEventContext buildEventContext, InvalidProjectFileException invalidProjectFileException)
        {
            ErrorUtilities.VerifyThrow(invalidProjectFileException != null, "Need exception context.");

            // Don't log the exception more than once.
            if (!invalidProjectFileException.HasBeenLogged)
            {
                BuildErrorEventArgs e =
                    new BuildErrorEventArgs
                    (
                        invalidProjectFileException.ErrorSubcategory,
                        invalidProjectFileException.ErrorCode,
                        invalidProjectFileException.ProjectFile,
                        invalidProjectFileException.LineNumber,
                        invalidProjectFileException.ColumnNumber,
                        invalidProjectFileException.EndLineNumber,
                        invalidProjectFileException.EndColumnNumber,
                        invalidProjectFileException.BaseMessage,
                        invalidProjectFileException.HelpKeyword,
                        "MSBuild"
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);

                invalidProjectFileException.HasBeenLogged = true;
            }
        }

        /// <summary>
        /// Logs an error regarding an unexpected build failure with all registered loggers.
        /// This will include a stack dump.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="exception"></param>
        /// <param name="file"></param>
        virtual internal void LogFatalBuildError(BuildEventContext buildEventContext, Exception exception, BuildEventFileInfo file)
        {
            LogFatalError(buildEventContext, exception, file, "FatalBuildError");
        }

        /// <summary>
        /// Logs an error regarding an unexpected task failure with all registered loggers.
        /// This will include a stack dump.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="exception"></param>
        /// <param name="file"></param>
        /// <param name="taskName"></param>
        virtual internal void LogFatalTaskError(BuildEventContext buildEventContext, Exception exception, BuildEventFileInfo file, string taskName)
        {
            ErrorUtilities.VerifyThrow(taskName != null, "Must specify the name of the task that failed.");

            LogFatalError(buildEventContext, exception, file, "FatalTaskError", taskName);
        }

        /// <summary>
        /// Logs an error regarding an unexpected failure with all registered loggers using the specified resource string.
        /// This will include a stack dump.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="exception"></param>
        /// <param name="file"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        virtual internal void LogFatalError(BuildEventContext buildEventContext, Exception exception, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need resource string for error message.");

            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, messageResourceName, messageArgs);
            #if DEBUG
            message += Environment.NewLine + "This is an unhandled exception -- PLEASE OPEN A BUG.";
            #endif
            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            LogErrorFromText(buildEventContext, null, errorCode, helpKeyword, file, message);
        }

        #endregion

        #region Log warnings
        /**************************************************************************************************************************
         * WARNING: Do not add overloads that allow raising events without specifying a file. In general ALL events should have a
         * file associated with them. We've received a LOT of feedback from dogfooders about the lack of information in our
         * events. If an event TRULY does not have an associated file, then String.Empty can be passed in for the file. However,
         * that burden should lie on the caller -- these wrapper methods should NOT make it easy to skip the filename.
         *************************************************************************************************************************/

        /// <summary>
        /// Logs an warning regarding an unexpected task failure with all registered loggers.
        /// This will include a stack dump.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="exception"></param>
        /// <param name="file"></param>
        /// <param name="taskName"></param>
        virtual internal void LogTaskWarningFromException(BuildEventContext buildEventContext, Exception exception, BuildEventFileInfo file, string taskName)
        {
            ErrorUtilities.VerifyThrow(taskName != null, "Must specify the name of the task that failed.");
            ErrorUtilities.VerifyThrow(file != null, "Must specify the associated file.");

            string warningCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out warningCode, out helpKeyword, "FatalTaskError", taskName);
            #if DEBUG
            message += Environment.NewLine + "This is an unhandled exception -- PLEASE OPEN A BUG.";
            #endif

            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            LogWarningFromText(buildEventContext, null, warningCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Logs a warning with all registered loggers using the specified resource string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="file"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
         virtual internal void LogWarning(BuildEventContext buildEventContext, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            LogWarning(buildEventContext,null, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs a warning with all registered loggers using the specified resource string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="subcategoryResourceName">Can be null.</param>
        /// <param name="file"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="messageArgs"></param>
        virtual internal void LogWarning(BuildEventContext buildEventContext, string subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need resource string for warning message.");

            string warningCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out warningCode, out helpKeyword, messageResourceName, messageArgs);

            LogWarningFromText(buildEventContext, subcategoryResourceName, warningCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Logs a warning with all registered loggers using the given text.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="subcategoryResourceName">Can be null.</param>
        /// <param name="warningCode">Can be null.</param>
        /// <param name="helpKeyword">Can be null.</param>
        /// <param name="file"></param>
        /// <param name="message"></param>
        virtual internal void LogWarningFromText(BuildEventContext buildEventContext, string subcategoryResourceName, string warningCode, string helpKeyword, BuildEventFileInfo file, string message)
        {
            ErrorUtilities.VerifyThrow(file != null, "Must specify the associated file.");
            ErrorUtilities.VerifyThrow(message != null, "Need warning message.");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = AssemblyResources.GetString(subcategoryResourceName);
            }

            BuildWarningEventArgs e = new BuildWarningEventArgs
                (
                    subcategory,
                    warningCode,
                    file.File,
                    file.Line,
                    file.Column,
                    file.EndLine,
                    file.EndColumn,
                    message,
                    helpKeyword,
                    "MSBuild"
                );
            e.BuildEventContext = buildEventContext;
            PostLoggingEvent(e);
        }

        #endregion

        #region Log status
        /**************************************************************************************************************************
         * WARNING: Do not add overloads that allow raising events without specifying a file. In general ALL events should have a
         * file associated with them. We've received a LOT of feedback from dogfooders about the lack of information in our
         * events. If an event TRULY does not have an associated file, then String.Empty can be passed in for the file. However,
         * that burden should lie on the caller -- these wrapper methods should NOT make it easy to skip the filename.
         *************************************************************************************************************************/

        /// <summary>
        /// Logs that the build has started with all loggers (only called on the main node)
        /// </summary>
        virtual internal void LogBuildStarted()
        {
            // If we're only logging critical events, don't risk causing all the resources to load by formatting
            // a string that won't get emitted anyway.
            string message = String.Empty;
            if (!OnlyLogCriticalEvents)
            {
                message = ResourceUtilities.FormatResourceString("BuildStarted");
            }

            BuildStartedEventArgs e = new BuildStartedEventArgs(message, null /* no help keyword */);
            PostLoggingEvent(e);

            // Wrap the event to be sent to central loggers
            NodeLoggingEventWithLoggerId nodeEventToCentralLoggers =
                new NodeLoggingEventWithLoggerId(e, EngineLoggingServicesInProc.ALL_PRIVATE_EVENTSOURCES);
            PostLoggingEvent(nodeEventToCentralLoggers);
        }

        /// <summary>
        /// Logs that the build has finished with all loggers, except for forwarding loggers.
        /// </summary>
        virtual internal void LogBuildStarted(int loggerId)
        {
            // If we're only logging critical events, don't risk causing all the resources to load by formatting
            // a string that won't get emitted anyway.
            string message = String.Empty;
            if (!OnlyLogCriticalEvents)
            {
                message = ResourceUtilities.FormatResourceString("BuildStarted");
            }

            BuildStartedEventArgs e = new BuildStartedEventArgs(message, null /* no help keyword */);

            // Wrap the BuildStarted event so it is only sent to the loggers to
            // the specified logger id
            NodeLoggingEventWithLoggerId nodeEvent =
                new NodeLoggingEventWithLoggerId(e, loggerId);
            PostLoggingEvent(nodeEvent);
        }

        /// <summary>
        /// Logs that the build has finished with all registered loggers.
        /// </summary>
        /// <param name="success"></param>
        virtual internal void LogBuildFinished(bool success)
        {
            // If we're only logging critical events, don't risk causing all the resources to load by formatting
            // a string that won't get emitted anyway.
            string message = String.Empty;
            if (!OnlyLogCriticalEvents)
            {
                message = ResourceUtilities.FormatResourceString(success ? "BuildFinishedSuccess" : "BuildFinishedFailure");
            }

            BuildFinishedEventArgs e = new BuildFinishedEventArgs(message, null /* no help keyword */, success);

            PostLoggingEvent(e);
        }

        /// <summary>
        /// Logs that the build has finished to a particular logger Id
        /// </summary>
        virtual internal void LogBuildFinished(bool success, int loggerId)
        {
            // If we're only logging critical events, don't risk causing all the resources to load by formatting
            // a string that won't get emitted anyway.
            string message = String.Empty;
            if (!OnlyLogCriticalEvents)
            {
                message = ResourceUtilities.FormatResourceString(success ? "BuildFinishedSuccess" : "BuildFinishedFailure");
            }

            BuildFinishedEventArgs e = new BuildFinishedEventArgs(message, null /* no help keyword */, success);

            // Wrap the BuildFinished event so it is only sent to the loggers to
            // the specified logger id
            NodeLoggingEventWithLoggerId nodeEvent =
                new NodeLoggingEventWithLoggerId(e, loggerId);
            PostLoggingEvent(nodeEvent);
        }

        /// <summary>
        /// Logs that a project build has started with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="projectFile">project file</param>
        /// <param name="targetNames">target names</param>
        /// <param name="properties">properties list</param>
        /// <param name="items">items list</param>
        virtual internal void LogProjectStarted(int projectId, BuildEventContext parentBuildEventContext, BuildEventContext projectBuildEventContext, string projectFile, string targetNames, IEnumerable properties, IEnumerable items)
        {
            if (!OnlyLogCriticalEvents)
            {
                ProjectStartedEventArgs e;
              
                if (!string.IsNullOrEmpty(targetNames))
                {
                    e = new ProjectStartedEventArgs
                        (
                            projectId,
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithTargetNames", Path.GetFileName(projectFile), targetNames),
                            null,       // no help keyword
                            projectFile,
                            targetNames,
                            properties,
                            items,
                            parentBuildEventContext
                        );
                }
                else
                {
                    e = new ProjectStartedEventArgs
                        (
                            projectId,
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", Path.GetFileName(projectFile)),
                            null,       // no help keyword
                            projectFile,
                            targetNames,
                            properties,
                            items,
                            parentBuildEventContext
                        );
                }
 
                e.BuildEventContext = projectBuildEventContext;

                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Logs that a project build has finished with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="projectFile"></param>
        /// <param name="success"></param>
        virtual internal void LogProjectFinished(BuildEventContext buildEventContext, string projectFile, bool success)
        {
            if (!OnlyLogCriticalEvents)
            {
                string message = ResourceUtilities.FormatResourceString(success ? "ProjectFinishedSuccess" : "ProjectFinishedFailure", Path.GetFileName(projectFile));

                ProjectFinishedEventArgs e = new ProjectFinishedEventArgs
                    (
                        message,
                        null,       // no help keyword
                        projectFile,
                        success
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Logs that a target build has started with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="targetName">Name of target</param>
        /// <param name="projectFile">Main project file</param>
        /// <param name="projectFileOfTargetElement">Project file actually containing the target</param>
        virtual internal void LogTargetStarted(BuildEventContext buildEventContext, string targetName, string projectFile, string projectFileOfTargetElement)
        {
            if (!OnlyLogCriticalEvents)
            {
                TargetStartedEventArgs e = new TargetStartedEventArgs
                    (
                        ResourceUtilities.FormatResourceString("TargetStarted", targetName, Path.GetFileName(projectFile)),
                        null,             // no help keyword
                        targetName,
                        projectFile,
                        projectFileOfTargetElement
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Logs that a target build has finished with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="targetName"></param>
        /// <param name="projectFile">main project file</param>
        /// <param name="success"></param>
        /// <param name="projectFileOfTargetElement">project file actually containing the target</param>
        virtual internal void LogTargetFinished(BuildEventContext buildEventContext, string targetName, string projectFile, string projectFileOfTargetElement, bool success)
        {
            if (!OnlyLogCriticalEvents)
            {
                string message = ResourceUtilities.FormatResourceString(success ? "TargetFinishedSuccess" : "TargetFinishedFailure", targetName, Path.GetFileName(projectFile));

                TargetFinishedEventArgs e = new TargetFinishedEventArgs
                    (
                        message,
                        null,             // no help keyword
                        targetName,
                        projectFile,
                        projectFileOfTargetElement,
                        success
                    );

                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Logs that task execution has started with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="taskName"></param>
        /// <param name="projectFile"></param>
        /// <param name="projectFileOfTaskNode">project file actually containing the task</param>
        virtual internal void LogTaskStarted(BuildEventContext buildEventContext, string taskName, string projectFile, string projectFileOfTaskNode)
        {
            if (!OnlyLogCriticalEvents)
            {
                TaskStartedEventArgs e = new TaskStartedEventArgs
                    (
                        ResourceUtilities.FormatResourceString("TaskStarted", taskName),
                        null,             // no help keyword
                        projectFile,
                        projectFileOfTaskNode,
                        taskName
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        /// <summary>
        /// Logs that a task has finished executing with all registered loggers.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="taskName"></param>
        /// <param name="projectFile"></param>
        /// <param name="success"></param>
        /// <param name="projectFileOfTaskNode">project file actually containing the task</param>
        virtual internal void LogTaskFinished(BuildEventContext buildEventContext, string taskName, string projectFile, string projectFileOfTaskNode, bool success)
        {
            if (!OnlyLogCriticalEvents)
            {
                string message = ResourceUtilities.FormatResourceString(success ? "TaskFinishedSuccess" : "TaskFinishedFailure", taskName);

                TaskFinishedEventArgs e = new TaskFinishedEventArgs
                    (
                        message,
                        null,             // no help keyword
                        projectFile,
                        projectFileOfTaskNode,
                        taskName,
                        success
                    );
                e.BuildEventContext = buildEventContext;
                PostLoggingEvent(e);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// When true, only log critical events such as warnings and errors.
        /// </summary>
        internal bool OnlyLogCriticalEvents
        {
            get
            {
                return this.onlyLogCriticalEvents;
            }
            set
            {
                this.onlyLogCriticalEvents = value;
            }
        }

        /// <summary>
        /// Chains another logging service to this service. All incoming local events will be forwarded to the
        /// chained logging service.
        /// </summary>
        internal EngineLoggingServices ForwardingService
        {
            get
            {
                return this.forwardingService;
            }
            set
            {
                this.forwardingService = value;
            }
        }

        /// <summary>
        /// When true, only log critical events such as warnings and errors.
        /// </summary>
        internal bool FlushBuildEventsImmediatly
        {
            get
            {
                return this.flushBuildEventsImmediatly;
            }
            set
            {
                this.flushBuildEventsImmediatly = value;
            }
        }

        #endregion

        #region Data

        /// <summary>
        /// Use to optimize away status messages. When this is set to true, only "critical"
        /// events like errors are logged.
        /// </summary>
        protected bool onlyLogCriticalEvents;

        /// <summary>
        /// Whether the loggers are paused. If they are paused, we do not pass on any events.
        /// </summary>
        private bool paused;

        // In single proc we would like to flush the logging events right away rather than queueing them up
        // we only want to flush build events because node events can only come in a multi proc build
        protected bool flushBuildEventsImmediatly;

        /// <summary>
        /// A dual queue which allows for one reader multiple writer access (used to receive events from 
        /// the engine and the TEM)
        /// </summary>
        protected DualQueue<BuildEventArgs> loggingQueueOfBuildEvents;

        /// <summary>
        /// A dual queue which allows for one reader multiple writer access (used to receive events from
        /// other nodes)
        /// </summary>
        protected DualQueue<NodeLoggingEvent> loggingQueueOfNodeEvents;

        /// <summary>
        /// If there are multiple logging services hooked up to an engine, the events
        /// are forwarding from the top service down.
        /// </summary>
        protected EngineLoggingServices forwardingService;

        /// <summary>
        /// Last timestamp when the queue was flushed
        /// </summary>
        protected long lastFlushTime;

        /// <summary>
        /// If the number of items in the queue goes up in a spike the logging service can request
        /// a flush of the queue. To prevent multiple requests this flag is used to indicate that
        /// flush request has already been posted
        /// </summary>
        protected bool requestedQueueFlush;

        /// <summary>
        /// An event used to request a flush of the logging service. Typically triggered due to 
        /// a spike in logging activity.
        /// </summary>
        protected ManualResetEvent flushRequestEvent;

        internal const int flushTimeoutInMS = 500;          // flush the queue at least every 1/2 second
        internal const int flushTimeoutInTicks = 500*10000; // flush the queue at least every 1/2 second
        internal const int flushQueueSize = 1000;            // flush the queue every time 1000 events accumulate

        #endregion
    }
}
