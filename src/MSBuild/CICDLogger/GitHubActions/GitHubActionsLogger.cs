// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.CommandLine.CICDLogger.GitHubActions;

/// <summary>
/// Logger for GitHub Actions that formats build diagnostics using GitHub Actions workflow commands.
/// See: https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions
/// </summary>
public sealed class GitHubActionsLogger : INodeLogger
{
    private Action<string> _write = Console.Out.Write;

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public string Parameters { get; set; }

    /// <summary>
    /// Detects if GitHub Actions environment is active.
    /// </summary>
    /// <returns>true if running in GitHub Actions; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return Traits.IsEnvVarOneOrTrue("GITHUB_ACTIONS");
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
        // Format: ::error file={name},line={line},col={col},endColumn={endCol},title={title}::{message}
        var output = new StringBuilder();
        output.Append("::error");

        if (!string.IsNullOrEmpty(e.File))
        {
            output.Append(" file=");
            output.Append(EscapeProperty(e.File));

            if (e.LineNumber > 0)
            {
                output.Append(",line=");
                output.Append(e.LineNumber);

                if (e.ColumnNumber > 0)
                {
                    output.Append(",col=");
                    output.Append(e.ColumnNumber);

                    if (e.EndColumnNumber > 0)
                    {
                        output.Append(",endColumn=");
                        output.Append(e.EndColumnNumber);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            output.Append(",title=");
            output.Append(EscapeProperty(e.Code));
        }

        output.Append("::");
        output.Append(EscapeData(e.Message));
        output.AppendLine();

        _write(output.ToString());
    }

    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        // Format: ::warning file={name},line={line},col={col},endColumn={endCol},title={title}::{message}
        var output = new StringBuilder();
        output.Append("::warning");

        if (!string.IsNullOrEmpty(e.File))
        {
            output.Append(" file=");
            output.Append(EscapeProperty(e.File));

            if (e.LineNumber > 0)
            {
                output.Append(",line=");
                output.Append(e.LineNumber);

                if (e.ColumnNumber > 0)
                {
                    output.Append(",col=");
                    output.Append(e.ColumnNumber);

                    if (e.EndColumnNumber > 0)
                    {
                        output.Append(",endColumn=");
                        output.Append(e.EndColumnNumber);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            output.Append(",title=");
            output.Append(EscapeProperty(e.Code));
        }

        output.Append("::");
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
            // Use groups to collapse project output in GitHub Actions
            _write($"::group::Building {e.ProjectFile ?? "project"}");
            _write(Environment.NewLine);
        }
    }

    private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            _write("::endgroup::");
            _write(Environment.NewLine);
        }
    }

    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            _write(e.Succeeded ? "Build succeeded." : "Build failed.");
            _write(Environment.NewLine);
        }
    }

    /// <summary>
    /// Escapes special characters in property values for GitHub Actions workflow commands.
    /// </summary>
    private static string EscapeProperty(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace("%", "%25")
                    .Replace("\r", "%0D")
                    .Replace("\n", "%0A")
                    .Replace(":", "%3A")
                    .Replace(",", "%2C");
    }

    /// <summary>
    /// Escapes special characters in message data for GitHub Actions workflow commands.
    /// </summary>
    private static string EscapeData(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace("%", "%25")
                    .Replace("\r", "%0D")
                    .Replace("\n", "%0A");
    }
}
