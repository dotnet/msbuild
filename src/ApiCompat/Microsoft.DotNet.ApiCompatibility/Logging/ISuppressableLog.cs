// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Interface to define a logging abstraction for APICompat suppressions shared between Console and MSBuild tasks.
    /// </summary>
    public interface ISuppressableLog : ILog
    {
        /// <summary>
        /// Reports whether the logger emitted an error suppression.
        /// </summary>
        bool HasLoggedErrorSuppressions { get; }

        /// <summary>
        /// Log an error based on a passed in suppression, code and message.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="message">The message</param>
        /// <returns>Returns true if the error is logged and not suppressed.</returns>
        bool LogError(Suppression suppression, string code, string message);

        /// <summary>
        /// Log a warning based on the passed in suppression, code and message.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="message">The message</param>
        /// <returns>Returns true if the warning is logged and not suppressed.</returns>
        bool LogWarning(Suppression suppression, string code, string message);
    }
}
