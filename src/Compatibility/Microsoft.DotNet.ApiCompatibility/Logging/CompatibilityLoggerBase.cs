// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// The compatibility logger base class that is used to emit messages, warnings and errors suppression files.
    /// </summary>
    public abstract class CompatibilityLoggerBase
    {
        /// <summary>
        /// If true, all errors are baselined instead of emitted.
        /// </summary>
        public bool BaselineAllErrors { get; }

        /// <summary>
        /// The engine that is used to handle suppressions and generate the suppression file.
        /// </summary>
        public SuppressionEngine SuppressionEngine { get; }

        /// <summary>
        /// The path to the suppression file to read from or to generate.
        /// </summary>
        public string? SuppressionsFile { get; }

        /// <summary>
        /// Generates a compatibility logger and the underlying suppression engine.
        /// </summary>
        /// <param name="suppressionsFile">The path to the suppression file to read from or to generate.</param>
        /// <param name="baselineAllErrors">If true, all errors are baselined.</param>
        /// <param name="noWarn">Suppression ids to suppress specific errors. Multiple suppressions are separated by a ';' character.</param>
        public CompatibilityLoggerBase(string? suppressionsFile, bool baselineAllErrors, string? noWarn)
        {
            SuppressionsFile = suppressionsFile;
            BaselineAllErrors = baselineAllErrors;
            SuppressionEngine = baselineAllErrors && !File.Exists(suppressionsFile) ?
                new SuppressionEngine(noWarn: noWarn) :
                new SuppressionEngine(suppressionsFile, noWarn);
        }

        /// <summary>
        /// Log an error based on a passed in suppression, code, format and additional arguments.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// <returns>Returns true if the error is logged and not suppressed.</returns>
        public abstract bool LogError(Suppression suppression, string code, string format, params string[] args);

        /// <summary>
        /// Log a warning based on the passed in suppression, code, format and additional arguments.
        /// </summary>
        /// <param name="suppression">The suppression object which contains the rule information.</param>
        /// <param name="code">The suppression code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// <returns>Returns true if the warning is logged and not suppressed.</returns>
        public abstract bool LogWarning(Suppression suppression, string code, string format, params string[] args);

        /// <summary>
        /// Log a message based on the passed in importance, format and arguments.
        /// </summary>
        /// <param name="importance">The message importance</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        public abstract void LogMessage(MessageImportance importance, string format, params string[] args);

        /// <summary>
        /// Generates a suppression file that contains the suppressed errors.
        /// </summary>
        /// <exception cref="ArgumentNullException">Throws if a suppressions file path isn't set.</exception>
        public void WriteSuppressionFile()
        {
            if (SuppressionsFile is null)
            {
                throw new ArgumentException(Resources.SuppressionsFileNotSpecified, nameof(SuppressionsFile));
            }

            if (SuppressionEngine.WriteSuppressionsToFile(SuppressionsFile))
            {
                LogMessage(MessageImportance.High, Resources.WroteSuppressions, SuppressionsFile);
            }
        }
    }
}
