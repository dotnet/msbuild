// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
