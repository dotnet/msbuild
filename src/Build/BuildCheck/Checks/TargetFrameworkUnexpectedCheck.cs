// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;
internal class TargetFrameworkUnexpectedCheck : Check
{
    private const string RuleId = "BC0108";
    public static CheckRule SupportedRule = new CheckRule(RuleId, "TargetFrameworkUnexpected",
        ResourceUtilities.GetResourceString("BuildCheck_BC0108_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0108_MessageFmt")!,
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.TargetFrameworkUnexpected";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    internal override bool IsBuiltIn => true;

    private readonly HashSet<string> _projectsSeen = new(MSBuildNameIgnoreCaseComparer.Default);

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
    {
        // We want to avoid repeated checking of a same project (as it might be evaluated multiple times)
        //  for this reason we use a hashset with already seen projects.
        if (!_projectsSeen.Add(context.Data.ProjectFilePath))
        {
            return;
        }

        string? frameworks = null;
        string? framework = null;
        // This is not SDK style project
        if ((!context.Data.EvaluatedProperties.TryGetValue(PropertyNames.UsingMicrosoftNETSdk, out string? usingSdkStr) ||
            !StringExtensions.IsMSBuildTrueString(usingSdkStr))
            &&
            // But TargetFramework(s) is specified
            (context.Data.EvaluatedProperties.TryGetValue(PropertyNames.TargetFrameworks, out frameworks) ||
            context.Data.EvaluatedProperties.TryGetValue(PropertyNames.TargetFramework, out framework)) &&
            !string.IsNullOrEmpty(framework ?? frameworks))
        {
            // {0} specifies 'TargetFrameworks' property '{1}' and 'TargetFramework' property '{2}'
            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                // Populating precise location tracked via https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=58661732
                ElementLocation.EmptyLocation,
                Path.GetFileName(context.Data.ProjectFilePath),
                framework ?? frameworks ?? string.Empty));
        }
    }
}
