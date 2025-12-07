// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public static CheckRule SupportedRule = new CheckRule(
        RuleId,
        "PrivateItemsNotDisposed",
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_Title"),
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_MessageFmt"),
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning, EvaluationCheckScope = EvaluationCheckScope.ProjectFileOnly });

    public override string FriendlyName => "MSBuild.ItemDisposalCheck";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    internal override bool IsBuiltIn => true;

    private readonly SimpleProjectRootElementCache _cache = new SimpleProjectRootElementCache();
    private readonly HashSet<string> _projectsSeen = new(MSBuildNameIgnoreCaseComparer.Default);

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // No custom configuration
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        // We use the ParsedItemsAction as a hook to get access to the project file path,
        // then we load and analyze the ProjectRootElement ourselves to check targets.
#pragma warning disable CS0618 // Type or member is obsolete - ParsedItemsCheckData is obsolete but we need it for the hook
        registrationContext.RegisterParsedItemsAction(ParsedItemsAction);
#pragma warning restore CS0618
    }

#pragma warning disable CS0618 // Type or member is obsolete
    private void ParsedItemsAction(BuildCheckDataContext<ParsedItemsCheckData> context)
#pragma warning restore CS0618
    {
        // Avoid repeated checking of the same project
        if (!_projectsSeen.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        // Load the ProjectRootElement to access targets
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
            // If we can't load the project, skip analysis
            return;
        }

        AnalyzeTargets(projectRoot, context);
    }

    /// <summary>
    /// Analyzes all targets in the project for private items that are not properly disposed.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete - ParsedItemsCheckData is obsolete but we need it for the hook
    private void AnalyzeTargets(ProjectRootElement projectRoot, BuildCheckDataContext<ParsedItemsCheckData> context)
#pragma warning restore CS0618
    {
        foreach (ProjectTargetElement target in projectRoot.Targets)
        {
            AnalyzeTarget(target, context);
        }
    }

    /// <summary>
    /// Analyzes a single target for private items that are not properly disposed.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete - ParsedItemsCheckData is obsolete but we need it for the hook
    private void AnalyzeTarget(ProjectTargetElement target, BuildCheckDataContext<ParsedItemsCheckData> context)
#pragma warning restore CS0618
    {
        // Collect all item types that are referenced in Outputs or Returns attributes
        HashSet<string> exposedItemTypes = GetExposedItemTypes(target);

        // Track item types that have Include operations and need cleanup
        // Key: item type (case-insensitive), Value: first Include element for that type
        Dictionary<string, ProjectItemElement> pendingPrivateItems = new(MSBuildNameIgnoreCaseComparer.Default);

        // Single pass: process operations in order
        // An Include adds an item type to pending list
        // A Remove after an Include clears that item type from pending
        foreach (ProjectItemGroupElement itemGroup in target.ItemGroups)
        {
            foreach (ProjectItemElement item in itemGroup.Items)
            {
                string itemType = item.ItemType;

                // Check if this is a private item (starts with underscore)
                if (!IsPrivateItemType(itemType))
                {
                    continue;
                }

                // Check for Include operation (creates items)
                if (!string.IsNullOrEmpty(item.Include))
                {
                    // Record this Include - only if not already tracked
                    if (!pendingPrivateItems.ContainsKey(itemType))
                    {
                        pendingPrivateItems[itemType] = item;
                    }
                }

                // Check for Remove operation - clears pending Include
                if (!string.IsNullOrEmpty(item.Remove))
                {
                    // Remove clears the pending Include for this item type
                    pendingPrivateItems.Remove(itemType);
                }
            }
        }

        // Report items that still have pending Includes (not cleaned up)
        foreach (var kvp in pendingPrivateItems)
        {
            string itemType = kvp.Key;
            ProjectItemElement firstIncludeElement = kvp.Value;

            // Skip if item type is exposed via Outputs or Returns
            if (exposedItemTypes.Contains(itemType))
            {
                continue;
            }

            // Report the violation
            context.ReportResult(BuildCheckResult.CreateBuiltIn(
                SupportedRule,
                firstIncludeElement.IncludeLocation ?? firstIncludeElement.Location,
                itemType,
                target.Name));
        }
    }

    /// <summary>
    /// Checks if an item type is considered "private" (starts with underscore).
    /// </summary>
    private static bool IsPrivateItemType(string itemType)
    {
        return !string.IsNullOrEmpty(itemType) && itemType[0] == '_';
    }

    /// <summary>
    /// Extracts all item types referenced in the target's Outputs and Returns attributes.
    /// </summary>
    private static HashSet<string> GetExposedItemTypes(ProjectTargetElement target)
    {
        HashSet<string> exposedItems = new(MSBuildNameIgnoreCaseComparer.Default);

        string[]? expressions =
            target switch {
                { Outputs: string outputs, Returns: string returns } => [outputs, returns],
                { Outputs: string outputs } => [outputs],
                { Returns: string returns } => [returns],
                _ => null
            };

        if (expressions is null)
        {
            return exposedItems;
        }

        ExtractItemReferences(expressions, exposedItems);

        return exposedItems;
    }

    /// <summary>
    /// Extracts item type references from a string using MSBuild's expression parser.
    /// This properly handles complex transforms, separators, and escaped characters.
    /// </summary>
    private static void ExtractItemReferences(string[] expressions, HashSet<string> itemTypes)
    {
        // Use MSBuild's ExpressionShredder to properly parse the expression
        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(expressions);

        // Add all found item types to the output set
        if (pair.Items != null)
        {
            foreach (string itemType in pair.Items)
            {
                itemTypes.Add(itemType);
            }
        }
    }

    /// <summary>
    /// Public method for testing purposes - analyzes targets and adds results to the provided list.
    /// </summary>
    internal void AnalyzeTargets(ProjectRootElement projectRoot, List<BuildCheckResult> results)
    {
        foreach (ProjectTargetElement target in projectRoot.Targets)
        {
            AnalyzeTargetForTesting(target, results);
        }
    }

    /// <summary>
    /// Internal method for testing - analyzes a single target.
    /// </summary>
    private void AnalyzeTargetForTesting(ProjectTargetElement target, List<BuildCheckResult> results)
    {
        // Collect all item types that are referenced in Outputs or Returns attributes
        HashSet<string> exposedItemTypes = GetExposedItemTypes(target);

        // Track item types that have Include operations and need cleanup
        // Key: item type (case-insensitive), Value: first Include element for that type
        Dictionary<string, ProjectItemElement> pendingPrivateItems = new(MSBuildNameIgnoreCaseComparer.Default);

        // Single pass: process operations in order
        // An Include adds an item type to pending list
        // A Remove after an Include clears that item type from pending
        foreach (ProjectItemGroupElement itemGroup in target.ItemGroups)
        {
            foreach (ProjectItemElement item in itemGroup.Items)
            {
                string itemType = item.ItemType;

                // Check if this is a private item (starts with underscore)
                if (!IsPrivateItemType(itemType))
                {
                    continue;
                }

                // Check for Include operation (creates items)
                if (!string.IsNullOrEmpty(item.Include))
                {
                    // Record this Include - only if not already tracked
                    if (!pendingPrivateItems.ContainsKey(itemType))
                    {
                        pendingPrivateItems[itemType] = item;
                    }
                }

                // Check for Remove operation - clears pending Include
                if (!string.IsNullOrEmpty(item.Remove))
                {
                    // Remove clears the pending Include for this item type
                    pendingPrivateItems.Remove(itemType);
                }
            }
        }

        // Report items that still have pending Includes (not cleaned up)
        foreach (var kvp in pendingPrivateItems)
        {
            string itemType = kvp.Key;
            ProjectItemElement firstIncludeElement = kvp.Value;

            // Skip if item type is exposed via Outputs or Returns
            if (exposedItemTypes.Contains(itemType))
            {
                continue;
            }

            // Report the violation
            results.Add(BuildCheckResult.CreateBuiltIn(
                SupportedRule,
                firstIncludeElement.IncludeLocation ?? firstIncludeElement.Location,
                itemType,
                target.Name));
        }
    }
}
