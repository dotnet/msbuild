// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A logger that captures all events to a JSON file for analysis.
    /// This logger listens to the same events as TerminalLogger.
    /// </summary>
    public class EventLogger : ILogger
    {
        private string? _outputFile;
        private StreamWriter? _writer;
        private readonly List<object> _events = new();

        public string? Parameters { get; set; }
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

        public void Initialize(IEventSource eventSource)
        {
            // Parse output file from parameters
            _outputFile = Parameters ?? "events.json";
            _writer = new StreamWriter(_outputFile, false);

            // Subscribe to the same events as TerminalLogger
            eventSource.BuildStarted += BuildStarted;
            eventSource.BuildFinished += BuildFinished;
            eventSource.ProjectStarted += ProjectStarted;
            eventSource.ProjectFinished += ProjectFinished;
            eventSource.TargetStarted += TargetStarted;
            eventSource.TargetFinished += TargetFinished;
            eventSource.TaskStarted += TaskStarted;
            eventSource.StatusEventRaised += StatusEventRaised;
            eventSource.MessageRaised += MessageRaised;
            eventSource.WarningRaised += WarningRaised;
            eventSource.ErrorRaised += ErrorRaised;
        }

        public void Shutdown()
        {
            if (_writer != null)
            {
                _writer.Close();
                _writer.Dispose();
            }
        }

        private void WriteEvent(string eventType, object eventData)
        {
            var eventInfo = new
            {
                EventType = eventType,
                Timestamp = DateTime.Now,
                Data = eventData
            };

            var json = JsonSerializer.Serialize(eventInfo);
            
            lock (_events)
            {
                _writer?.WriteLine(json);
                _writer?.Flush();
                _events.Add(eventInfo);
            }
        }

        private void BuildStarted(object sender, BuildStartedEventArgs e)
        {
            WriteEvent("BuildStarted", new { e.Message, e.BuildEventContext, e.Timestamp });
        }

        private void BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            WriteEvent("BuildFinished", new { e.Message, e.Succeeded, e.BuildEventContext, e.Timestamp });
        }

        private void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            WriteEvent("ProjectStarted", new
            {
                e.Message,
                e.ProjectFile,
                e.TargetNames,
                e.BuildEventContext,
                e.ParentProjectBuildEventContext,
                e.Timestamp,
                GlobalProperties = e.GlobalProperties != null ? new Dictionary<string, string>(e.GlobalProperties) : null
            });
        }

        private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            WriteEvent("ProjectFinished", new
            {
                e.Message,
                e.ProjectFile,
                e.Succeeded,
                e.BuildEventContext,
                e.Timestamp
            });
        }

        private void TargetStarted(object sender, TargetStartedEventArgs e)
        {
            WriteEvent("TargetStarted", new
            {
                e.Message,
                e.TargetName,
                e.ProjectFile,
                e.TargetFile,
                e.BuildEventContext,
                e.ParentTarget,
                e.Timestamp
            });
        }

        private void TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            WriteEvent("TargetFinished", new
            {
                e.Message,
                e.TargetName,
                e.ProjectFile,
                e.TargetFile,
                e.Succeeded,
                e.BuildEventContext,
                e.Timestamp
            });
        }

        private void TaskStarted(object sender, TaskStartedEventArgs e)
        {
            WriteEvent("TaskStarted", new
            {
                e.Message,
                e.TaskName,
                e.ProjectFile,
                e.TaskFile,
                e.BuildEventContext,
                e.Timestamp
            });
        }

        private void StatusEventRaised(object sender, BuildStatusEventArgs e)
        {
            WriteEvent("StatusEventRaised", new
            {
                e.Message,
                e.BuildEventContext,
                e.Timestamp,
                EventType = e.GetType().Name
            });
        }

        private void MessageRaised(object sender, BuildMessageEventArgs e)
        {
            WriteEvent("MessageRaised", new
            {
                e.Message,
                e.Code,
                e.File,
                e.LineNumber,
                e.ColumnNumber,
                e.ProjectFile,
                e.Importance,
                e.BuildEventContext,
                e.Timestamp,
                EventType = e.GetType().Name
            });
        }

        private void WarningRaised(object sender, BuildWarningEventArgs e)
        {
            WriteEvent("WarningRaised", new
            {
                e.Message,
                e.Code,
                e.File,
                e.LineNumber,
                e.ColumnNumber,
                e.ProjectFile,
                e.Subcategory,
                e.BuildEventContext,
                e.Timestamp
            });
        }

        private void ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            WriteEvent("ErrorRaised", new
            {
                e.Message,
                e.Code,
                e.File,
                e.LineNumber,
                e.ColumnNumber,
                e.ProjectFile,
                e.Subcategory,
                e.BuildEventContext,
                e.Timestamp
            });
        }
    }
}
