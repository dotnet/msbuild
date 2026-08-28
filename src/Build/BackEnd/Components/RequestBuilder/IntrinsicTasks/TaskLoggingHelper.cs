// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal class TaskLoggingHelper : ITaskLogger
    {
        private readonly ITask _taskInstance;
        private string _taskNameUpperCase;

        internal TaskLoggingHelper(ITask taskInstance)
        {
            ArgumentNullException.ThrowIfNull(taskInstance);
            _taskInstance = taskInstance;
            TaskName = taskInstance.GetType().Name;
        }

        protected IBuildEngine BuildEngine => _taskInstance.BuildEngine;

        protected string TaskName { get; }

        private string TaskNameUpperCase => _taskNameUpperCase ??= TaskName.ToUpper();

        internal ResourceManager TaskResources { get; set; }

        internal string HelpKeywordPrefix { get; set; }

        public bool IsEnabled => true;

        internal virtual string FormatResourceString(string resourceName, params object[] args)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            ErrorUtilities.VerifyThrowInvalidOperation(TaskResources is not null, "TaskResourcesNotRegistered", TaskName);

            string resourceString = TaskResources.GetString(resourceName, CultureInfo.CurrentUICulture);
            ErrorUtilities.VerifyThrowArgument(resourceString is not null, "TaskResourceNotFound", resourceName, TaskName);

            return MessageFormatter.Format(resourceString, args);
        }

        public void LogErrorWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
            string message = FormatResourceString(messageResourceName, messageArgs);
            if (MessageParser.TryParseAnyCode(message, out string errorCode, out string strippedMessage))
            {
                message = strippedMessage;
            }
            string helpKeyword = HelpKeywordPrefix is null ? null : HelpKeywordPrefix + messageResourceName;

            LogError(null, errorCode, helpKeyword, null, 0, 0, 0, 0, message);
        }

        public void LogWarningWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
            LogWarningWithCodeFromResources(null, null, 0, 0, 0, 0, messageResourceName, messageArgs);
        }

        public void LogWarningWithCodeFromResources(
            string subcategoryResourceName,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string messageResourceName,
            params object[] messageArgs)
        {
            string subcategory = subcategoryResourceName is null ? null : FormatResourceString(subcategoryResourceName);
            string message = FormatResourceString(messageResourceName, messageArgs);

            if (MessageParser.TryParseAnyCode(message, out string warningCode, out string strippedMessage))
            {
                message = strippedMessage;
            }

            string helpKeyword = HelpKeywordPrefix is null ? null : HelpKeywordPrefix + messageResourceName;

            LogWarning(
                subcategory,
                warningCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message);
        }

        private void LogError(
            string subcategory,
            string errorCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message)
        {
            VerifyBuildEngine(message);
            bool fillInLocation = string.IsNullOrEmpty(file) && lineNumber == 0 && columnNumber == 0;

            BuildEngine.LogErrorEvent(
                new BuildErrorEventArgs(
                    subcategory,
                    errorCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName));
        }

        private void LogWarning(
            string subcategory,
            string warningCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message)
        {
            VerifyBuildEngine(message);
            bool fillInLocation = string.IsNullOrEmpty(file) && lineNumber == 0 && columnNumber == 0;

            if (BuildEngine is IBuildEngine8 buildEngine8 && buildEngine8.ShouldTreatWarningAsError(warningCode))
            {
                LogError(
                    subcategory,
                    warningCode,
                    helpKeyword,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message);
                return;
            }

            BuildEngine.LogWarningEvent(
                new BuildWarningEventArgs(
                    subcategory,
                    warningCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName));
        }

        public void LogMessageFromResources(MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
            ArgumentNullException.ThrowIfNull(messageResourceName);

            if (BuildEngine is IBuildEngine10 buildEngine10 && !buildEngine10.EngineServices.LogsMessagesOfImportance(importance))
            {
                return;
            }

            LogMessage(FormatResourceString(messageResourceName), importance, messageArgs);
        }

        public bool LogMessageFromText(string message, MessageImportance importance)
        {
            ArgumentNullException.ThrowIfNull(message);

            CanonicalError.Parts messageParts = CanonicalError.Parse(message);
            if (messageParts is null)
            {
                LogMessage(message, importance, []);
                return false;
            }

            string origin = string.IsNullOrEmpty(messageParts.origin) ? TaskNameUpperCase : messageParts.origin;

            if (messageParts.category == CanonicalError.Parts.Category.Error)
            {
                LogError(
                    messageParts.subcategory,
                    messageParts.code,
                    helpKeyword: null,
                    origin,
                    messageParts.line,
                    messageParts.column,
                    messageParts.endLine,
                    messageParts.endColumn,
                    messageParts.text);
                return true;
            }

            if (messageParts.category != CanonicalError.Parts.Category.Warning)
            {
                InternalError.Throw("Impossible canonical part.");
            }

            LogWarning(
                messageParts.subcategory,
                messageParts.code,
                helpKeyword: null,
                origin,
                messageParts.line,
                messageParts.column,
                messageParts.endLine,
                messageParts.endColumn,
                messageParts.text);
            return false;
        }

        private void LogMessage(string message, MessageImportance importance, params object[] messageArgs)
        {
            if (BuildEngine is IBuildEngine10 buildEngine10 && !buildEngine10.EngineServices.LogsMessagesOfImportance(importance))
            {
                return;
            }

            var eventArgs = new BuildMessageEventArgs(
                message,
                helpKeyword: null,
                senderName: TaskName,
                importance,
                DateTime.UtcNow,
                messageArgs);

            VerifyBuildEngine(eventArgs.Message);
            BuildEngine.LogMessageEvent(eventArgs);
        }

        private void VerifyBuildEngine(string message)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(BuildEngine is not null, "LoggingBeforeTaskInitialization", message);
        }
    }

    internal sealed class TaskLoggingHelperExtension : TaskLoggingHelper
    {
        private ResourceManager _taskSharedResources;

        internal TaskLoggingHelperExtension(
            ITask taskInstance,
            ResourceManager primaryResources,
            ResourceManager sharedResources,
            string helpKeywordPrefix)
            : base(taskInstance)
        {
            TaskResources = primaryResources;
            _taskSharedResources = sharedResources;
            HelpKeywordPrefix = helpKeywordPrefix;
        }

        internal override string FormatResourceString(string resourceName, params object[] args)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            ErrorUtilities.VerifyThrowInvalidOperation(TaskResources is not null, "TaskResourcesNotRegistered", TaskName);
            ErrorUtilities.VerifyThrowInvalidOperation(_taskSharedResources is not null, "TaskResourcesNotRegistered", TaskName);

            string resourceString = TaskResources.GetString(resourceName, CultureInfo.CurrentUICulture)
                ?? _taskSharedResources.GetString(resourceName, CultureInfo.CurrentUICulture);

            ErrorUtilities.VerifyThrowArgument(resourceString is not null, "TaskResourceNotFound", resourceName, TaskName);

            return MessageFormatter.Format(resourceString, args);
        }
    }
}
