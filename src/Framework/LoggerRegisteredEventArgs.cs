// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Describes a single registered logger.
    /// </summary>
    [Serializable]
    public sealed class RegisteredLoggerInfo
    {
        /// <summary>
        /// Initialize a new instance of the RegisteredLoggerInfo class.
        /// </summary>
        public RegisteredLoggerInfo(string loggerName, IReadOnlyList<string>? outputFilePaths = null, LoggerVerbosity? verbosity = null)
        {
            LoggerName = loggerName;
            OutputFilePaths = outputFilePaths ?? Array.Empty<string>();
            Verbosity = verbosity;
        }

        /// <summary>
        /// The name of the logger.
        /// </summary>
        public string LoggerName { get; }

        /// <summary>
        /// The output file paths for the logger.
        /// </summary>
        public IReadOnlyList<string> OutputFilePaths { get; }

        /// <summary>
        /// The verbosity level of the logger.
        /// </summary>
        public LoggerVerbosity? Verbosity { get; }
    }

    /// <summary>
    /// Arguments for the logger registered event, containing one or more logger registrations.
    /// </summary>
    [Serializable]
    public sealed class LoggerRegisteredEventArgs : BuildStatusEventArgs
    {
        internal LoggerRegisteredEventArgs()
        {
        }

        /// <summary>
        /// Initialize a new instance of the LoggerRegisteredEventArgs class.
        /// </summary>
        /// <param name="loggers">The list of registered loggers.</param>
        public LoggerRegisteredEventArgs(IReadOnlyList<RegisteredLoggerInfo> loggers)
            : base(FormatMessage(loggers), null, null)
        {
            Loggers = loggers;
        }

        /// <summary>
        /// Formats a summary message listing loggers with output paths.
        /// This serves as a fallback message for loggers that do not handle this event.
        /// </summary>
        private static string? FormatMessage(IReadOnlyList<RegisteredLoggerInfo> loggers)
        {
            var withPaths = loggers.Where(l => l.OutputFilePaths.Count > 0).ToList();
            if (withPaths.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", withPaths.Select(l => $"{l.LoggerName}: {string.Join(", ", l.OutputFilePaths)}"));
        }

        /// <summary>
        /// The registered loggers.
        /// </summary>
        public IReadOnlyList<RegisteredLoggerInfo> Loggers { get; internal set; } = Array.Empty<RegisteredLoggerInfo>();

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.Write(Loggers.Count);
            foreach (var logger in Loggers)
            {
                writer.Write(logger.LoggerName);
                writer.Write(logger.Verbosity.HasValue);
                if (logger.Verbosity.HasValue)
                {
                    writer.Write((int)logger.Verbosity.Value);
                }

                writer.Write(logger.OutputFilePaths.Count);
                foreach (var path in logger.OutputFilePaths)
                {
                    writer.Write(path);
                }
            }
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            int count = reader.ReadInt32();
            var loggers = new List<RegisteredLoggerInfo>(count);
            for (int i = 0; i < count; i++)
            {
                string loggerName = reader.ReadString();

                LoggerVerbosity? verbosity = null;
                if (reader.ReadBoolean())
                {
                    verbosity = (LoggerVerbosity)reader.ReadInt32();
                }

                int pathCount = reader.ReadInt32();
                var outputFilePaths = new string[pathCount];
                for (int j = 0; j < pathCount; j++)
                {
                    outputFilePaths[j] = reader.ReadString();
                }

                loggers.Add(new RegisteredLoggerInfo(loggerName, outputFilePaths, verbosity));
            }

            Loggers = loggers;
        }
    }
}
