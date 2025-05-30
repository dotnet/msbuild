// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Analyzers;

internal sealed class SharedOutputPathAnalyzer : BuildAnalyzer
{
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule("BC0101", "ConflictingOutputPath",
        "Two projects should not share their OutputPath nor IntermediateOutputPath locations",
        "Projects {0} and {1} have conflicting output paths: {2}.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, IsEnabled = true });

    public override string FriendlyName => "MSBuild.SharedOutputPathAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    private readonly Dictionary<string, string> _projectsPerOutputPath = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly HashSet<string> _projects = new(StringComparer.CurrentCultureIgnoreCase);

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        if (!_projects.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        string? binPath, objPath;
        context.Data.EvaluatedProperties.TryGetPathValue("OutputPath", out binPath);
        context.Data.EvaluatedProperties.TryGetPathValue("IntermediateOutputPath", out objPath);

        string? absoluteBinPath = CheckAndAddFullOutputPath(binPath, context);
        // Check objPath only if it is different from binPath
        if (
            !string.IsNullOrEmpty(objPath) && !string.IsNullOrEmpty(absoluteBinPath) &&
            !objPath.Equals(binPath, StringComparison.CurrentCultureIgnoreCase)
            && !objPath.Equals(absoluteBinPath, StringComparison.CurrentCultureIgnoreCase)
        )
        {
            CheckAndAddFullOutputPath(objPath, context);
        }
    }

    private string? CheckAndAddFullOutputPath(string? path, BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        string projectPath = context.Data.ProjectFilePath;

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Path.GetDirectoryName(projectPath)!, path);
        }

        // Normalize the path to avoid false negatives due to different path representations.
        path = Path.GetFullPath(path);

        if (_projectsPerOutputPath.TryGetValue(path!, out string? conflictingProject))
        {
            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                // Populating precise location tracked via https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=58661732
                ElementLocation.EmptyLocation,
                Path.GetFileName(projectPath),
                Path.GetFileName(conflictingProject),
                path!));
        }
        else
        {
            _projectsPerOutputPath[path!] = projectPath;
        }

        return path;
    }
}
