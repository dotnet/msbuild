// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Interface to define a logging abstraction for APICompat suppressions shared between Console and MSBuild tasks.
    /// </summary>
    public interface ISuppressableLog : ILog
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
        /// Reports whether the logger emitted a compatibility suppression.
        /// </summary>
        bool SuppressionWasLogged { get; }
    }
}
