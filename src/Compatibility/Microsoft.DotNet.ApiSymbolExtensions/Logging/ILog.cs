// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Interface to define common logging abstraction between Console and MSBuild tasks across the APICompat and GenAPI codebases.
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// True if errors are logged.
        /// </summary>
        bool HasLoggedErrors { get; }

        /// <summary>
        /// Log an error.
        /// <param name="message">The message</param>
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Log an error with an error code.
        /// <param name="code">The error code</param>
        /// <param name="message">The message</param>
        /// </summary>
        void LogError(string code, string message);

        /// <summary>
        /// Log a warning.
        /// <param name="message">The message</param>
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Log a warning with a warning code.
        /// <param name="code">The warning code</param>
        /// <param name="message">The message</param>
        /// </summary>
        void LogWarning(string code, string message);

        /// <summary>
        /// Log a message with normal importance.
        /// <param name="message">The message</param>
        /// </summary>
        void LogMessage(string message);

        /// <summary>
        /// Log a message with a custom importance.
        /// <param name="importance">The message importance</param>
        /// <param name="message">The message</param>
        /// </summary>
        void LogMessage(MessageImportance importance, string message);
    }
}
