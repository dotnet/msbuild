// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the logger registered event, indicating that a logger has been registered.
    /// </summary>
    [Serializable]
    public class LoggerRegisteredEventArgs : BuildMessageEventArgs
    {
        protected LoggerRegisteredEventArgs()
        {
        }
        /// <summary>
        /// Initialize a new instance of the LoggerRegisteredEventArgs class.
        /// </summary>
        /// <param name="loggerName">The name of the logger.</param>
        /// <param name="outputFilePath">The full path to the log output file.</param>
        /// <param name="verbosity">The verbosity level of the logger.</param>
        /// <param name="additionalOutputFilePaths">Additional output file paths for the logger.</param>
        public LoggerRegisteredEventArgs(string loggerName, string? outputFilePath, LoggerVerbosity? verbosity, IReadOnlyList<string>? additionalOutputFilePaths)
            : base(FormatMessage(loggerName, outputFilePath), null, null, MessageImportance.Low)
        {
            OutputFilePath = outputFilePath;
            LoggerName = loggerName;
            Verbosity = verbosity;
            AdditionalOutputFilePaths = additionalOutputFilePaths;
        }
        /// <summary>
        /// Formats the message for the logger registered event, including the logger name and output file path if available. This only serves as a message for loggers that do not handle this event.
        /// </summary>
        /// <param name="loggerName">The name of the logger.</param>
        /// <param name="outputFilePath">The full path to the log output file.</param>
        /// <returns>A formatted message string.</returns>
        private static string? FormatMessage(string loggerName, string? outputFilePath)
        {
            return !string.IsNullOrEmpty(outputFilePath)
                ? $"{loggerName}: {outputFilePath}"
                : null;
        }

        /// <summary>
        /// The name of the logger.
        /// </summary>
        public string LoggerName { get; set; } = string.Empty;

        /// <summary>
        /// The verbosity level of the logger.
        /// </summary>
        public LoggerVerbosity? Verbosity { get; set; }

        /// <summary>
        /// The full path to the log output file.
        /// </summary>
        public string? OutputFilePath { get; set; }

        /// <summary>
        /// Additional output file paths for the logger.
        /// </summary>
        public IReadOnlyList<string>? AdditionalOutputFilePaths { get; set; }
    }
}
