// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.CommandLine.CICDLogger.GitHubActions;

/// <summary>
/// Data captured from project evaluation.
/// </summary>
public sealed class GitHubActionsEvalData
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
public sealed class GitHubActionsProjectData
{
    public string? ProjectFile { get; set; }
    public string? TargetFramework { get; set; }
    public string? RuntimeIdentifier { get; set; }
    public List<TimestampedBuildEvent> Events { get; } = new();
    
    public int ErrorCount => Events.Count(e => e.Event is BuildErrorEventArgs);
    public int WarningCount => Events.Count(e => e.Event is BuildWarningEventArgs);
}

/// <summary>
/// Data stored for the entire build session.
/// </summary>
public sealed class GitHubActionsBuildData
{
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
}

/// <summary>
/// Logger for GitHub Actions that formats build diagnostics using GitHub Actions workflow commands.
/// See: https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions
/// </summary>
public sealed class GitHubActionsLogger : ProjectTrackingLoggerBase<GitHubActionsEvalData, object?, GitHubActionsProjectData, GitHubActionsBuildData>
{
    private Action<string> _write = Console.Out.Write;

    /// <inheritdoc/>
    public override LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public override string? Parameters { get; set; }

    /// <inheritdoc/>
    public override void Initialize(IEventSource eventSource, int nodeCount)
    {
        // Check for ACTIONS_STEP_DEBUG to force diagnostic verbosity
        if (Traits.IsEnvVarOneOrTrue("ACTIONS_STEP_DEBUG"))
        {
            Verbosity = LoggerVerbosity.Diagnostic;
        }

        base.Initialize(eventSource, nodeCount);
    }

    /// <summary>
    /// Detects if GitHub Actions environment is active.
    /// </summary>
    /// <returns>true if running in GitHub Actions; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return Traits.IsEnvVarOneOrTrue("GITHUB_ACTIONS");
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
    }

    #region Abstract method implementations

    protected override GitHubActionsEvalData CreateEvalData(ProjectEvaluationFinishedEventArgs e)
    {
        var evalData = new GitHubActionsEvalData();

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

    protected override GitHubActionsProjectData? CreateProjectData(GitHubActionsEvalData evalData, ProjectStartedEventArgs e)
    {
        return new GitHubActionsProjectData
        {
            ProjectFile = e.ProjectFile,
            TargetFramework = evalData?.TargetFramework,
            RuntimeIdentifier = evalData?.RuntimeIdentifier
        };
    }

    protected override GitHubActionsBuildData CreateBuildData(BuildStartedEventArgs e)
    {
        return new GitHubActionsBuildData();
    }

    #endregion

    #region Event handlers

    protected override void OnErrorRaised(BuildErrorEventArgs e, GitHubActionsProjectData? projectData, GitHubActionsBuildData buildData)
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
            WriteDiagnostic("error", e.File, e.LineNumber, e.ColumnNumber, e.EndColumnNumber, e.Code, e.Message);
        }
    }

    protected override void OnWarningRaised(BuildWarningEventArgs e, GitHubActionsProjectData? projectData, GitHubActionsBuildData buildData)
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
            WriteDiagnostic("warning", e.File, e.LineNumber, e.ColumnNumber, e.EndColumnNumber, e.Code, e.Message);
        }
    }

    protected override void OnMessageRaised(BuildMessageEventArgs e, GitHubActionsProjectData? projectData, GitHubActionsBuildData buildData)
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

    protected override void OnProjectFinished(ProjectFinishedEventArgs e, GitHubActionsProjectData projectData, GitHubActionsBuildData buildData)
    {
        if (Verbosity >= LoggerVerbosity.Normal)
        {
            // Build project header with context information
            var header = new StringBuilder();
            header.Append("Building ");
            header.Append(projectData.ProjectFile ?? e.ProjectFile ?? "project");

            if (!string.IsNullOrEmpty(projectData.TargetFramework))
            {
                header.Append(" (");
                header.Append(projectData.TargetFramework);

                if (!string.IsNullOrEmpty(projectData.RuntimeIdentifier))
                {
                    header.Append(" | ");
                    header.Append(projectData.RuntimeIdentifier);
                }

                header.Append(')');
            }

            // Add success/failure status
            if (!e.Succeeded)
            {
                header.Append(" - Failed");
            }
            else if (projectData.ErrorCount > 0 || projectData.WarningCount > 0)
            {
                header.Append($" - {projectData.ErrorCount} error(s), {projectData.WarningCount} warning(s)");
            }

            // Use groups to collapse project output in GitHub Actions
            _write($"::group::{header}");
            _write(Environment.NewLine);

            // Output all events in timestamp order
            foreach (var timestampedEvent in projectData.Events.OrderBy(e => e.Timestamp))
            {
                switch (timestampedEvent.Event)
                {
                    case BuildErrorEventArgs error:
                        WriteDiagnostic("error", error.File, error.LineNumber, error.ColumnNumber, error.EndColumnNumber, error.Code, error.Message);
                        break;
                    case BuildWarningEventArgs warning:
                        WriteDiagnostic("warning", warning.File, warning.LineNumber, warning.ColumnNumber, warning.EndColumnNumber, warning.Code, warning.Message);
                        break;
                    case BuildMessageEventArgs message:
                        _write(message.Message ?? string.Empty);
                        _write(Environment.NewLine);
                        break;
                }
            }

            _write("::endgroup::");
            _write(Environment.NewLine);
        }
    }

    protected override void OnBuildFinished(BuildFinishedEventArgs e, GitHubActionsProjectData[] projectData, GitHubActionsBuildData buildData)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            if (!e.Succeeded)
            {
                _write($"Build failed. {buildData.TotalErrors} error(s), {buildData.TotalWarnings} warning(s)");
            }
            else
            {
                _write($"Build succeeded. {buildData.TotalWarnings} warning(s)");
            }
            _write(Environment.NewLine);
        }

        // Write build summary to GITHUB_STEP_SUMMARY file if available
        WriteStepSummary(e, projectData, buildData);
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Writes the build summary to the GitHub Step Summary file.
    /// </summary>
    private void WriteStepSummary(BuildFinishedEventArgs e, GitHubActionsProjectData[] projectData, GitHubActionsBuildData buildData)
    {
        var summaryFile = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(summaryFile))
        {
            return;
        }

        try
        {
            using var writer = new System.IO.StreamWriter(summaryFile, append: true);
            
            // Write header
            writer.WriteLine("## Build Summary");
            writer.WriteLine();

            // Write overall status
            if (e.Succeeded)
            {
                writer.WriteLine("✅ **Build Succeeded**");
            }
            else
            {
                writer.WriteLine("❌ **Build Failed**");
            }
            writer.WriteLine();

            // Write summary table
            writer.WriteLine($"- **Total Errors:** {buildData.TotalErrors}");
            writer.WriteLine($"- **Total Warnings:** {buildData.TotalWarnings}");
            writer.WriteLine();

            // Write per-project details
            if (projectData.Length > 0)
            {
                writer.WriteLine("### Project Details");
                writer.WriteLine();

                foreach (var project in projectData)
                {
                    // Project header
                    writer.Write("#### ");
                    writer.Write(project.ProjectFile ?? "project");
                    
                    if (!string.IsNullOrEmpty(project.TargetFramework))
                    {
                        writer.Write(" (");
                        writer.Write(project.TargetFramework);
                        if (!string.IsNullOrEmpty(project.RuntimeIdentifier))
                        {
                            writer.Write(" | ");
                            writer.Write(project.RuntimeIdentifier);
                        }
                        writer.Write(")");
                    }
                    writer.WriteLine();
                    writer.WriteLine();

                    if (project.ErrorCount > 0 || project.WarningCount > 0)
                    {
                        writer.WriteLine($"- Errors: {project.ErrorCount}");
                        writer.WriteLine($"- Warnings: {project.WarningCount}");
                        writer.WriteLine();

                        // Write diagnostics ordered by timestamp
                        if (project.Events.Count > 0)
                        {
                            writer.WriteLine("<details>");
                            writer.WriteLine("<summary>View Diagnostics</summary>");
                            writer.WriteLine();

                            foreach (var timestampedEvent in project.Events.OrderBy(e => e.Timestamp))
                            {
                                switch (timestampedEvent.Event)
                                {
                                    case BuildErrorEventArgs error:
                                        writer.Write("❌ **Error** ");
                                        if (!string.IsNullOrEmpty(error.Code))
                                        {
                                            writer.Write($"`{error.Code}` ");
                                        }
                                        if (!string.IsNullOrEmpty(error.File))
                                        {
                                            writer.Write($"in `{error.File}`");
                                            if (error.LineNumber > 0)
                                            {
                                                writer.Write($" (line {error.LineNumber}");
                                                if (error.ColumnNumber > 0)
                                                {
                                                    writer.Write($", col {error.ColumnNumber}");
                                                }
                                                writer.Write(")");
                                            }
                                        }
                                        writer.WriteLine();
                                        writer.WriteLine($"  {error.Message}");
                                        writer.WriteLine();
                                        break;

                                    case BuildWarningEventArgs warning:
                                        writer.Write("⚠️ **Warning** ");
                                        if (!string.IsNullOrEmpty(warning.Code))
                                        {
                                            writer.Write($"`{warning.Code}` ");
                                        }
                                        if (!string.IsNullOrEmpty(warning.File))
                                        {
                                            writer.Write($"in `{warning.File}`");
                                            if (warning.LineNumber > 0)
                                            {
                                                writer.Write($" (line {warning.LineNumber}");
                                                if (warning.ColumnNumber > 0)
                                                {
                                                    writer.Write($", col {warning.ColumnNumber}");
                                                }
                                                writer.Write(")");
                                            }
                                        }
                                        writer.WriteLine();
                                        writer.WriteLine($"  {warning.Message}");
                                        writer.WriteLine();
                                        break;
                                }
                            }

                            writer.WriteLine("</details>");
                            writer.WriteLine();
                        }
                    }
                    else
                    {
                        writer.WriteLine("✅ No errors or warnings");
                        writer.WriteLine();
                    }
                }
            }

            writer.WriteLine("---");
            writer.WriteLine($"*Build completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        }
        catch
        {
            // Silently fail if we can't write to the summary file (permission issues, etc.)
        }
    }

    /// <summary>
    /// Writes a diagnostic (error or warning) using GitHub Actions workflow commands.
    /// </summary>
    private void WriteDiagnostic(string type, string? file, int lineNumber, int columnNumber, int endColumnNumber, string? code, string? message)
    {
        // Format: ::{type} file={name},line={line},col={col},endColumn={endCol},title={title}::{message}
        var output = new StringBuilder();
        output.Append("::");
        output.Append(type);

        if (!string.IsNullOrEmpty(file))
        {
            output.Append(" file=");
            output.Append(EscapeProperty(file));

            if (lineNumber > 0)
            {
                output.Append(",line=");
                output.Append(lineNumber);

                if (columnNumber > 0)
                {
                    output.Append(",col=");
                    output.Append(columnNumber);

                    if (endColumnNumber > 0)
                    {
                        output.Append(",endColumn=");
                        output.Append(endColumnNumber);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(code))
        {
            output.Append(",title=");
            output.Append(EscapeProperty(code));
        }

        output.Append("::");
        output.Append(EscapeData(message ?? string.Empty));
        output.AppendLine();

        _write(output.ToString());
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

    #endregion
}
