// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

using SdkLoggerBase = Microsoft.Build.Framework.SdkLogger;

#nullable disable

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

        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _loggingContext.LogCommentFromText(messageImportance, message);
        }
    }
}
