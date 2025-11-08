// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.CommandLine.CICDLogger.GitLab;

/// <summary>
/// Logger for GitLab CI that formats build diagnostics using ANSI color codes and collapsible sections.
/// See: https://docs.gitlab.com/ee/ci/jobs/#custom-collapsible-sections
/// </summary>
public sealed class GitLabLogger : INodeLogger
{
    private Action<string> _write = Console.Out.Write;
    private int _sectionId = 0;

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public string? Parameters { get; set; }

    /// <summary>
    /// Detects if GitLab CI environment is active.
    /// </summary>
    /// <returns>true if running in GitLab CI; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITLAB_CI"));
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
        // GitLab uses ANSI color codes for formatting
        // Red color for errors
        _write("\x1b[31m");  // Red
        _write("ERROR: ");

        if (!string.IsNullOrEmpty(e.File))
        {
            _write(e.File);

            if (e.LineNumber > 0)
            {
                _write("(");
                _write(e.LineNumber.ToString());

                if (e.ColumnNumber > 0)
                {
                    _write(",");
                    _write(e.ColumnNumber.ToString());
                }

                _write(")");
            }

            _write(": ");
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            _write(e.Code);
            _write(": ");
        }

        _write(e.Message ?? string.Empty);
        _write("\x1b[0m");  // Reset color
        _write(Environment.NewLine);
    }

    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        // Yellow color for warnings
        _write("\x1b[33m");  // Yellow
        _write("WARNING: ");

        if (!string.IsNullOrEmpty(e.File))
        {
            _write(e.File);

            if (e.LineNumber > 0)
            {
                _write("(");
                _write(e.LineNumber.ToString());

                if (e.ColumnNumber > 0)
                {
                    _write(",");
                    _write(e.ColumnNumber.ToString());
                }

                _write(")");
            }

            _write(": ");
        }

        if (!string.IsNullOrEmpty(e.Code))
        {
            _write(e.Code);
            _write(": ");
        }

        _write(e.Message ?? string.Empty);
        _write("\x1b[0m");  // Reset color
        _write(Environment.NewLine);
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
            _write(e.Message ?? string.Empty);
            _write(Environment.NewLine);
        }
    }

    private void ProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            // Use collapsible sections in GitLab CI
            // Format: \e[0Ksection_start:TIMESTAMP:SECTION_NAME\r\e[0K
            _sectionId++;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sectionName = $"build_project_{_sectionId}";
            
            _write($"\x1b[0Ksection_start:{timestamp}:{sectionName}\r\x1b[0K");
            _write($"\x1b[36mBuilding {e.ProjectFile ?? "project"}\x1b[0m");  // Cyan color
            _write(Environment.NewLine);
        }
    }

    private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            // End collapsible section
            // Format: \e[0Ksection_end:TIMESTAMP:SECTION_NAME\r\e[0K
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sectionName = $"build_project_{_sectionId}";
            
            _write($"\x1b[0Ksection_end:{timestamp}:{sectionName}\r\x1b[0K");
        }
    }

    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            if (e.Succeeded)
            {
                _write("\x1b[32m");  // Green
                _write("Build succeeded.");
            }
            else
            {
                _write("\x1b[31m");  // Red
                _write("Build failed.");
            }
            _write("\x1b[0m");  // Reset color
            _write(Environment.NewLine);
        }
    }
}
