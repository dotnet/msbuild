// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Checks;
internal class EmbeddedResourceCheck : Check
{
    private const string RuleId = "BC0105";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "EmbeddedResourceCulture",
        ResourceUtilities.GetResourceString("BuildCheck_BC0105_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0105_MessageFmt")!,
        new CheckConfiguration() { RuleId = "BC0105", Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.EmbeddedResourceCulture";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedItemsAction(EvaluatedItemsAction);
    }

    internal override bool IsBuiltIn => true;

    private readonly HashSet<string> _projects = new(MSBuildNameIgnoreCaseComparer.Default);

    private void EvaluatedItemsAction(BuildCheckDataContext<EvaluatedItemsCheckData> context)
    {
        // Deduplication
        if (!_projects.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        foreach (ItemData itemData in context.Data.EnumerateItemsOfType("EmbeddedResource"))
        {
            string evaluatedEmbedItem = itemData.EvaluatedInclude;
            bool hasDoubleExtension = HasDoubleExtension(evaluatedEmbedItem);

            if (!hasDoubleExtension)
            {
                continue;
            }

            bool hasNeededMetadata = false;
            foreach (KeyValuePair<string, string> keyValuePair in itemData.EnumerateMetadata())
            {
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(keyValuePair.Key, ItemMetadataNames.culture))
                {
                    hasNeededMetadata = true;
                    break;
                }

                if (MSBuildNameIgnoreCaseComparer.Default.Equals(keyValuePair.Key, ItemMetadataNames.withCulture) &&
                    keyValuePair.Value.IsMSBuildFalseString())
                {
                    hasNeededMetadata = true;
                    break;
                }
            }

            if (!hasNeededMetadata)
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    // Populating precise location tracked via https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=58661732
                    ElementLocation.EmptyLocation,
                    Path.GetFileName(context.Data.ProjectFilePath),
                    evaluatedEmbedItem,
                    GetMiddleExtension(evaluatedEmbedItem)));
            }
        }
    }

    private static bool HasDoubleExtension(string s, char extensionSeparator = '.')
    {
        int firstIndex;
        return
            !string.IsNullOrEmpty(s) &&
            (firstIndex = s.IndexOf(extensionSeparator)) > -1 &&
            // We need at least 2 chars for this extension - separator and one char of extension,
            // so next extension can start closest 2 chars from this one
            // (this is to grace handle double dot - which is not double extension)
            firstIndex + 2 <= s.Length &&
            s.IndexOf(extensionSeparator, firstIndex + 2) > -1;
    }

    private string GetMiddleExtension(string s, char extensionSeparator = '.')
    {
        int firstIndex = s.IndexOf(extensionSeparator);
        if (firstIndex < 0 || firstIndex + 2 > s.Length)
        {
            return string.Empty;
        }
        int secondIndex = s.IndexOf(extensionSeparator, firstIndex + 2);
        if (secondIndex < firstIndex)
        {
            return string.Empty;
        }
        return s.Substring(firstIndex + 1, secondIndex - firstIndex - 1);
    }
}
