// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    }
}
