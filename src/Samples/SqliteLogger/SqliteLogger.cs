// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

#nullable enable

namespace SqliteLogger
{
    /// <summary>
    /// MSBuild logger that writes build events to a SQLite database for
    /// easy querying with standard SQL tools.
    ///
    /// Usage:
    ///   msbuild Foo.sln -logger:SqliteLogger,SqliteLogger.dll;LogFile=output.sqlite
    ///
    /// Parameters (semicolon-delimited):
    ///   LogFile=path.sqlite   Output file path (default: msbuild.sqlite)
    ///   IncludeTaskInputs     Record task input/output parameters
    ///   VerboseMessages       Include Low importance messages
    ///   NoEvalProperties      Skip evaluation properties
    ///   NoEvalItems           Skip evaluation items
    /// </summary>
    public sealed class SqliteLogger : INodeLogger
    {
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
        public string? Parameters { get; set; }

        private DatabaseWriter? _writer;

        // Parsed parameters
        private string _filePath = "msbuild.sqlite";
        private bool _includeTaskInputs;
        private bool _verboseMessages;
        private bool _includeEvalProperties = true;
        private bool _includeEvalItems = true;

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        public void Initialize(IEventSource eventSource)
        {
            ProcessParameters();

            _writer = new DatabaseWriter(_filePath, _includeTaskInputs, _verboseMessages, _includeEvalProperties, _includeEvalItems);

            // Opt-in to detailed events
            if (_includeTaskInputs && eventSource is IEventSource3 es3)
            {
                es3.IncludeTaskInputs();
            }

            if ((_includeEvalProperties || _includeEvalItems) && eventSource is IEventSource4 es4)
            {
                es4.IncludeEvaluationPropertiesAndItems();
            }

            // Subscribe to events
            eventSource.BuildStarted += (_, e) => _writer.OnBuildStarted(e);
            eventSource.BuildFinished += (_, e) => _writer.OnBuildFinished(e);
            eventSource.StatusEventRaised += (_, e) => _writer.OnStatusEvent(e);
            eventSource.ErrorRaised += (_, e) => _writer.OnError(e);
            eventSource.WarningRaised += (_, e) => _writer.OnWarning(e);
            eventSource.MessageRaised += (_, e) => _writer.OnMessage(e);
        }

        public void Shutdown()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private void ProcessParameters()
        {
            if (Parameters is null)
            {
                return;
            }

            foreach (string param in Parameters.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = param.Trim();

                if (trimmed.StartsWith("LogFile=", StringComparison.OrdinalIgnoreCase))
                {
                    _filePath = trimmed.Substring("LogFile=".Length).Trim('"');
                    continue;
                }

                if (string.Equals(trimmed, "IncludeTaskInputs", StringComparison.OrdinalIgnoreCase))
                {
                    _includeTaskInputs = true;
                    continue;
                }

                if (string.Equals(trimmed, "VerboseMessages", StringComparison.OrdinalIgnoreCase))
                {
                    _verboseMessages = true;
                    continue;
                }

                if (string.Equals(trimmed, "NoEvalProperties", StringComparison.OrdinalIgnoreCase))
                {
                    _includeEvalProperties = false;
                    continue;
                }

                if (string.Equals(trimmed, "NoEvalItems", StringComparison.OrdinalIgnoreCase))
                {
                    _includeEvalItems = false;
                    continue;
                }

                // If it ends with .sqlite or .db, treat as file path
                if (trimmed.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    _filePath = trimmed;
                    continue;
                }
            }
        }
    }
}
