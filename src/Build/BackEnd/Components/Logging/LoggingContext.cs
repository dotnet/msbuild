using System;
using System.Diagnostics;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// This object encapsulates the logging service plus the current BuildEventContext and
    /// hides the requirement to pass BuildEventContexts to the logging service or query the
    /// host for the logging service all of the time.
    /// </summary>
    internal class LoggingContext
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

        /// <summary>
        /// Constructs the logging context from a logging service and an event context.
        /// </summary>
        /// <param name="loggingService">The logging service to use</param>
        /// <param name="eventContext">The event context</param>
        public LoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
        {
            ErrorUtilities.VerifyThrowArgumentNull(loggingService, "loggingService");
            ErrorUtilities.VerifyThrowArgumentNull(eventContext, "eventContext");

            _loggingService = loggingService;
            _eventContext = eventContext;
            _isValid = false;
        }

        /// <summary>
        /// Constructs a logging context from another logging context.  This is used primarily in
        /// the constructors for other logging contexts to populate the logging service parameter,
        /// while the event context will come from a call into the logging service itself.
        /// </summary>
        /// <param name="baseContext">The context from which this context is being created.</param>
        public LoggingContext(LoggingContext baseContext)
        {
            _loggingService = baseContext._loggingService;
            _eventContext = null;
            _isValid = baseContext._isValid;
        }

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

            protected set
            {
                ErrorUtilities.VerifyThrow(_eventContext == null, "eventContext should be null");
                _eventContext = value;
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

        /// <summary>
        ///  Helper method to create a message build event from a string resource and some parameters
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="messageResourceName">string within the resource which indicates the format string to use</param>
        /// <param name="messageArgs">string resource arguments</param>
        internal void LogComment(MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogComment(_eventContext, importance, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Helper method to create a message build event from a string
        /// </summary>
        /// <param name="importance">Importance level of the message</param>
        /// <param name="message">message to log</param>
        internal void LogCommentFromText(MessageImportance importance, string message)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
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
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogError(_eventContext, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="subcategoryResourceName">The resource name which indicates the subCategory</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="messageResourceName">The resource name for the error</param>
        /// <param name="messageArgs">Parameters for the resource string</param>
        internal void LogErrorWithSubcategory(string subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogError(_eventContext, subcategoryResourceName, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="subcategoryResourceName">The resource name which indicates the subCategory</param>
        /// <param name="errorCode"> Error code</param>
        /// <param name="helpKeyword">Help keyword</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="message">Error message</param>
        internal void LogErrorFromText(string subcategoryResourceName, string errorCode, string helpKeyword, BuildEventFileInfo file, string message)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogErrorFromText(_eventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Log an invalid project file exception
        /// </summary>
        /// <param name="invalidProjectFileException">The invalid Project File Exception which is to be logged</param>
        internal void LogInvalidProjectFileError(InvalidProjectFileException invalidProjectFileException)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogInvalidProjectFileError(_eventContext, invalidProjectFileException);
        }

        /// <summary>
        /// Log an error based on an exception
        /// </summary>
        /// <param name="exception">The exception wich is to be logged</param>
        /// <param name="file">The file in which the error occurred</param>
        /// <param name="messageResourceName">The string resource which has the formatting string for the error</param>
        /// <param name="messageArgs">The arguments for the error message</param>
        internal void LogFatalError(Exception exception, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogFatalError(_eventContext, exception, file, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Log a warning
        /// </summary>
        /// <param name="subcategoryResourceName">The subcategory resource name</param>
        /// <param name="file">The file in which the warning occurred</param>
        /// <param name="messageResourceName">The string resource which contains the formatted warning string</param>
        /// <param name="messageArgs">parameters for the string resource</param>
        internal void LogWarning(string subcategoryResourceName, BuildEventFileInfo file, string messageResourceName, params object[] messageArgs)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
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
        internal void LogWarningFromText(string subcategoryResourceName, string warningCode, string helpKeyword, BuildEventFileInfo file, string message)
        {
            ErrorUtilities.VerifyThrow(_isValid, "must be valid");
            _loggingService.LogWarningFromText(_eventContext, subcategoryResourceName, warningCode, helpKeyword, file, message);
        }

        /// <summary>
        /// Will Log a build Event. Will also take into account OnlyLogCriticalEvents when determining if to drop the event or to log it.
        /// </summary>
        /// <param name="buildEvent">The event to log</param>
        internal void LogBuildEvent(BuildEventArgs buildEvent)
        {
            ErrorUtilities.VerifyThrow(IsValid, "must be valid");
            LoggingService.LogBuildEvent(buildEvent);
        }

        /// <summary>
        /// Log an error based on an exception
        /// </summary>
        /// <param name="exception">The exception wich is to be logged</param>
        /// <param name="file">The file in which the error occurred</param>
        internal void LogFatalBuildError(Exception exception, BuildEventFileInfo file)
        {
            ErrorUtilities.VerifyThrow(IsValid, "must be valid");
            LoggingService.LogFatalBuildError(BuildEventContext, exception, file);
        }
    }
}
