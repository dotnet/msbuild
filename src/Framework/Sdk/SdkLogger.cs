// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Eventing;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     An abstract interface class to providing real-time logging and status while resolving
    ///     an SDK.
    /// </summary>
    public abstract class SdkLogger
    {
        /// <summary>
        ///     Log a build message to MSBuild.
        /// </summary>
        /// <param name="message">Message string.</param>
        /// <param name="messageImportance">Optional message importances. Default to low.</param>
        public abstract void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low);

        /// <summary>
        /// Logs that an event.
        /// </summary>
        /// <param name="args">An array of arguments to log with the event.</param>
        public virtual void LogEvent(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEvent(args);
        }

        /// <summary>
        /// Logs that an event when an operation has started.
        /// </summary>
        /// <param name="args">An array of arguments to log with the event.</param>
        public virtual void LogEventStart(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEventStart(args);
        }

        /// <summary>
        /// Logs that an event when an operation has completed.
        /// </summary>
        /// <param name="args">An array of arguments to log with the event.</param>
        public virtual void LogEventStop(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEventStop(args);
        }
    }
}
