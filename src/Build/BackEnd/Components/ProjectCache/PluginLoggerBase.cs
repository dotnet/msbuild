// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Events logged with this logger will get pushed into MSBuild's logging infrastructure.
    /// </summary>
    public abstract class PluginLoggerBase
    {
        protected PluginLoggerBase(LoggerVerbosity verbosity)
        {
            Verbosity = verbosity;
        }

        /// <summary>
        ///     See <see cref="ILogger.Verbosity" />
        /// </summary>
        private LoggerVerbosity Verbosity { get; }

        public abstract bool HasLoggedErrors { get; protected set; }

        public abstract void LogMessage(string message, MessageImportance? messageImportance = null);

        public abstract void LogWarning(string warning);

        public abstract void LogError(string error);
    }
}
