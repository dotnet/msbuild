// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Checks;
internal class PreferProjectReferenceCheck : Check
{
    private const string RuleId = "BC0104";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "PreferProjectReference",
        ResourceUtilities.GetResourceString("BuildCheck_BC0104_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0104_MessageFmt")!,
        new CheckConfiguration() { RuleId = "BC0104", Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.PreferProjectReferenceCheck";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
        registrationContext.RegisterEvaluatedItemsAction(EvaluatedItemsAction);
    }

    internal override bool IsBuiltIn => true;

    private readonly Dictionary<string, (string, string)> _projectsPerReferencPath = new(MSBuildNameIgnoreCaseComparer.Default);
    private readonly Dictionary<string, string> _projectsPerOutputPath = new(MSBuildNameIgnoreCaseComparer.Default);
    private readonly HashSet<string> _projects = new(MSBuildNameIgnoreCaseComparer.Default);

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
    {
        // Just check - do not add yet - it'll be done by EvaluatedItemsAction
        if (_projects.Contains(context.Data.ProjectFilePath))
        {
            return;
        }

        string? targetPath;

        context.Data.EvaluatedProperties.TryGetValue("TargetPath", out targetPath);

        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        targetPath = RootEvaluatedPath(targetPath, context.Data.ProjectFilePath);

        _projectsPerOutputPath[targetPath] = context.Data.ProjectFilePath;

        (string, string) projectProducingOutput;
        if (_projectsPerReferencPath.TryGetValue(targetPath, out projectProducingOutput))
        {
            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                // Populating precise location tracked via https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=58661732
                ElementLocation.EmptyLocation,
                Path.GetFileName(context.Data.ProjectFilePath),
                Path.GetFileName(projectProducingOutput.Item1),
                projectProducingOutput.Item2));
        }
    }

    private void EvaluatedItemsAction(BuildCheckDataContext<EvaluatedItemsCheckData> context)
    {
        if (!_projects.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        foreach (ItemData itemData in context.Data.EnumerateItemsOfType("Reference"))
        {
            string evaluatedReferencePath = itemData.EvaluatedInclude;
            string referenceFullPath = RootEvaluatedPath(evaluatedReferencePath, context.Data.ProjectFilePath);

            _projectsPerReferencPath[referenceFullPath] = (context.Data.ProjectFilePath, evaluatedReferencePath);
            string? projectReferencedViaOutput;
            if (_projectsPerOutputPath.TryGetValue(referenceFullPath, out projectReferencedViaOutput))
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    // Populating precise location tracked via https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=58661732
                    ElementLocation.EmptyLocation,
                    Path.GetFileName(projectReferencedViaOutput),
                    Path.GetFileName(context.Data.ProjectFilePath),
                    evaluatedReferencePath));
            }
        }
    }

    private static string RootEvaluatedPath(string path, string projectFilePath)
    {
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Path.GetDirectoryName(projectFilePath)!, path);
        }
        // Normalize the path to avoid false negatives due to different path representations.
        path = FileUtilities.NormalizePath(path)!;

        return path;
    }
}
