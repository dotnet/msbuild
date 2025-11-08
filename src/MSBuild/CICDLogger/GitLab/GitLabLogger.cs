// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

#nullable enable

namespace Microsoft.Build.CommandLine.CICDLogger.GitLab;

/// <summary>
/// Data captured from project evaluation.
/// </summary>
public sealed class GitLabEvalData
{
    public string? TargetFramework { get; set; }
    public string? RuntimeIdentifier { get; set; }
}

/// <summary>
/// Wrapper for build events with timestamps to maintain ordering.
/// </summary>
public sealed class TimestampedBuildEvent
{
    public DateTime Timestamp { get; }
    public BuildEventArgs Event { get; }

    public TimestampedBuildEvent(BuildEventArgs evt)
    {
        Event = evt;
        Timestamp = evt.Timestamp;
    }
}

/// <summary>
/// Data stored for each project during the build.
/// </summary>
public sealed class GitLabProjectData
{
    public string? ProjectFile { get; set; }
    public string? TargetFramework { get; set; }
    public string? RuntimeIdentifier { get; set; }
    public List<TimestampedBuildEvent> Events { get; } = new();
    public int SectionId { get; set; }
    
    public int ErrorCount => Events.Count(e => e.Event is BuildErrorEventArgs);
    public int WarningCount => Events.Count(e => e.Event is BuildWarningEventArgs);
}

/// <summary>
/// Data stored for the entire build session.
/// </summary>
public sealed class GitLabBuildData
{
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
    public int NextSectionId { get; set; } = 1;
}

/// <summary>
/// Logger for GitLab CI that formats build diagnostics using ANSI color codes and collapsible sections.
/// See: https://docs.gitlab.com/ee/ci/jobs/#custom-collapsible-sections
/// </summary>
public sealed class GitLabLogger : ProjectTrackingLoggerBase<GitLabEvalData, object?, GitLabProjectData, GitLabBuildData>
{
    private Action<string> _write = Console.Out.Write;

    /// <inheritdoc/>
    public override LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public override string? Parameters { get; set; }

    /// <summary>
    /// Detects if GitLab CI environment is active.
    /// </summary>
    /// <returns>true if running in GitLab CI; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITLAB_CI"));
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
    }

    #region Abstract method implementations

    protected override GitLabEvalData CreateEvalData(ProjectEvaluationFinishedEventArgs e)
    {
        var evalData = new GitLabEvalData();

        // Extract target framework and runtime identifier from evaluation properties
        if (e.Properties != null)
        {
            foreach (var item in e.Properties)
            {
                if (item is System.Collections.DictionaryEntry kvp)
                {
                    var key = kvp.Key as string;
                    if (key == "TargetFramework")
                    {
                        evalData.TargetFramework = kvp.Value?.ToString();
                    }
                    else if (key == "RuntimeIdentifier")
                    {
                        evalData.RuntimeIdentifier = kvp.Value?.ToString();
                    }
                }
            }
        }

        return evalData;
    }

    protected override GitLabProjectData? CreateProjectData(GitLabEvalData evalData, ProjectStartedEventArgs e)
    {
        return new GitLabProjectData
        {
            ProjectFile = e.ProjectFile,
            TargetFramework = evalData?.TargetFramework,
            RuntimeIdentifier = evalData?.RuntimeIdentifier
        };
    }

    protected override GitLabBuildData CreateBuildData(BuildStartedEventArgs e)
    {
        return new GitLabBuildData();
    }

    #endregion

    #region Event handlers

    protected override void OnErrorRaised(BuildErrorEventArgs e, GitLabProjectData? projectData, GitLabBuildData buildData)
    {
        buildData.TotalErrors++;

        if (projectData != null)
        {
            // Buffer error for output at project finished
            projectData.Events.Add(new TimestampedBuildEvent(e));
        }
        else
        {
            // No project context, write immediately
            WriteDiagnostic("ERROR", "\x1b[31m", e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);
        }
    }

    protected override void OnWarningRaised(BuildWarningEventArgs e, GitLabProjectData? projectData, GitLabBuildData buildData)
    {
        buildData.TotalWarnings++;

        if (projectData != null)
        {
            // Buffer warning for output at project finished
            projectData.Events.Add(new TimestampedBuildEvent(e));
        }
        else
        {
            // No project context, write immediately
            WriteDiagnostic("WARNING", "\x1b[33m", e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message);
        }
    }

    protected override void OnMessageRaised(BuildMessageEventArgs e, GitLabProjectData? projectData, GitLabBuildData buildData)
    {
        if (Verbosity == LoggerVerbosity.Quiet)
        {
            return;
        }

        // First question: should I care about this message?
        bool shouldLog = e.Importance == MessageImportance.High ||
                        (e.Importance == MessageImportance.Normal && Verbosity >= LoggerVerbosity.Normal) ||
                        (e.Importance == MessageImportance.Low && Verbosity >= LoggerVerbosity.Detailed);

        if (!shouldLog)
        {
            return;
        }

        // Second question: do I log it now or at the end of the project build?
        if (projectData != null)
        {
            // Buffer for output at project finished
            projectData.Events.Add(new TimestampedBuildEvent(e));
        }
        else
        {
            // No project context, write immediately
            _write(e.Message ?? string.Empty);
            _write(Environment.NewLine);
        }
    }

    protected override void OnProjectStarted(ProjectStartedEventArgs e, GitLabEvalData evalData, GitLabProjectData projectData, GitLabBuildData buildData)
    {
        // Assign section ID when project starts
        projectData.SectionId = buildData.NextSectionId++;
    }

    protected override void OnProjectFinished(ProjectFinishedEventArgs e, GitLabProjectData projectData, GitLabBuildData buildData)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            // Build project header with context information
            var headerText = "Building ";
            headerText += projectData.ProjectFile ?? e.ProjectFile ?? "project";

            if (!string.IsNullOrEmpty(projectData.TargetFramework))
            {
                headerText += " (";
                headerText += projectData.TargetFramework;

                if (!string.IsNullOrEmpty(projectData.RuntimeIdentifier))
                {
                    headerText += " | ";
                    headerText += projectData.RuntimeIdentifier;
                }

                headerText += ")";
            }

            // Add success/failure status
            if (!e.Succeeded)
            {
                headerText += " - Failed";
            }
            else if (projectData.ErrorCount > 0 || projectData.WarningCount > 0)
            {
                headerText += $" - {projectData.ErrorCount} error(s), {projectData.WarningCount} warning(s)";
            }

            // Use collapsible sections in GitLab CI
            // Format: \e[0Ksection_start:TIMESTAMP:SECTION_NAME\r\e[0K
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sectionName = $"build_project_{projectData.SectionId}";

            _write($"\x1b[0Ksection_start:{timestamp}:{sectionName}\r\x1b[0K");
            _write($"\x1b[36m{headerText}\x1b[0m");  // Cyan color for header
            _write(Environment.NewLine);

            // Output all events in timestamp order
            foreach (var timestampedEvent in projectData.Events.OrderBy(e => e.Timestamp))
            {
                switch (timestampedEvent.Event)
                {
                    case BuildErrorEventArgs error:
                        WriteDiagnostic("ERROR", "\x1b[31m", error.File, error.LineNumber, error.ColumnNumber, error.Code, error.Message);
                        break;
                    case BuildWarningEventArgs warning:
                        WriteDiagnostic("WARNING", "\x1b[33m", warning.File, warning.LineNumber, warning.ColumnNumber, warning.Code, warning.Message);
                        break;
                    case BuildMessageEventArgs message:
                        _write(message.Message ?? string.Empty);
                        _write(Environment.NewLine);
                        break;
                }
            }

            // End collapsible section
            // Format: \e[0Ksection_end:TIMESTAMP:SECTION_NAME\r\e[0K
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _write($"\x1b[0Ksection_end:{timestamp}:{sectionName}\r\x1b[0K");
        }
    }

    protected override void OnBuildFinished(BuildFinishedEventArgs e, GitLabProjectData[] projectData, GitLabBuildData buildData)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            if (e.Succeeded)
            {
                _write("\x1b[32m");  // Green
                _write($"Build succeeded. {buildData.TotalWarnings} warning(s)");
            }
            else
            {
                _write("\x1b[31m");  // Red
                _write($"Build failed. {buildData.TotalErrors} error(s), {buildData.TotalWarnings} warning(s)");
            }
            _write("\x1b[0m");  // Reset color
            _write(Environment.NewLine);
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Writes a diagnostic (error or warning) using GitLab formatting.
    /// </summary>
    private void WriteDiagnostic(string type, string colorCode, string? file, int lineNumber, int columnNumber, string? code, string? message)
    {
        // GitLab uses ANSI color codes for formatting
        _write(colorCode);  // Color code (red for errors, yellow for warnings)
        _write(type);
        _write(": ");

        if (!string.IsNullOrEmpty(file))
        {
            _write(file);

            if (lineNumber > 0)
            {
                _write("(");
                _write(lineNumber.ToString());

                if (columnNumber > 0)
                {
                    _write(",");
                    _write(columnNumber.ToString());
                }

                _write(")");
            }

            _write(": ");
        }

        if (!string.IsNullOrEmpty(code))
        {
            _write(code);
            _write(": ");
        }

        _write(message ?? string.Empty);
        _write("\x1b[0m");  // Reset color
        _write(Environment.NewLine);
    }

    #endregion
}
