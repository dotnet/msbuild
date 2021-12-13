// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
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

        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _loggingContext.LogCommentFromText(messageImportance, message);
        }
    }
}
