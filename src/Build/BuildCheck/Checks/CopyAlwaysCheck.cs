// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;
internal class CopyAlwaysCheck : Check
{
    private const string RuleId = "BC0106";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "AvoidCopyAlways",
        ResourceUtilities.GetResourceString("BuildCheck_BC0106_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0106_MessageFmt")!,
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.CopyAlwaysCheck";

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

        context.Data.EvaluatedProperties.TryGetValue("SkipUnchangedFilesOnCopyAlways", out string? skipUnchanged);

        if (skipUnchanged.IsMSBuildTrueString())
        {
            // Now we know that copy logic is optimized - so we do not need to check items. Avoiding the items check by inserting into lookup.
            _projectsSeen.Add(context.Data.ProjectFilePath);
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

        foreach (ItemData itemData in context.Data.EnumerateItemsOfTypes([ItemNames.Content, ItemNames.Compile, ItemNames.None, ItemNames.EmbeddedResource]))
        {
            foreach (KeyValuePair<string, string> keyValuePair in itemData.EnumerateMetadata())
            {
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(keyValuePair.Key, ItemMetadataNames.copyToOutputDirectory))
                {
                    if (MSBuildNameIgnoreCaseComparer.Default.Equals(keyValuePair.Value, ItemMetadataNames.copyAlways))
                    {
                        // Project {0} specifies '{0}' item '{1}', ...
                        context.ReportResult(BuildCheckResult.Create(
                            SupportedRule,
                            // Populating precise location tracked via https://github.com/dotnet/msbuild/issues/10383
                            ElementLocation.EmptyLocation,
                            Path.GetFileName(context.Data.ProjectFilePath),
                            itemData.Type,
                            itemData.EvaluatedInclude));
                    }

                    break;
                }
            }
        }
    }
}
