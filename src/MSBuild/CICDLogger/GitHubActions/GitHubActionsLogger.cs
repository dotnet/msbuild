// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
/// Data stored for each project during the build.
/// </summary>
public sealed class GitHubActionsProjectData
{
    public string? ProjectFile { get; set; }
    public string? TargetFramework { get; set; }
    public string? RuntimeIdentifier { get; set; }
    public List<BuildErrorEventArgs> Errors { get; } = new();
    public List<BuildWarningEventArgs> Warnings { get; } = new();
    public List<BuildMessageEventArgs> ImportantMessages { get; } = new();
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
            projectData.Errors.Add(e);
        }
        else
        {
            // No project context, write immediately
            WriteError(e);
        }
    }

    protected override void OnWarningRaised(BuildWarningEventArgs e, GitHubActionsProjectData? projectData, GitHubActionsBuildData buildData)
    {
        buildData.TotalWarnings++;

        if (projectData != null)
        {
            // Buffer warning for output at project finished
            projectData.Warnings.Add(e);
        }
        else
        {
            // No project context, write immediately
            WriteWarning(e);
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
            projectData.ImportantMessages.Add(e);
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
            else if (projectData.Errors.Count > 0 || projectData.Warnings.Count > 0)
            {
                header.Append($" - {projectData.Errors.Count} error(s), {projectData.Warnings.Count} warning(s)");
            }

            // Use groups to collapse project output in GitHub Actions
            _write($"::group::{header}");
            _write(Environment.NewLine);

            // Output important messages
            foreach (var message in projectData.ImportantMessages)
            {
                _write(message.Message ?? string.Empty);
                _write(Environment.NewLine);
            }

            // Output all errors for this project
            foreach (var error in projectData.Errors)
            {
                WriteError(error);
            }

            // Output all warnings for this project
            foreach (var warning in projectData.Warnings)
            {
                WriteWarning(warning);
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
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Writes an error using GitHub Actions workflow commands.
    /// </summary>
    private void WriteError(BuildErrorEventArgs e)
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
        output.Append(EscapeData(e.Message ?? string.Empty));
        output.AppendLine();

        _write(output.ToString());
    }

    /// <summary>
    /// Writes a warning using GitHub Actions workflow commands.
    /// </summary>
    private void WriteWarning(BuildWarningEventArgs e)
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
        output.Append(EscapeData(e.Message ?? string.Empty));
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
