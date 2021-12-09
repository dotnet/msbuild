// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;

using SdkLoggerBase = Microsoft.Build.Framework.SdkLogger;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Framework.SdkLogger"/>.
    /// </summary>
    internal class SdkLogger : SdkLoggerBase
    {
        private readonly LoggingContext _loggingContext;

        public SdkLogger(LoggingContext loggingContext)
        {
            _loggingContext = loggingContext;
        }

        public override void LogEvent(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEvent(args);
        }

        public override void LogEventStart(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEventStart(args);
        }

        public override void LogEventStop(params object[] args)
        {
            MSBuildEventSource.Log.SdkResolverEventStop(args);
        }

        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _loggingContext.LogCommentFromText(messageImportance, message);
        }
    }
}
