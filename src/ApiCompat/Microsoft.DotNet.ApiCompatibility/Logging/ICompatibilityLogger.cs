// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// The compatibility logger base class that is used to emit messages, warnings and errors suppression files.
    /// </summary>
    public interface ICompatibilityLogger
    {
        /// <summary>
        /// Log an error based on a passed in suppression, code, format and additional arguments.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// <returns>Returns true if the error is logged and not suppressed.</returns>
        bool LogError(Suppression suppression, string code, string format, params string[] args);

        /// <summary>
        /// Log a warning based on the passed in suppression, code, format and additional arguments.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// <returns>Returns true if the warning is logged and not suppressed.</returns>
        bool LogWarning(Suppression suppression, string code, string format, params string[] args);

        /// <summary>
        /// Log a message based on the passed in importance, format and arguments.
        /// </summary>
        /// <param name="importance">The message importance</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        void LogMessage(MessageImportance importance, string format, params string[] args);
    }
}
