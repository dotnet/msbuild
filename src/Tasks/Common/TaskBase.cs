// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public abstract class TaskBase : Task
    {
        private Logger _logger;

        internal TaskBase(Logger logger = null)
        {
            _logger = logger;
        }

        internal new Logger Log
        {
            get
            {
                if (_logger == null)
                {
                    _logger = new LogAdapter(base.Log);
                }

                return _logger;
            }
        }

        public override bool Execute()
        {
            try
            {
                ExecuteCore();
            }
            catch (BuildErrorException e)
            {
                Log.LogError(e.Message);
            }

            return !Log.HasLoggedErrors;
        }

        protected abstract void ExecuteCore();

        private sealed class LogAdapter : Logger
        {
            private TaskLoggingHelper _taskLogger;

            public LogAdapter(TaskLoggingHelper taskLogger)
            {
                _taskLogger = taskLogger;
            }

            protected override void LogCore(in Message message)
            {
                switch (message.Level)
                {
                    case MessageLevel.Error:
                        _taskLogger.LogError(
                            subcategory: default,
                            errorCode: message.Code,
                            helpKeyword: default,
                            file: message.File,
                            lineNumber: default,
                            columnNumber: default,
                            endLineNumber: default,
                            endColumnNumber: default,
                            message: message.Text);
                        break;

                    case MessageLevel.Warning:
                        _taskLogger.LogWarning(
                            subcategory: default,
                            warningCode: message.Code,
                            helpKeyword: default,
                            file: message.File,
                            lineNumber: default,
                            columnNumber: default,
                            endLineNumber: default,
                            endColumnNumber: default,
                            message: message.Text);
                        break;

                    case MessageLevel.HighImportance:
                    case MessageLevel.NormalImportance:
                    case MessageLevel.LowImportance:
                        _taskLogger.LogMessage(
                            subcategory: default,
                            code: message.Code,
                            helpKeyword: default,
                            file: message.File,
                            lineNumber: default,
                            columnNumber: default,
                            endLineNumber: default,
                            endColumnNumber: default,
                            importance: message.Level.ToImportance(),
                            message: message.Text);
                        break;

                    default:
                        throw new ArgumentException(
                            $"Message \"{message.Code}: {message.Text}\" logged with invalid Level=${message.Level}",
                            paramName: nameof(message));
                }
            }
        }
    }
}
