// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;
internal class PreferProjectReferenceCheck : Check
{
    private const string RuleId = "BC0104";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "PreferProjectReference",
        ResourceUtilities.GetResourceString("BuildCheck_BC0104_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0104_MessageFmt")!,
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning });

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

    private readonly Dictionary<string, (string, string)> _projectsPerReferencePath = new(MSBuildNameIgnoreCaseComparer.Default);
    private readonly Dictionary<string, string> _projectsPerOutputPath = new(MSBuildNameIgnoreCaseComparer.Default);
    private readonly HashSet<string> _projectsSeen = new(MSBuildNameIgnoreCaseComparer.Default);

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
    {
        // We want to avoid repeated checking of a same project (as it might be evaluated multiple times)
        //  for this reason we use a hashset with already seen projects.
        // We want to do the same prevention for both registered actions: EvaluatedPropertiesAction and EvaluatedItemsAction.
        //  To avoid the need to have separate hashset for each of those functions - we use a single one and we use the fact that
        //  both functions are always called after each other (EvaluatedPropertiesAction first, then EvaluatedItemsAction),
        //  so this function just checks the hashset (not to prevent execution of EvaluatedItemsAction) and EvaluatedItemsAction
        //  updates the hashset.
        if (_projectsSeen.Contains(context.Data.ProjectFilePath))
        {
            return;
        }

        string? targetPath;

        context.Data.EvaluatedProperties.TryGetValue(ItemMetadataNames.targetPath, out targetPath);

        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        targetPath = BuildCheckUtilities.RootEvaluatedPath(targetPath, context.Data.ProjectFilePath);

        _projectsPerOutputPath[targetPath] = context.Data.ProjectFilePath;

        (string, string) projectProducingOutput;
        if (_projectsPerReferencePath.TryGetValue(targetPath, out projectProducingOutput))
        {
            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                // Populating precise location tracked via https://github.com/dotnet/msbuild/issues/10383
                ElementLocation.EmptyLocation,
                Path.GetFileName(context.Data.ProjectFilePath),
                Path.GetFileName(projectProducingOutput.Item1),
                projectProducingOutput.Item2));
        }
    }

    private void EvaluatedItemsAction(BuildCheckDataContext<EvaluatedItemsCheckData> context)
    {
        // We want to avoid repeated checking of a same project (as it might be evaluated multiple times)
        //  for this reason we use a hashset with already seen projects.
        if (!_projectsSeen.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        foreach (ItemData itemData in context.Data.EnumerateItemsOfType(ItemNames.Reference))
        {
            string evaluatedReferencePath = itemData.EvaluatedInclude;
            string referenceFullPath = BuildCheckUtilities.RootEvaluatedPath(evaluatedReferencePath, context.Data.ProjectFilePath);

            _projectsPerReferencePath[referenceFullPath] = (context.Data.ProjectFilePath, evaluatedReferencePath);
            string? projectReferencedViaOutput;
            if (_projectsPerOutputPath.TryGetValue(referenceFullPath, out projectReferencedViaOutput))
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    // Populating precise location tracked via https://github.com/dotnet/msbuild/issues/10383
                    ElementLocation.EmptyLocation,
                    Path.GetFileName(projectReferencedViaOutput),
                    Path.GetFileName(context.Data.ProjectFilePath),
                    evaluatedReferencePath));
            }
        }
    }
}
