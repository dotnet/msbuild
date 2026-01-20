// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

/// <summary>
/// Check that detects when MSBuild Targets create "private" item lists (names starting with underscore)
/// that are not cleaned up at the end of the target.
/// </summary>
internal sealed class ItemDisposalCheck : Check
{
    private const string RuleId = "BC0303";

    private readonly SimpleProjectRootElementCache _cache = new();
    private readonly HashSet<string> _projectsSeen = new(MSBuildNameIgnoreCaseComparer.Default);

    public static readonly CheckRule SupportedRule = new(
        RuleId,
        "PrivateItemsNotDisposed",
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_Title"),
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_MessageFmt"),
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning, EvaluationCheckScope = EvaluationCheckScope.ProjectFileOnly });

    public override string FriendlyName => "MSBuild.ItemDisposalCheck";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    internal override bool IsBuiltIn => true;

    public override void Initialize(ConfigurationContext configurationContext)
    { }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) => registrationContext.RegisterEvaluatedItemsAction(EvaluatedItemsAction);

    private void EvaluatedItemsAction(BuildCheckDataContext<EvaluatedItemsCheckData> context)
    {
        if (!_projectsSeen.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        ProjectRootElement? projectRoot;
        try
        {
            projectRoot = ProjectRootElement.OpenProjectOrSolution(
                context.Data.ProjectFilePath,
                globalProperties: null,
                toolsVersion: null,
                _cache,
                isExplicitlyLoaded: false);
        }
        catch
        {
            return;
        }

        foreach (ProjectTargetElement target in projectRoot.Targets)
        {
            AnalyzeTarget(target, context.ReportResult);
        }
    }

    /// <summary>
    /// Analyzes a single target for private items that are not properly disposed.
    /// Single pass through item groups: Include adds to pending, Remove clears from pending.
    /// Items still pending at the end (and not exposed via Outputs/Returns) are violations.
    /// </summary>
    private static void AnalyzeTarget(ProjectTargetElement target, Action<BuildCheckResult> reportResult)
    {
        // Track private items with Include that need cleanup (case-insensitive)
        Dictionary<string, ProjectItemElement>? pendingPrivateItems = null;

        foreach (ProjectItemGroupElement itemGroup in target.ItemGroups)
        {
            foreach (ProjectItemElement item in itemGroup.Items)
            {
                string itemType = item.ItemType;

                // Only process private items (starting with underscore)
                if (itemType.Length == 0 || itemType[0] != '_')
                {
                    continue;
                }

                bool hasInclude = item.Include.Length > 0;
                bool hasRemove = item.Remove.Length > 0;

                if (hasInclude)
                {
                    pendingPrivateItems ??= new(MSBuildNameIgnoreCaseComparer.Default);
                    if (!pendingPrivateItems.ContainsKey(itemType))
                    {
                        pendingPrivateItems[itemType] = item;
                    }
                }

                if (hasRemove)
                {
                    pendingPrivateItems?.Remove(itemType);
                }
            }
        }

        if (pendingPrivateItems is null || pendingPrivateItems.Count == 0)
        {
            return;
        }

        // Get exposed items only if we have pending items to check
        HashSet<string>? exposedItemTypes = GetExposedItemTypes(target);

        foreach (KeyValuePair<string, ProjectItemElement> kvp in pendingPrivateItems)
        {
            if (exposedItemTypes?.Contains(kvp.Key) == true)
            {
                continue;
            }

            reportResult(BuildCheckResult.CreateBuiltIn(
                SupportedRule,
                kvp.Value.IncludeLocation ?? kvp.Value.Location,
                kvp.Key,
                target.Name));
        }
    }

    /// <summary>
    /// Extracts all item types referenced in the target's Outputs and Returns attributes.
    /// Returns null if neither attribute is set.
    /// </summary>
    private static HashSet<string>? GetExposedItemTypes(ProjectTargetElement target)
    {
        string? outputs = target.Outputs;
        string? returns = target.Returns;

        bool hasOutputs = !string.IsNullOrEmpty(outputs);
        bool hasReturns = !string.IsNullOrEmpty(returns);

        if (!hasOutputs && !hasReturns)
        {
            return null;
        }

        string[] expressions = (hasOutputs, hasReturns) switch
        {
            (true, true) => [outputs, returns],
            (true, false) => [outputs],
            (false, true) => [returns],
            _ => []
        };

        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(expressions);

        if (pair.Items is null)
        {
            return null;
        }

        HashSet<string> exposedItems = new(MSBuildNameIgnoreCaseComparer.Default);
        foreach (string itemType in pair.Items)
        {
            exposedItems.Add(itemType);
        }

        return exposedItems;
    }

    /// <summary>
    /// Internal method for testing - analyzes targets and collects results.
    /// </summary>
    internal void AnalyzeTargets(ProjectRootElement projectRoot, List<BuildCheckResult> results)
    {
        foreach (ProjectTargetElement target in projectRoot.Targets)
        {
            AnalyzeTarget(target, results.Add);
        }
    }
}
