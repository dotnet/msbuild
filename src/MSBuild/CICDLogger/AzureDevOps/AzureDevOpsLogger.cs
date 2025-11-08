// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

#nullable enable

namespace Microsoft.Build.CommandLine.CICDLogger.AzureDevOps;

/// <summary>
/// Data captured from project evaluation.
/// </summary>
public sealed class AzureDevOpsEvalData
{
    public string? TargetFramework { get; set; }
    public string? RuntimeIdentifier { get; set; }
}

/// <summary>
/// Data stored for each project during the build.
/// </summary>
public sealed class AzureDevOpsProjectData
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
public sealed class AzureDevOpsBuildData
{
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
}

/// <summary>
/// Logger for Azure DevOps that formats build diagnostics using Azure Pipelines logging commands.
/// See: https://learn.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands
/// </summary>
public sealed class AzureDevOpsLogger : ProjectTrackingLoggerBase<AzureDevOpsEvalData, object?, AzureDevOpsProjectData, AzureDevOpsBuildData>
{
    private Action<string> _write = Console.Out.Write;

    /// <inheritdoc/>
    public override LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public override string? Parameters { get; set; }

    /// <summary>
    /// Detects if Azure DevOps environment is active.
    /// </summary>
    /// <returns>true if running in Azure DevOps; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
    }

    #region Abstract method implementations

    protected override AzureDevOpsEvalData CreateEvalData(ProjectEvaluationFinishedEventArgs e)
    {
        var evalData = new AzureDevOpsEvalData();

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

    protected override AzureDevOpsProjectData? CreateProjectData(AzureDevOpsEvalData evalData, ProjectStartedEventArgs e)
    {
        return new AzureDevOpsProjectData
        {
            ProjectFile = e.ProjectFile,
            TargetFramework = evalData?.TargetFramework,
            RuntimeIdentifier = evalData?.RuntimeIdentifier
        };
    }

    protected override AzureDevOpsBuildData CreateBuildData(BuildStartedEventArgs e)
    {
        return new AzureDevOpsBuildData();
    }

    #endregion

    #region Event handlers

    protected override void OnErrorRaised(BuildErrorEventArgs e, AzureDevOpsProjectData? projectData, AzureDevOpsBuildData buildData)
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

    protected override void OnWarningRaised(BuildWarningEventArgs e, AzureDevOpsProjectData? projectData, AzureDevOpsBuildData buildData)
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

    protected override void OnMessageRaised(BuildMessageEventArgs e, AzureDevOpsProjectData? projectData, AzureDevOpsBuildData buildData)
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

    protected override void OnProjectFinished(ProjectFinishedEventArgs e, AzureDevOpsProjectData projectData, AzureDevOpsBuildData buildData)
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

            // Output group header (collapsible)
            _write($"##[group]{header}");
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

            // End the group
            _write("##[endgroup]");
            _write(Environment.NewLine);
        }
    }

    protected override void OnBuildFinished(BuildFinishedEventArgs e, AzureDevOpsProjectData[] projectData, AzureDevOpsBuildData buildData)
    {
        if (Verbosity >= LoggerVerbosity.Minimal)
        {
            if (!e.Succeeded)
            {
                _write($"##vso[task.complete result=Failed]Build failed. {buildData.TotalErrors} error(s), {buildData.TotalWarnings} warning(s)");
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
    /// Writes an error using Azure DevOps logging commands.
    /// </summary>
    private void WriteError(BuildErrorEventArgs e)
    {
        _write(FormatDiagnostic("error", e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message));
    }

    /// <summary>
    /// Writes a warning using Azure DevOps logging commands.
    /// </summary>
    private void WriteWarning(BuildWarningEventArgs e)
    {
        _write(FormatDiagnostic("warning", e.File, e.LineNumber, e.ColumnNumber, e.Code, e.Message));
    }

    /// <summary>
    /// Formats a diagnostic (error or warning) using Azure DevOps logging commands.
    /// </summary>
    private string FormatDiagnostic(string type, string? file, int lineNumber, int columnNumber, string? code, string? message)
    {
        // Format: ##vso[task.logissue type={type};sourcepath=file;linenumber=line;columnnumber=col;code=code;]message
        var output = new StringBuilder();
        output.Append("##vso[task.logissue type=");
        output.Append(type);

        if (!string.IsNullOrEmpty(file))
        {
            output.Append(";sourcepath=");
            output.Append(EscapeProperty(file));

            if (lineNumber > 0)
            {
                output.Append(";linenumber=");
                output.Append(lineNumber);

                if (columnNumber > 0)
                {
                    output.Append(";columnnumber=");
                    output.Append(columnNumber);
                }
            }
        }

        if (!string.IsNullOrEmpty(code))
        {
            output.Append(";code=");
            output.Append(EscapeProperty(code));
        }

        output.Append(']');
        output.Append(EscapeData(message ?? string.Empty));
        output.AppendLine();

        return output.ToString();
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

    #endregion
}
