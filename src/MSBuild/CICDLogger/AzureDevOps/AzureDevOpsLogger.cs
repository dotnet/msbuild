// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.CommandLine.CICDLogger.AzureDevOps;

/// <summary>
/// Logger for Azure DevOps that formats build diagnostics using Azure Pipelines logging commands.
/// See: https://learn.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands
/// </summary>
public sealed class AzureDevOpsLogger : INodeLogger
{
    private Action<string> _write = Console.Out.Write;

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public string Parameters { get; set; }

    /// <summary>
    /// Detects if Azure DevOps environment is active.
    /// </summary>
    /// <returns>true if running in Azure DevOps; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource)
    {
        eventSource.ErrorRaised += ErrorRaised;
        eventSource.WarningRaised += WarningRaised;
        eventSource.MessageRaised += MessageRaised;
        eventSource.ProjectStarted += ProjectStarted;
        eventSource.ProjectFinished += ProjectFinished;
        eventSource.BuildFinished += BuildFinished;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
    }

    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        // Format: ##vso[task.logissue type=error;sourcepath=file;linenumber=line;columnnumber=col;code=code;]message
        var output = new StringBuilder();
        output.Append("##vso[task.logissue type=error");

        if (!string.IsNullOrEmpty(e.File))
        {
            output.Append(";sourcepath=");
            output.Append(EscapeProperty(e.File));

            if (e.LineNumber > 0)
            {
                output.Append(";linenumber=");
                output.Append(e.LineNumber);

                if (e.ColumnNumber > 0)
                {
                    output.Append(";columnnumber=");
                    output.Append(e.ColumnNumber);
                }
            }
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            output.Append(";code=");
            output.Append(EscapeProperty(e.Code));
        }

        output.Append(']');
        output.Append(EscapeData(e.Message));
        output.AppendLine();

        _write(output.ToString());
    }

    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        // Format: ##vso[task.logissue type=warning;sourcepath=file;linenumber=line;columnnumber=col;code=code;]message
        var output = new StringBuilder();
        output.Append("##vso[task.logissue type=warning");

        if (!string.IsNullOrEmpty(e.File))
        {
            output.Append(";sourcepath=");
            output.Append(EscapeProperty(e.File));

            if (e.LineNumber > 0)
            {
                output.Append(";linenumber=");
                output.Append(e.LineNumber);

                if (e.ColumnNumber > 0)
                {
                    output.Append(";columnnumber=");
                    output.Append(e.ColumnNumber);
                }
            }
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            output.Append(";code=");
            output.Append(EscapeProperty(e.Code));
        }

        output.Append(']');
        output.Append(EscapeData(e.Message));
        output.AppendLine();

        _write(output.ToString());
    }

    private void MessageRaised(object sender, BuildMessageEventArgs e)
    {
        if (Verbosity == LoggerVerbosity.Quiet)
        {
            return;
        }

        if (e.Importance == MessageImportance.High ||
            (e.Importance == MessageImportance.Normal && Verbosity >= LoggerVerbosity.Normal) ||
            (e.Importance == MessageImportance.Low && Verbosity >= LoggerVerbosity.Detailed))
        {
            _write(e.Message);
            _write(Environment.NewLine);
        }
    }

    private void ProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            // Use sections to group project output in Azure DevOps
            _write($"##[section]Building {e.ProjectFile ?? "project"}");
            _write(Environment.NewLine);
        }
    }

    private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        // Azure DevOps doesn't require explicit section end
    }

    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            if (!e.Succeeded)
            {
                _write("##vso[task.complete result=Failed]Build failed.");
            }
            else
            {
                _write("Build succeeded.");
            }
            _write(Environment.NewLine);
        }
    }

    /// <summary>
    /// Escapes special characters in property values for Azure DevOps logging commands.
    /// </summary>
    private static string EscapeProperty(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace(";", "%3B")
                    .Replace("\r", "%0D")
                    .Replace("\n", "%0A")
                    .Replace("]", "%5D");
    }

    /// <summary>
    /// Escapes special characters in message data for Azure DevOps logging commands.
    /// </summary>
    private static string EscapeData(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace("\r", "%0D")
                    .Replace("\n", "%0A")
                    .Replace("]", "%5D");
    }
}
