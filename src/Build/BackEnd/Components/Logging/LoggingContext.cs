// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// This object encapsulates the logging service plus the current BuildEventContext and
    /// hides the requirement to pass BuildEventContexts to the logging service or query the
    /// host for the logging service all of the time.
    /// </summary>
    internal class LoggingContext : IBuildEngineDataConsumer
    {
        /// <summary>
        /// The logging service to which this context is attached
        /// </summary>
        private readonly ILoggingService _loggingService;

        /// <summary>
        /// The build event context understood by the logging service.
        /// </summary>
        private BuildEventContext _eventContext;

        /// <summary>
        /// True if this context is still valid (i.e. hasn't been "finished")
        /// </summary>
        private bool _isValid;

        protected bool _hasLoggedErrors;
        private bool _hasLoggedWarnings;
        private bool _hasLoggedSdkMessages;
        private List<Action<LoggingContext>>? _deferredEvents;
        private bool _hasDeferredEvaluationDiagnostics;

        /// <summary>
        /// Constructs the logging context from a logging service and an event context.
        /// </summary>
        /// <param name="loggingService">The logging service to use</param>
        /// <param name="eventContext">The event context</param>
        public LoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
        {
            ArgumentNullException.ThrowIfNull(loggingService);
            ArgumentNullException.ThrowIfNull(eventContext);

            _loggingService = loggingService;
            _eventContext = eventContext;
            _isValid = false;
            _hasLoggedErrors = false;
        }

        /// <summary>
        /// Constructs a logging context from another logging context.  This is used primarily in
        /// the constructors for other logging contexts to populate the logging service parameter,
        /// while the event context will come from a call into the logging service itself.
        /// </summary>
        /// <param name="baseContext">The context from which this context is being created.</param>
        /// <param name="newEventContext">The new logging context to be associated here.</param>
        public LoggingContext(LoggingContext baseContext, BuildEventContext newEventContext)
        {
            _loggingService = baseContext._loggingService;
            _eventContext = newEventContext;
            _isValid = baseContext._isValid;
        }

        /// <summary>
        /// Consumer of the execution information from the build engine.
        /// </summary>
        internal IBuildEngineDataConsumer BuildEngineDataConsumer => this;

        /// <summary>
        /// Retrieves the logging service
        /// </summary>
        public ILoggingService LoggingService
        {
            [DebuggerStepThrough]
            get
            { return _loggingService; }
        }

        /// <summary>
        /// Retrieves the build event context
        /// UNDONE: (Refactor) We eventually want to remove this because all logging should go
        /// through a context object.  This exists only so we can make certain
        /// logging calls in code which has not yet been fully refactored.
        /// </summary>
        public BuildEventContext BuildEventContext
        {
            [DebuggerStepThrough]
            get
            {
                return _eventContext;
            }
        }

        /// <summary>
        /// Returns true if the context is still valid, false if the
        /// appropriate 'Finished' call has been invoked.
        /// </summary>
        public bool IsValid
        {
            [DebuggerStepThrough]
            get
            {
                return _isValid;
            }

            [DebuggerStepThrough]
            protected set
            {
                _isValid = value;
            }
        }

        internal bool HasLoggedErrors { get { return _hasLoggedErrors; } set { _hasLoggedErrors = value; } }

        internal bool HasLoggedWarnings => _hasLoggedWarnings;

        internal bool HasLoggedSdkMessages => _hasLoggedSdkMessages;

        internal bool HasDeferredEvaluationDiagnostics => _hasDeferredEvaluationDiagnostics;

        internal static LoggingContext CreateDeferred(
            ILoggingService loggingService,
            BuildEventContext eventContext)
        {
            var context = new LoggingContext(loggingService, eventContext)
            {
                IsValid = true,
                _deferredEvents = [],
            };
            return context;
        }

        internal void ReplayDeferredEvents(LoggingContext target)
        {
            ArgumentNullException.ThrowIfNull(target);
            List<Action<LoggingContext>>? events = _deferredEvents;
            _deferredEvents = null;
            if (events is null)
            {
                return;
            }

            foreach (Action<LoggingContext> deferredEvent in events)
            {
                deferredEvent(target);
            }
        }

        /// <summary>
        ///  Helper method to create a message build event from a string resource and some parameters
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="messageResourceName">string within the resource which indicates the format string to use</param>
        /// <param name="messageArgs">string resource arguments</param>
        internal void LogComment(MessageImportance importance, string messageResourceName, params object?[]? messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogComment(importance, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: false))
            {
                return;
            }

            _loggingService.LogComment(_eventContext, importance, messageResourceName, messageArgs);
        }

        /// <summary>
        ///  Helper method to create a message build event from a string resource and some parameters
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="file">The file in which the event occurred</param>
        /// <param name="messageResourceName">string within the resource which indicates the format string to use</param>
        /// <param name="messageArgs">string resource arguments</param>
        internal void LogComment(MessageImportance importance, BuildEventFileInfo file, string messageResourceName, params object?[]? messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogComment(importance, file, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: false))
            {
                return;
            }

            _loggingService.LogBuildEvent(new BuildMessageEventArgs(
                null,
                null,
                file.File,
                file.Line,
                file.Column,
                file.EndLine,
                file.EndColumn,
                ResourceUtilities.GetResourceString(messageResourceName),
                helpKeyword: null,
                senderName: "MSBuild",
                importance,
                DateTime.UtcNow,
                messageArgs)
            {
                BuildEventContext = _eventContext
            });
        }

        /// <summary>
        /// Helper method to create a message build event from a string
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="message">message to log</param>
        internal void LogCommentFromText(MessageImportance importance, string message)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogCommentFromText(importance, message),
                    isEvaluationDiagnostic: false))
            {
                return;
            }

            _loggingService.LogCommentFromText(_eventContext, importance, message);
        }

        /// <summary>
        /// Helper method to create a message build event from a string
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="message">Message to log</param>
        /// <param name="messageArgs">Format string arguments</param>
        internal void LogCommentFromText(MessageImportance importance, string message, params object[] messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogCommentFromText(importance, message, messageArgs),
                    isEvaluationDiagnostic: false))
            {
                return;
            }

            _loggingService.LogCommentFromText(_eventContext, importance, message, messageArgs);
        }

        internal void LogSdkMessage(MessageImportance importance, string message)
        {
            CheckValidity();
            _hasLoggedSdkMessages = true;
            if (TryDefer(
                    context => context.LogSdkMessage(importance, message),
                    isEvaluationDiagnostic: true))
            {
                return;
            }

            _loggingService.LogCommentFromText(_eventContext, importance, message);
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="messageResourceName">The resource name for the error</param>
        /// <param name="messageArgs">Parameters for the resource string</param>
        internal void LogError(BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogError(file, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            _loggingService.LogError(_eventContext, file, messageResourceName, messageArgs);
            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="subcategoryResourceName">The resource name which indicates the subCategory</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="messageResourceName">The resource name for the error</param>
        /// <param name="messageArgs">Parameters for the resource string</param>
        internal void LogErrorWithSubcategory(string? subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogErrorWithSubcategory(subcategoryResourceName, file, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            _loggingService.LogError(_eventContext, subcategoryResourceName, file, messageResourceName, messageArgs);
            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="subcategoryResourceName">The resource name which indicates the subCategory</param>
        /// <param name="errorCode"> Error code</param>
        /// <param name="helpKeyword">Help keyword</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="message">Error message</param>
        internal void LogErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogErrorFromText(subcategoryResourceName, errorCode, helpKeyword, file, message),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            _loggingService.LogErrorFromText(_eventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);
            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Log an invalid project file exception
        /// </summary>
        /// <param name="invalidProjectFileException">The invalid Project File Exception which is to be logged</param>
        internal void LogInvalidProjectFileError(InvalidProjectFileException invalidProjectFileException)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogInvalidProjectFileError(invalidProjectFileException),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            _loggingService.LogInvalidProjectFileError(_eventContext, invalidProjectFileException);
            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Log an error based on an exception
        /// </summary>
        /// <param name="exception">The exception wich is to be logged</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="messageResourceName">The string resource which has the formatting string for the error</param>
        /// <param name="messageArgs">The arguments for the error message</param>
        internal void LogFatalError(Exception exception, BuildEventFileInfo file, string messageResourceName, params object?[]? messageArgs)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogFatalError(exception, file, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            _loggingService.LogFatalError(_eventContext, exception, file, messageResourceName, messageArgs);
            _hasLoggedErrors = true;
        }

        internal void LogWarning(string messageResourceName, params object[] messageArgs)
        {
            CheckValidity();
            _hasLoggedWarnings = true;
            if (TryDefer(
                    context => context.LogWarning(messageResourceName, messageArgs),
                    isEvaluationDiagnostic: true))
            {
                return;
            }

            _loggingService.LogWarning(_eventContext, null, BuildEventFileInfo.Empty, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Log a warning
        /// </summary>
        /// <param name="subcategoryResourceName">The subcategory resource name</param>
        /// <param name="file">The file in which the warning occurred</param>
        /// <param name="messageResourceName">The string resource which contains the formatted warning string</param>
        /// <param name="messageArgs">parameters for the string resource</param>
        internal void LogWarning(string? subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object?[]? messageArgs)
        {
            CheckValidity();
            _hasLoggedWarnings = true;
            if (TryDefer(
                    context => context.LogWarning(subcategoryResourceName, file, messageResourceName, messageArgs),
                    isEvaluationDiagnostic: true))
            {
                return;
            }

            _loggingService.LogWarning(_eventContext, subcategoryResourceName, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Log a warning based on a text message
        /// </summary>
        /// <param name="subcategoryResourceName">The subcategory resource name</param>
        /// <param name="warningCode"> Warning code</param>
        /// <param name="helpKeyword"> Help keyword</param>
        /// <param name="file">The file in which the warning occurred</param>
        /// <param name="message">The message to be logged as a warning</param>
        internal void LogWarningFromText(string? subcategoryResourceName, string warningCode, string helpKeyword, BuildEventFileInfo file, string message)
        {
            CheckValidity();
            _hasLoggedWarnings = true;
            if (TryDefer(
                    context => context.LogWarningFromText(subcategoryResourceName, warningCode, helpKeyword, file, message),
                    isEvaluationDiagnostic: true))
            {
                return;
            }

            _loggingService.LogWarningFromText(_eventContext, subcategoryResourceName, warningCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Will Log a build Event. Will also take into account OnlyLogCriticalEvents when determining if to drop the event or to log it.
        /// </summary>
        /// <param name="buildEvent">The event to log</param>
        internal void LogBuildEvent(BuildEventArgs buildEvent)
        {
            CheckValidity();
            _hasLoggedWarnings |= buildEvent is BuildWarningEventArgs;
            _hasLoggedErrors |= buildEvent is BuildErrorEventArgs;
            if (TryDefer(
                    context =>
                    {
                        buildEvent.BuildEventContext = context.BuildEventContext;
                        context.LogBuildEvent(buildEvent);
                    },
                    buildEvent is BuildWarningEventArgs or BuildErrorEventArgs))
            {
                return;
            }

            LoggingService.LogBuildEvent(buildEvent);
        }

        /// <summary>
        /// Log an error based on an exception
        /// </summary>
        /// <param name="exception">The exception to be logged</param>
        /// <param name="file">The file in which the error occurred</param>
        internal void LogFatalBuildError(Exception exception, BuildEventFileInfo file)
        {
            CheckValidity();
            if (TryDefer(
                    context => context.LogFatalBuildError(exception, file),
                    isEvaluationDiagnostic: true))
            {
                _hasLoggedErrors = true;
                return;
            }

            LoggingService.LogFatalBuildError(BuildEventContext, exception, file);
            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Logs a file to be included in the binary logger
        /// </summary>
        /// <param name="filePath">Path to response file</param>
        internal void LogIncludeFile(string filePath)
        {
            CheckValidity();
            _loggingService.LogIncludeFile(BuildEventContext, filePath);
        }

        public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo)
            => _loggingService.BuildEngineDataRouter.ProcessPropertyRead(
                    propertyReadInfo,
                    new CheckLoggingContext(_loggingService, BuildEventContext));

        public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo)
            => _loggingService.BuildEngineDataRouter.ProcessPropertyWrite(
                    propertyWriteInfo,
                    new CheckLoggingContext(_loggingService, BuildEventContext));

        private protected void CheckValidity()
            => Assumed.True(_isValid, $"LoggingContext (type: {GetType()}) was not valid during logging attempt.");

        private bool TryDefer(
            Action<LoggingContext> deferredEvent,
            bool isEvaluationDiagnostic)
        {
            if (_deferredEvents is null)
            {
                return false;
            }

            _deferredEvents.Add(deferredEvent);
            _hasDeferredEvaluationDiagnostics |= isEvaluationDiagnostic;
            return true;
        }
    }
}
