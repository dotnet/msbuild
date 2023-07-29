// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Serves as the base class for classes implementing loggers for installers.
    /// </summary>
    internal abstract class SetupLoggerBase
    {
        /// <summary>
        /// Writes the message to the log, prefixing it with the process ID.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void LogMessage(string message)
        {
            WriteMessage(FormatMessage(message));
        }

        /// <summary>
        /// Writes the message to the underlying setup log.
        /// </summary>
        /// <param name="message">The message to write.</param>
        protected abstract void WriteMessage(string message);

        /// <summary>
        /// Creates a formatted message, prefixed with the process ID.
        /// </summary>
        /// <param name="message">The message to format.</param>
        /// <returns>The formatted message.</returns>
        protected string FormatMessage(string message)
        {
            return $"[{Environment.ProcessId:X8}] {message}";
        }
    }
}
