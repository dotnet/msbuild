// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;
internal class EmbeddedResourceCheck : Check
{
    private const string RuleId = "BC0105";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "EmbeddedResourceCulture",
        ResourceUtilities.GetResourceString("BuildCheck_BC0105_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0105_MessageFmt")!,
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning });

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

        foreach (ItemData itemData in context.Data.EnumerateItemsOfType(ItemNames.EmbeddedResource))
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
                    // Populating precise location tracked via https://github.com/dotnet/msbuild/issues/10383
                    ElementLocation.EmptyLocation,
                    Path.GetFileName(context.Data.ProjectFilePath),
                    evaluatedEmbedItem,
                    GetSupposedCultureExtension(evaluatedEmbedItem)));
            }
        }
    }

    private static bool HasDoubleExtension(string s)
    {
        const char extensionSeparator = '.';
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

    /// <summary>
    /// Returns the extension that is supposed to implicitly denote the culture.
    /// This is mimicking the behavior of Microsoft.Build.Tasks.Culture.GetItemCultureInfo
    /// </summary>
    private string GetSupposedCultureExtension(string s)
    {
        // If the item is defined as "Strings.en-US.resx", then we want to arrive to 'en-US'

        string extension = Path.GetExtension(Path.GetFileNameWithoutExtension(s));
        if (extension.Length > 1)
        {
            extension = extension.Substring(1);
        }
        return extension;
    }
}
