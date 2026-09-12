// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared
{
    internal readonly struct TaskLoggingHelperAdapter : ITaskLogger
    {
        private readonly TaskLoggingHelper? _log;

        internal TaskLoggingHelperAdapter(TaskLoggingHelper? log)
        {
            _log = log;
        }

        public bool IsEnabled => _log is not null;

        public void LogErrorWithCodeFromResources(string messageResourceName, params object[] messageArgs)
            => _log?.LogErrorWithCodeFromResources(messageResourceName, messageArgs);

        public void LogWarningWithCodeFromResources(string messageResourceName, params object[] messageArgs)
            => _log?.LogWarningWithCodeFromResources(messageResourceName, messageArgs);

        public void LogMessageFromResources(MessageImportance importance, string messageResourceName, params object[] messageArgs)
            => _log?.LogMessageFromResources(importance, messageResourceName, messageArgs);

        public bool LogMessageFromText(string message, MessageImportance importance)
            => _log?.LogMessageFromText(message, importance) ?? false;
    }
}
