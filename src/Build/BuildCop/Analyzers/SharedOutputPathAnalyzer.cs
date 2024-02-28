// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.BuildCop.Infrastructure;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Analyzers;

// Some background on ids:
//  * https://github.com/dotnet/roslyn-analyzers/blob/main/src/Utilities/Compiler/DiagnosticCategoryAndIdRanges.txt
//  * https://github.com/dotnet/roslyn/issues/40351
//
// quick suggestion now - let's force external ids to start with 'X', for ours - avoid 'MSB'
//  maybe - BT - build static/styling; BA - build authoring; BE - build execution/environment; BC - build configuration

internal sealed class SharedOutputPathAnalyzer : BuildAnalyzer
{
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule("BC0101", "ConflictingOutputPath",
        "Two projects should not share their OutputPath nor IntermediateOutputPath locations", "Configuration",
        "Projects {0} and {1} have conflicting output paths: {2}.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, IsEnabled = true });

    public override string FriendlyName => "MSBuild.SharedOutputPathAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } =[SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCopContext context)
    {
        context.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    private readonly Dictionary<string, string> _projectsPerOutputPath = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly HashSet<string> _projects = new(StringComparer.CurrentCultureIgnoreCase);

    private void EvaluatedPropertiesAction(BuildAnalysisContext<EvaluatedPropertiesAnalysisData> context)
    {
        if (!_projects.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        string? binPath, objPath;

        context.Data.EvaluatedProperties.TryGetValue("OutputPath", out binPath);
        context.Data.EvaluatedProperties.TryGetValue("IntermediateOutputPath", out objPath);

        string? absoluteBinPath = CheckAndAddFullOutputPath(binPath, context);
        if (
            !string.IsNullOrEmpty(objPath) && !string.IsNullOrEmpty(absoluteBinPath) &&
            !objPath.Equals(binPath, StringComparison.CurrentCultureIgnoreCase)
            && !objPath.Equals(absoluteBinPath, StringComparison.CurrentCultureIgnoreCase)
        )
        {
            CheckAndAddFullOutputPath(objPath, context);
        }
    }

    private string? CheckAndAddFullOutputPath(string? path, BuildAnalysisContext<EvaluatedPropertiesAnalysisData> context)
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

        if (_projectsPerOutputPath.TryGetValue(path!, out string? conflictingProject))
        {
            context.ReportResult(BuildCopResult.Create(
                SupportedRule,
                // TODO: let's support transmitting locations of specific properties
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
