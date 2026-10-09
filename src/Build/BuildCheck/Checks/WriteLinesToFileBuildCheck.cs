// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal sealed class WriteLinesToFileBuildCheck : Check
{
    private const string RuleId = "BC0303";
    private const string TaskName = "WriteLinesToFile";
    private const string OverwriteParameterName = "Overwrite";

    public static CheckRule SupportedRule = new(
        RuleId,
        "WriteLinesToFileOverwrite",
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0303_MessageFmt")!,
        new CheckConfiguration() { RuleId = RuleId, Severity = CheckResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.WriteLinesToFileBuildCheck";

    internal override bool IsBuiltIn => true;

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterTaskInvocationAction(TaskInvocationAction);
    }

    private static void TaskInvocationAction(BuildCheckDataContext<TaskInvocationCheckData> context)
    {
        if (MSBuildNameIgnoreCaseComparer.Default.Equals(context.Data.TaskName, TaskName)
            && !HasOverwriteParameter(context.Data.Parameters))
        {
            context.ReportResult(BuildCheckResult.CreateBuiltIn(
                SupportedRule,
                context.Data.TaskInvocationLocation,
                Path.GetFileName(context.Data.ProjectFilePath)));
        }
    }

    private static bool HasOverwriteParameter(IReadOnlyDictionary<string, TaskInvocationCheckData.TaskParameter> parameters)
    {
        foreach (string parameterName in parameters.Keys)
        {
            if (MSBuildNameIgnoreCaseComparer.Default.Equals(parameterName, OverwriteParameterName))
            {
                return true;
            }
        }

        return false;
    }
}
